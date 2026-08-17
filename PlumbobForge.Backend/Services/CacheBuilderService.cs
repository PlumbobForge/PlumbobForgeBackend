using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlumbobForge.Backend.Configuration;
using PlumbobForge.Backend.Database;
using S3ForgeTools.GameFiles.Package;
using S3ForgeTools.GameFiles.TS3Pack;
using S3ForgeTools.Utils;

namespace PlumbobForge.Backend.Services;

public class CacheBuilderService
{
    private readonly AppDbContext _db;
    private readonly PlumbobForgeOptions _options;
    private readonly LocalizationService _localizer;

    private string? _cachedSims3FolderPath;

    public CacheBuilderService(AppDbContext db, IOptionsSnapshot<PlumbobForgeOptions> options, LocalizationService localizer)
    {
        _db = db;
        _options = options.Value;
        _localizer = localizer;
    }

    public string GetSims3FolderPath()
    {
        if (_cachedSims3FolderPath != null) return _cachedSims3FolderPath;

        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (Directory.Exists(eaDir))
        {
            var candidates = Directory.GetDirectories(eaDir)
                .Where(d => Path.GetFileName(d).Contains("Sims 3", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count > 0)
            {
                var exactMatch = candidates.FirstOrDefault(d => Path.GetFileName(d).Equals("The Sims 3", StringComparison.OrdinalIgnoreCase));
                _cachedSims3FolderPath = exactMatch ?? candidates[0];
                return _cachedSims3FolderPath;
            }
        }

        // Default fallback if no existing folder is found
        _cachedSims3FolderPath = Path.Combine(eaDir, "The Sims 3");
        return _cachedSims3FolderPath;
    }

    public async Task SyncToSims3Async(Action<string>? onProgress = null, bool forceRebuildStatic = false)
    {
        try
        {
            onProgress?.Invoke(_localizer.GetString("syncing_cache_to_sims3"));

            string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
            if (eaDir == null) return;

            string sims3ModsDir = Path.Combine(GetSims3FolderPath(), "Mods");
            string sims3CacheDir = Path.Combine(sims3ModsDir, "Cache");
            string sims3ConfigDir = Path.Combine(sims3CacheDir, "Config");
            string staticCacheDir = Path.Combine(sims3CacheDir, "StaticCache");

            Directory.CreateDirectory(sims3ModsDir);
            Directory.CreateDirectory(sims3CacheDir);
            Directory.CreateDirectory(sims3ConfigDir);

            EnsureMainResourceCfg(sims3ModsDir);

            bool isStatic = string.Equals(_options.CacheMethod, "Static", StringComparison.OrdinalIgnoreCase);
            if (isStatic)
            {
                string configResourceCfgStatic = Path.Combine(sims3ConfigDir, "Resource.cfg");
                using (StreamWriter sw = new StreamWriter(configResourceCfgStatic, false))
                {
                    sw.WriteLine("Priority 500");
                    sw.WriteLine(@"PackedFile ../StaticCache/*.package");
                    sw.WriteLine(@"PackedFile ../StaticCache/*/*.package");
                    sw.WriteLine(@"PackedFile ../StaticCache/*/*/*.package");
                    sw.WriteLine(@"PackedFile ../StaticCache/*/*/*/*.package");
                    sw.WriteLine(@"PackedFile ../StaticCache/*/*/*/*/*.package");
                }

                bool staticCacheExists = Directory.Exists(staticCacheDir) && Directory.GetFiles(staticCacheDir, "*.package").Length > 0;
                if (forceRebuildStatic || !staticCacheExists)
                {
                    await RebuildStaticCacheAsync(onProgress);
                }
            }
            else
            {
                // Clean up old orphaned cache sets
                var allSetFolderNames = _db.SetsEntities.Select(s => s.FolderName).ToList();
                allSetFolderNames.Add("Config");
                allSetFolderNames.Add("StaticCache");

                foreach (var dir in Directory.GetDirectories(sims3CacheDir))
                {
                    string dirName = Path.GetFileName(dir);
                    if (!allSetFolderNames.Contains(dirName))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }

            var activeConfig = await _db.ConfigEntities
                .Include(c => c.ConfigSetsEntities)
                .ThenInclude(cs => cs.SetsEntity)
                .FirstOrDefaultAsync(c => c.Active);

            var targetFilesToSync = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!isStatic)
            {
                string configResourceCfg = Path.Combine(sims3ConfigDir, "Resource.cfg");
                using (StreamWriter sw = new StreamWriter(configResourceCfg, false))
                {
                    sw.WriteLine("Priority 500");
                    if (activeConfig != null)
                    {
                        foreach (var cs in activeConfig.ConfigSetsEntities)
                        {
                            if (cs.SetsEntity != null)
                            {
                                string folderName = GetSetFolderName(cs.SetsEntity);
                                sw.WriteLine($"PackedFile ../{folderName}/*.package");
                                sw.WriteLine($"PackedFile ../{folderName}/*/*.package");
                                sw.WriteLine($"PackedFile ../{folderName}/*/*/*.package");
                                sw.WriteLine($"PackedFile ../{folderName}/*/*/*/*.package");
                                sw.WriteLine($"PackedFile ../{folderName}/*/*/*/*/*.package");

                                string sourceSetCacheDir = GetSetPath(cs.SetsEntity);
                                if (Directory.Exists(sourceSetCacheDir))
                                {
                                    string nonPackageFile = Path.Combine(sourceSetCacheDir, "NonPackageItems.txt");
                                    if (File.Exists(nonPackageFile))
                                    {
                                        foreach (var line in File.ReadAllLines(nonPackageFile))
                                        {
                                            if (string.IsNullOrWhiteSpace(line)) continue;
                                            string fileName = Path.GetFileName(line);
                                            string folder = Path.GetFileName(Path.GetDirectoryName(line)!);
                                            string destPath = GetTS3FolderPath(folder, fileName);
                                            targetFilesToSync[destPath] = line;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Remove un-synced non-package files
            var managedSubFolders = new[] { "Sims", "Lots", "Worlds" };
            foreach (var folder in managedSubFolders)
            {
                string targetDir = Path.Combine(GetSims3FolderPath(), folder);
                if (Directory.Exists(targetDir))
                {
                    foreach (var file in Directory.GetFiles(targetDir))
                    {
                        if (!targetFilesToSync.ContainsKey(file))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
            }

            // Copy missing target files
            foreach (var kvp in targetFilesToSync)
            {
                string destPath = kvp.Key;
                string sourcePath = kvp.Value;
                if (File.Exists(sourcePath) && !File.Exists(destPath))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        File.Copy(sourcePath, destPath, true);
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"Error syncing cache to Sims 3: {ex.Message}");
        }
    }

    public async Task RebuildCacheAsync(bool forceRebuild = false, Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
    {
        bool isStatic = string.Equals(_options.CacheMethod, "Static", StringComparison.OrdinalIgnoreCase);
        if (isStatic)
        {
            await RebuildStaticCacheAsync(onProgress, skippedFiles);
            return;
        }

        try
        {
            string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
            if (!string.IsNullOrEmpty(eaDir))
            {
                string staticDir = Path.Combine(GetSims3FolderPath(), "Mods", "Cache", "StaticCache");
                if (Directory.Exists(staticDir)) Directory.Delete(staticDir, true);
            }
        }
        catch { }

        var allSets = await _db.SetsEntities.Include(s => s.MetaEntities).ToListAsync();
        int total = allSets.Count;
        int current = 0;

        foreach (var set in allSets)
        {
            current++;
            if (forceRebuild || set.Dirty)
            {
                onProgress?.Invoke(_localizer.GetString("rebuilding_cache_for_set", set.Name, current, total));
                RebuildSet(set, onProgress, skippedFiles);
            }
        }
    }

    public async Task RebuildStaticCacheAsync(Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
    {
        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (string.IsNullOrEmpty(eaDir)) return;

        string staticCachePath = Path.Combine(GetSims3FolderPath(), "Mods", "Cache", "StaticCache");
        Directory.CreateDirectory(staticCachePath);

        foreach (var file in Directory.GetFiles(staticCachePath, "*.package"))
        {
            try { File.Delete(file); } catch { }
        }

        var allSetFolders = await _db.SetsEntities.Select(s => s.FolderName).ToListAsync();
        string sims3CacheDir = Path.Combine(GetSims3FolderPath(), "Mods", "Cache");
        foreach (var folder in allSetFolders)
        {
            if (string.IsNullOrWhiteSpace(folder) || folder == "Config" || folder == "StaticCache") continue;
            string fullPath = Path.Combine(sims3CacheDir, folder);
            if (Directory.Exists(fullPath))
            {
                try { Directory.Delete(fullPath, true); } catch { }
            }
        }

        var activeConfig = await _db.ConfigEntities
            .Include(c => c.ConfigSetsEntities)
            .ThenInclude(cs => cs.SetsEntity)
            .ThenInclude(s => s.MetaEntities)
            .FirstOrDefaultAsync(c => c.Active);

        if (activeConfig == null) return;

        var activeSetIds = activeConfig.ConfigSetsEntities.Where(cs => cs.SetsEntity != null).Select(cs => cs.SetsEntity!.Id).ToHashSet();
        var allMetaItems = await _db.MetaEntities.Where(m => m.SetsEntityId != null && activeSetIds.Contains(m.SetsEntityId.Value) && m.Enabled).ToListAsync();

        onProgress?.Invoke($"Rebuilding Static Cache for {allMetaItems.Count} items...");

        int packageCount = 0;
        DBPFPackageBuilder? outputPkg = null;
        var addedTgis = new HashSet<TGI_Key>();
        var nonPackageItems = new List<string>();
        int totalItems = allMetaItems.Count;
        int currentItem = 0;

        foreach (var item in allMetaItems)
        {
            currentItem++;
            if (currentItem % 10 == 0)
            {
                onProgress?.Invoke(_localizer.GetString("merging_item", currentItem, totalItems, "StaticCache"));
            }

            if (!File.Exists(item.CompleteFileName))
            {
                skippedFiles?.Add((item.FileName, "File not found on disk"));
                continue;
            }

            if (!item.FileName.ToLower().EndsWith(".sims3pack"))
            {
                DBPFPackage? dbpfPackage = null;
                try { dbpfPackage = new DBPFPackage(item.CompleteFileName); }
                catch (Exception ex)
                {
                    skippedFiles?.Add((item.FileName, ex.Message));
                    continue;
                }

                if (dbpfPackage != null)
                {
                    try
                    {
                        string originalName = Path.GetFileNameWithoutExtension(item.FileName);
                        if (dbpfPackage.Resources.Any(r => r.Key.Type == 107542056))
                        {
                            string path = InstallAsWorld(dbpfPackage, originalName, nonPackageItems);
                            if (path != null) nonPackageItems.Add(path);
                        }
                        else if (dbpfPackage.Resources.Any(r => r.Key.Type == 3496170587u))
                        {
                            string path = InstallAsLot(dbpfPackage, originalName, nonPackageItems);
                            if (path != null) nonPackageItems.Add(path);
                        }
                        else if (dbpfPackage.Resources.Any(r => r.Key.Type == 83396964))
                        {
                            string path = InstallAsSim(dbpfPackage, originalName, nonPackageItems);
                            if (path != null) nonPackageItems.Add(path);
                        }
                        else if (ValidatePackage(dbpfPackage))
                        {
                            RebuildPackageStatic(ref outputPkg, ref packageCount, staticCachePath, dbpfPackage, addedTgis);
                        }
                    }
                    finally { try { dbpfPackage.Close(); } catch { } }
                }
            }
        }

        if (outputPkg != null)
        {
            try { outputPkg.Close(); } catch { }
            outputPkg = null;
        }

        var allSets = await _db.SetsEntities.ToListAsync();
        foreach (var s in allSets) s.Dirty = false;
        await _db.SaveChangesAsync();
    }

    public string GetSetFolderName(SetsEntity activeSet)
    {
        string folderName = string.IsNullOrWhiteSpace(activeSet.FolderName) ? activeSet.Name : activeSet.FolderName;
        var invalidChars = Path.GetInvalidFileNameChars();
        folderName = new string(folderName.Where(c => !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(folderName)) folderName = $"Set_{activeSet.Id}";
        return folderName;
    }

    public string GetSetPath(SetsEntity activeSet)
    {
        if (activeSet == null) throw new ArgumentException("ActiveSet cannot be null");

        string folderName = GetSetFolderName(activeSet);

        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (eaDir != null)
        {
            return Path.Combine(GetSims3FolderPath(), "Mods", "Cache", folderName);
        }

        return Path.Combine(_options.SetCacheFolderPath, "Sets", folderName);
    }

    public string GetSetCachePath(string folderName)
    {
        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (!string.IsNullOrEmpty(eaDir))
        {
            return Path.Combine(GetSims3FolderPath(), "Mods", "Cache", folderName);
        }
        return Path.Combine(_options.SetCacheFolderPath, "Sets", folderName);
    }

    public void RebuildSet(SetsEntity activeSet, Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
    {
        if (activeSet.IsLegacy) return;

        string setPath = GetSetPath(activeSet);
        Directory.CreateDirectory(setPath);

        foreach (var file in Directory.GetFiles(setPath, "ModBUILD*.new"))
        {
            try { File.Delete(file); } catch { }
        }

        int packageCount = 0;
        DBPFPackageBuilder? outputPkg = null;

        var metaEntities = activeSet.MetaEntities.ToList();
        int totalItems = metaEntities.Count;
        int currentItem = 0;

        var addedTgis = new HashSet<TGI_Key>();
        var nonPackageItems = new List<string>();

        foreach (var item in metaEntities)
        {
            currentItem++;
            if (currentItem % 10 == 0)
            {
                onProgress?.Invoke(_localizer.GetString("merging_item", currentItem, totalItems, activeSet.Name));
            }
            if (!item.Enabled) continue;

            if (!File.Exists(item.CompleteFileName))
            {
                skippedFiles?.Add((item.FileName, "File not found on disk"));
                onProgress?.Invoke(_localizer.GetString("skipping_missing_file", item.FileName));
                continue;
            }

            if (!item.FileName.ToLower().EndsWith(".sims3pack"))
            {
                DBPFPackage? dbpfPackage = null;
                try
                {
                    dbpfPackage = new DBPFPackage(item.CompleteFileName);
                }
                catch (Exception ex)
                {
                    skippedFiles?.Add((item.FileName, ex.Message));
                    onProgress?.Invoke(_localizer.GetString("skipping_unreadable_file", item.FileName, ex.Message));
                    continue;
                }

                if (dbpfPackage != null)
                {
                    try
                    {
                        string originalName = Path.GetFileNameWithoutExtension(item.FileName);
                        if (dbpfPackage.Resources.Any(r => r.Key.Type == 107542056)) // World
                        {
                            string path = InstallAsWorld(dbpfPackage, originalName, nonPackageItems);
                            if (path != null) nonPackageItems.Add(path);
                        }
                        else if (dbpfPackage.Resources.Any(r => r.Key.Type == 3496170587u)) // Lot
                        {
                            string path = InstallAsLot(dbpfPackage, originalName, nonPackageItems);
                            if (path != null) nonPackageItems.Add(path);
                        }
                        else if (dbpfPackage.Resources.Any(r => r.Key.Type == 83396964)) // Sim
                        {
                            string path = InstallAsSim(dbpfPackage, originalName, nonPackageItems);
                            if (path != null) nonPackageItems.Add(path);
                        }
                        else if (ValidatePackage(dbpfPackage))
                        {
                            RebuildPackage(ref outputPkg, ref packageCount, activeSet, dbpfPackage, addedTgis);
                        }
                        else
                        {
                            skippedFiles?.Add((item.FileName, "Failed validation (possible corrupt data)"));
                            onProgress?.Invoke(_localizer.GetString("skipping_invalid_package", item.FileName));
                        }
                    }
                    catch (Exception ex)
                    {
                        skippedFiles?.Add((item.FileName, ex.Message));
                        onProgress?.Invoke(_localizer.GetString("error_processing_package", item.FileName, ex.Message));
                    }
                    finally
                    {
                        try { dbpfPackage.Close(); } catch { }
                    }
                }
            }
            else
            {
                try
                {
                    using (Sims3Pack sims3Pack = new Sims3Pack(item.CompleteFileName))
                    {
                        string originalName = Path.GetFileNameWithoutExtension(item.FileName);
                        foreach (DBPFPackage package in sims3Pack.Packages)
                        {
                            if (package.Resources.Any(r => r.Key.Type == 107542056)) // World
                            {
                                string path = InstallAsWorld(package, originalName, nonPackageItems);
                                if (path != null) nonPackageItems.Add(path);
                            }
                            else if (package.Resources.Any(r => r.Key.Type == 3496170587u)) // Lot
                            {
                                string path = InstallAsLot(package, originalName, nonPackageItems);
                                if (path != null) nonPackageItems.Add(path);
                            }
                            else if (package.Resources.Any(r => r.Key.Type == 83396964)) // Sim
                            {
                                string path = InstallAsSim(package, originalName, nonPackageItems);
                                if (path != null) nonPackageItems.Add(path);
                            }
                            else if (ValidatePackage(package))
                            {
                                RebuildPackage(ref outputPkg, ref packageCount, activeSet, package, addedTgis);
                            }
                            else
                            {
                                skippedFiles?.Add((item.FileName, "Failed validation (possible corrupt data)"));
                                onProgress?.Invoke(_localizer.GetString("skipping_invalid_package_sims3pack", item.FileName));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    skippedFiles?.Add((item.FileName, ex.Message));
                    onProgress?.Invoke(_localizer.GetString("skipping_unreadable_sims3pack", item.FileName, ex.Message));
                }
            }
        }

        if (outputPkg != null)
        {
            try { outputPkg.Close(); } catch { }
            outputPkg = null;
        }

        try
        {
            string[] oldFiles = Directory.GetFiles(setPath, "ModBUILD*.package");
            foreach (string path in oldFiles)
            {
                try { File.Delete(path); } catch { }
            }

            string[] newFiles = Directory.GetFiles(setPath, "ModBUILD*.new");
            foreach (string path in newFiles)
            {
                try
                {
                    string dest = Path.ChangeExtension(path, ".package");
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(path, dest);
                }
                catch { }
            }
        }
        catch { }

        string nonPackageFile = Path.Combine(setPath, "NonPackageItems.txt");
        if (nonPackageItems.Count > 0)
        {
            try { File.WriteAllLines(nonPackageFile, nonPackageItems); } catch { }
        }
        else if (File.Exists(nonPackageFile))
        {
            try { File.Delete(nonPackageFile); } catch { }
        }

        activeSet.Dirty = false;
        try { _db.SaveChanges(); } catch { }
    }

    private void RebuildPackageStatic(ref DBPFPackageBuilder? outputPkg, ref int packageCount, string staticCachePath, DBPFPackage inputPkg, HashSet<TGI_Key> addedTgis)
    {
        var validResources = new List<ResourceEntry>();

        foreach (var resource in inputPkg.Resources)
        {
            if (resource.Key.Type == 3571055589u) FixPTRN(resource);
            else if (resource.Key.Type == 53690476) FixPTRN_XML(resource);

            if (ValidateResource(resource.Key) && addedTgis.Add(resource.Key))
            {
                validResources.Add(resource);
            }
        }

        if (_options.CompressionLevel > 0)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.ForEach(validResources, parallelOptions, resource =>
            {
                try { resource.Compress(_options.CompressionLevel); }
                catch (Exception) { /* If compression fails, keep original uncompressed resource */ }
            });
        }

        foreach (var resource in validResources)
        {
            try
            {
                if (outputPkg == null)
                {
                    string path = Path.Combine(staticCachePath, $"StaticBundle_{packageCount++}.package");
                    outputPkg = new DBPFPackageBuilder(path);
                }

                outputPkg.AddResource(resource);

                if (outputPkg.PackageSize >= 1073741824)
                {
                    outputPkg.Close();
                    outputPkg = null;
                }
            }
            catch (Exception) { }
        }
    }

    private string GetTS3FolderPath(string folderName, string fileName)
    {
        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (eaDir != null)
        {
            return Path.Combine(GetSims3FolderPath(), folderName, fileName);
        }
        return Path.Combine(_options.DocumentBaseDir, folderName, fileName);
    }

    private string InstallAsSim(DBPFPackage package, string name, List<string> nonPackageItems)
    {
        string basePath = Path.ChangeExtension(Path.Combine(_options.DocumentBaseDir, "Sims", name), ".sim");
        string path = basePath;
        int count = 1;
        while (nonPackageItems.Contains(path))
        {
            path = Path.ChangeExtension(Path.Combine(_options.DocumentBaseDir, "Sims", $"{name}_{count}"), ".sim");
            count++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) package.Export(path);
        return path;
    }

    private string InstallAsLot(DBPFPackage package, string name, List<string> nonPackageItems)
    {
        string basePath = Path.ChangeExtension(Path.Combine(_options.DocumentBaseDir, "Lots", name), ".package");
        string path = basePath;
        int count = 1;
        while (nonPackageItems.Contains(path))
        {
            path = Path.ChangeExtension(Path.Combine(_options.DocumentBaseDir, "Lots", $"{name}_{count}"), ".package");
            count++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) package.Export(path);
        return path;
    }

    private string InstallAsWorld(DBPFPackage package, string name, List<string> nonPackageItems)
    {
        string basePath = Path.ChangeExtension(Path.Combine(_options.DocumentBaseDir, "Worlds", name), ".world");
        string path = basePath;
        int count = 1;
        while (nonPackageItems.Contains(path))
        {
            path = Path.ChangeExtension(Path.Combine(_options.DocumentBaseDir, "Worlds", $"{name}_{count}"), ".world");
            count++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) package.Export(path);
        return path;
    }

    private bool ValidatePackage(DBPFPackage package)
    {
        var dollDressedKey = new TGI_Key(832458525u, 0u, 4064452635095512314uL);
        foreach (var resource in package.Resources)
        {
            if (resource.Key.Type == dollDressedKey.Type && resource.Key.Group == dollDressedKey.Group && resource.Key.Instance == dollDressedKey.Instance)
            {
                return false;
            }
        }
        return true;
    }

    private bool ValidateResource(TGI_Key key)
    {
        if (key.Type == 1944665835) return false;
        return true;
    }

    private void RebuildPackage(ref DBPFPackageBuilder? outputPkg, ref int packageCount, SetsEntity activeSet, DBPFPackage package, HashSet<TGI_Key> addedTgis)
    {
        var validResources = new List<ResourceEntry>();

        foreach (var resource in package.Resources)
        {
            if (resource.Key.Type == 3571055589u) FixPTRN(resource);
            else if (resource.Key.Type == 53690476) FixPTRN_XML(resource);

            if (ValidateResource(resource.Key) && addedTgis.Add(resource.Key))
            {
                validResources.Add(resource);
            }
        }

        if (_options.CompressionLevel > 0)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.ForEach(validResources, parallelOptions, resource =>
            {
                try { resource.Compress(_options.CompressionLevel); }
                catch (Exception) { /* If compression fails, keep original uncompressed resource */ }
            });
        }

        foreach (var resource in validResources)
        {
            try
            {
                if (outputPkg == null)
                {
                    string path = Path.Combine(GetSetPath(activeSet), $"ModBUILD{packageCount++}.new");
                    outputPkg = new DBPFPackageBuilder(path);
                }

                outputPkg.AddResource(resource);

                if (outputPkg.PackageSize >= 1073741824)
                {
                    outputPkg.Close();
                    outputPkg = null;
                }
            }
            catch (Exception) { }
        }
    }

    private void FixPTRN(ResourceEntry resource)
    {
        XmlDocument xmlDocument = new XmlDocument();
        try { xmlDocument.Load(resource.GetStream()); } catch (Exception) { return; }
        bool flag = false;
        XmlNodeList elementsByTagName = xmlDocument.GetElementsByTagName("pattern");
        foreach (XmlElement item in elementsByTagName)
        {
            string attribute = item.GetAttribute("reskey");
            if (!string.IsNullOrEmpty(attribute) && attribute.Contains(": "))
            {
                item.SetAttribute("reskey", attribute.Replace(": ", ":"));
                flag = true;
            }
        }
        if (flag)
        {
            MemoryStream memoryStream = new MemoryStream();
            xmlDocument.Save(memoryStream);
            resource.ChangeStream(memoryStream);
        }
    }

    private void FixPTRN_XML(ResourceEntry resource)
    {
        XmlDocument xmlDocument = new XmlDocument();
        try { xmlDocument.Load(resource.GetStream()); } catch (Exception) { return; }
        bool flag = false;
        XmlNodeList elementsByTagName = xmlDocument.GetElementsByTagName("complate");
        foreach (XmlElement item in elementsByTagName)
        {
            string attribute = item.GetAttribute("reskey");
            if (!string.IsNullOrEmpty(attribute) && attribute.Contains(": "))
            {
                item.SetAttribute("reskey", attribute.Replace(": ", ":"));
                flag = true;
            }
        }
        if (flag)
        {
            MemoryStream memoryStream = new MemoryStream();
            xmlDocument.Save(memoryStream);
            resource.ChangeStream(memoryStream);
        }
    }

    private void EnsureMainResourceCfg(string sims3ModsDir)
    {
        string mainResourceCfg = Path.Combine(sims3ModsDir, "Resource.cfg");
        if (!File.Exists(mainResourceCfg))
        {
            try
            {
                using var sw = new StreamWriter(mainResourceCfg, false);
                sw.WriteLine("Priority 500");
                sw.WriteLine("PackedFile Cache/Config/Resource.cfg");
                sw.WriteLine("PackedFile Packages/*.package");
                sw.WriteLine("PackedFile Packages/*/*.package");
                sw.WriteLine("PackedFile Packages/*/*/*.package");
                sw.WriteLine("PackedFile Packages/*/*/*/*.package");
                sw.WriteLine("PackedFile Packages/*/*/*/*/*.package");
                sw.WriteLine("PackedFile Overrides/*.package");
                sw.WriteLine("PackedFile Overrides/*/*.package");
                sw.WriteLine("PackedFile Overrides/*/*/*.package");
                sw.WriteLine("PackedFile Overrides/*/*/*/*.package");
                sw.WriteLine("PackedFile Overrides/*/*/*/*/*.package");
            }
            catch { }
        }
        else
        {
            try
            {
                string content = File.ReadAllText(mainResourceCfg);
                if (!content.Contains("PackedFile Cache/Config/Resource.cfg", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                    int insertIndex = 0;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].TrimStart().StartsWith("Priority", StringComparison.OrdinalIgnoreCase))
                        {
                            insertIndex = i + 1;
                            break;
                        }
                    }
                    lines.Insert(insertIndex, "PackedFile Cache/Config/Resource.cfg");
                    File.WriteAllText(mainResourceCfg, string.Join(Environment.NewLine, lines));
                }
            }
            catch { }
        }
    }
}
