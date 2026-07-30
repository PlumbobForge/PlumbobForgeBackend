using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml;
using PlumbobForge.Backend.Database;
using PlumbobForge.Backend.Configuration;
using S3ForgeTools.GameFiles.Package;
using S3ForgeTools.GameFiles.TS3Pack;
using S3ForgeTools.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PlumbobForge.Backend.Services;

public class PKGManager
{
    private readonly AppDbContext _db;
    private readonly PlumbobForgeOptions _options;

    public PKGManager(AppDbContext db, IOptionsSnapshot<PlumbobForgeOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task RunAsync(bool isRefresh, Action<string>? onProgress = null)
    {
        if (!isRefresh)
        {
            CreateFolders();
        }
        else
        {
            try
            {
                var skippedFiles = new List<(string FileName, string Reason)>();

                onProgress?.Invoke("Checking for new orphan packages...");
                await CheckOrphanPackagesAsync();
                onProgress?.Invoke("Validating set hierarchy...");
                await CheckSetParentingAsync();
                onProgress?.Invoke("Initializing configuration profile...");
                await CheckConfigurationsAsync();
                onProgress?.Invoke("Scanning cache requirements...");
                await RebuildCacheAsync(false, onProgress, skippedFiles);
                await SyncToSims3Async(onProgress);

                if (skippedFiles.Count > 0)
                {
                    onProgress?.Invoke($"⚠ Rebuilding partially completed. {skippedFiles.Count} file(s) were skipped:");
                    foreach (var (fileName, reason) in skippedFiles)
                    {
                        onProgress?.Invoke($"  - {fileName}: {reason}");
                    }
                }
                else
                {
                    onProgress?.Invoke("Scan complete.");
                }
            }
            catch (Exception ex)
            {
                onProgress?.Invoke($"Error during rebuild: {ex.Message}");
            }
        }
    }

    public async Task UploadFilesAsync(Microsoft.AspNetCore.Http.IFormFileCollection files, Action<string>? onProgress = null)
    {
        try
        {
            if (!Directory.Exists(_options.ManagedPackageFolderPath))
            {
                Directory.CreateDirectory(_options.ManagedPackageFolderPath);
            }
            int movedCount = 0;

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                string fileName = Path.GetFileName(file.FileName);
                string dest = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                try
                {
                    if (File.Exists(dest))
                    {
                        dest = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                    }
                    using (var stream = new FileStream(dest, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    movedCount++;
                }
                catch (Exception ex)
                {
                    onProgress?.Invoke($"Failed to import {fileName}: {ex.Message}");
                }
            }

            if (movedCount > 0)
            {
                onProgress?.Invoke($"Imported {movedCount} files to Library. Registering in database...");
                await CheckOrphanPackagesAsync();
                await CheckConfigurationsAsync();
                await SyncToSims3Async(onProgress);
                onProgress?.Invoke("Import complete.");
            }
            else
            {
                onProgress?.Invoke("No files were imported.");
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"CRASH: {ex.Message} {ex.StackTrace}");
        }
    }

    public async Task ImportFilesAsync(string[] files, Action<string>? onProgress = null)
    {
        try
        {
            if (!Directory.Exists(_options.ManagedPackageFolderPath))
            {
                Directory.CreateDirectory(_options.ManagedPackageFolderPath);
            }
            int movedCount = 0;

            foreach (var file in files)
            {
                if (!File.Exists(file)) continue;

                string fileName = Path.GetFileName(file);
                string dest = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                try
                {
                    if (File.Exists(dest))
                    {
                        dest = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                    }
                    File.Copy(file, dest);
                    movedCount++;
                }
                catch (Exception ex)
                {
                    onProgress?.Invoke($"Failed to import {fileName}: {ex.Message}");
                }
            }

            if (movedCount > 0)
            {
                onProgress?.Invoke($"Imported {movedCount} files to Library. Registering in database...");
                await CheckOrphanPackagesAsync();
                await CheckConfigurationsAsync();
                await SyncToSims3Async(onProgress);
                onProgress?.Invoke("Import complete.");
            }
            else
            {
                onProgress?.Invoke("No files were imported.");
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"CRASH: {ex.Message} {ex.StackTrace}");
        }
    }

    public async Task ImportFromDownloadsAsync(Action<string>? onProgress = null)
    {
        try
        {
            string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
            if (eaDir == null)
            {
                onProgress?.Invoke("Could not find Electronic Arts directory.");
                return;
            }

            string downloadsDir = Path.Combine(eaDir, "The Sims 3", "Downloads");
            if (!Directory.Exists(downloadsDir))
            {
                onProgress?.Invoke("The Sims 3 Downloads folder does not exist.");
                return;
            }

            onProgress?.Invoke("Scanning Downloads folder for packages...");

            var packageFiles = Directory.GetFiles(downloadsDir, "*.package");
            var sims3packFiles = Directory.GetFiles(downloadsDir, "*.sims3pack");
            var files = packageFiles.Concat(sims3packFiles).ToArray();

            if (files.Length == 0)
            {
                onProgress?.Invoke("No .package or .sims3pack files found in Downloads.");
                return;
            }

            if (!Directory.Exists(_options.ManagedPackageFolderPath))
            {
                Directory.CreateDirectory(_options.ManagedPackageFolderPath);
            }
            int movedCount = 0;

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string dest = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                try
                {
                    if (File.Exists(dest))
                    {
                        // Avoid overwriting existing library items. Try to rename or skip.
                        dest = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                    }
                    File.Move(file, dest);
                    movedCount++;
                }
                catch (Exception ex)
                {
                    onProgress?.Invoke($"Failed to move {fileName}: {ex.Message}");
                }
            }

            if (movedCount > 0)
            {
                onProgress?.Invoke($"Moved {movedCount} files to Library. Registering in database...");
                await CheckOrphanPackagesAsync();
                onProgress?.Invoke("Import complete.");
            }
            else
            {
                onProgress?.Invoke("No files were moved.");
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"CRASH: {ex.Message} {ex.StackTrace}");
        }
    }

    public async Task AutoFixAsync(Action<string>? onProgress = null)
    {
        onProgress?.Invoke("Starting Auto-Fix Routine...");

        // 1. Ensure Default Set exists
        var defaultSet = await _db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
        if (defaultSet == null)
        {
            defaultSet = new SetsEntity { Name = "Default", FolderName = "Default", IsLegacy = false, Dirty = true, LongName = "Default", IsExpanded = true, IsDefault = true };
            _db.SetsEntities.Add(defaultSet);
            await _db.SaveChangesAsync();
        }

        // 2. Fix Orphaned Items
        onProgress?.Invoke("Checking for items with missing sets...");
        var orphans = await _db.MetaEntities.Where(m => m.SetsEntityId == null).ToListAsync();
        if (orphans.Any())
        {
            onProgress?.Invoke($"Found {orphans.Count} unassigned items. Moving to Default set...");
            foreach (var item in orphans)
            {
                item.SetsEntityId = defaultSet.Id;
            }
            defaultSet.Dirty = true;
        }

        // 3. Mark ALL Sets as Dirty for a full rebuild
        onProgress?.Invoke("Marking all sets for a complete cache rebuild...");
        var allSets = await _db.SetsEntities.ToListAsync();
        foreach (var set in allSets)
        {
            set.Dirty = true;
        }
        await _db.SaveChangesAsync();

        // 4. Force a complete rebuild
        onProgress?.Invoke("Delegating to standard rebuild process...");
        await RunAsync(true, onProgress);

        onProgress?.Invoke("Auto-Fix complete.");
    }

    private async Task CheckConfigurationsAsync()
    {
        bool hasConfig = await _db.ConfigEntities.AnyAsync();
        if (!hasConfig)
        {
            var defConfig = new ConfigEntity { Name = "Default", Default = true, Active = true };
            _db.ConfigEntities.Add(defConfig);

            var allSets = await _db.SetsEntities.ToListAsync();
            foreach (var set in allSets)
            {
                _db.ConfigSetsEntities.Add(new ConfigSetsEntity { ConfigEntity = defConfig, SetsEntity = set });
            }
            await _db.SaveChangesAsync();
        }
    }

    public async Task SyncToSims3Async(Action<string>? onProgress = null)
    {
        try
        {
            onProgress?.Invoke("Syncing cache to The Sims 3 folder...");

            string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
            if (eaDir == null) return;

            string sims3ModsDir = Path.Combine(eaDir, "The Sims 3", "Mods");
            string sims3CacheDir = Path.Combine(sims3ModsDir, "Cache");
            string sims3ConfigDir = Path.Combine(sims3CacheDir, "Config");

            Directory.CreateDirectory(sims3ModsDir);
            Directory.CreateDirectory(sims3CacheDir);
            Directory.CreateDirectory(sims3ConfigDir);

            // Clean up old orphaned cache sets (any folder that no longer exists in DB)
            var allSetFolderNames = _db.SetsEntities.Select(s => s.FolderName).ToList();
            allSetFolderNames.Add("Config"); // Always keep Config

            foreach (var dir in Directory.GetDirectories(sims3CacheDir))
            {
                string dirName = Path.GetFileName(dir);
                if (!allSetFolderNames.Contains(dirName))
                {
                    Directory.Delete(dir, true);
                }
            }

            // Clean up previously synced NonPackageItems (Worlds, Lots, Sims)
            string syncRecordFile = Path.Combine(_options.SetCacheFolderPath, "SyncedItems.txt");
            if (File.Exists(syncRecordFile))
            {
                var oldFiles = File.ReadAllLines(syncRecordFile);
                foreach (var f in oldFiles)
                {
                    if (File.Exists(f))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }

            var newSyncedFiles = new List<string>();

            // 2. Make a Resource.cfg in Electronic Arts\The Sims 3\Mods\Cache\Config
            string configResourceCfg = Path.Combine(sims3ConfigDir, "Resource.cfg");
            using (StreamWriter sw = new StreamWriter(configResourceCfg, false))
            {
                sw.WriteLine("Priority 500");

                var activeConfig = await _db.ConfigEntities
                    .Include(c => c.ConfigSetsEntities)
                    .ThenInclude(cs => cs.SetsEntity)
                    .FirstOrDefaultAsync(c => c.Active);

                if (activeConfig != null)
                {
                    foreach (var cs in activeConfig.ConfigSetsEntities)
                    {
                        if (cs.SetsEntity != null)
                        {
                            string folderName = GetSetFolderName(cs.SetsEntity);
                            sw.WriteLine($@"PackedFile ../{folderName}/*.package");
                            sw.WriteLine($@"PackedFile ../{folderName}/*/*.package");

                            // Sync Non-Package items for this active set
                            string setPath = GetSetPath(cs.SetsEntity);
                            string npFile = Path.Combine(setPath, "NonPackageItems.txt");
                            if (File.Exists(npFile))
                            {
                                var items = File.ReadAllLines(npFile);
                                foreach (var itemPath in items)
                                {
                                    if (File.Exists(itemPath))
                                    {
                                        string destPath = "";

                                        if (itemPath.EndsWith(".world", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (!string.IsNullOrWhiteSpace(_options.GameFilesDir))
                                            {
                                                destPath = Path.Combine(_options.GameFilesDir, "GameData", "Shared", "NonPackaged", "Worlds", Path.GetFileName(itemPath));
                                            }
                                        }
                                        else if (itemPath.EndsWith(".sim", StringComparison.OrdinalIgnoreCase))
                                        {
                                            destPath = Path.Combine(eaDir, "The Sims 3", "SavedSims", Path.GetFileName(itemPath));
                                        }
                                        else if (itemPath.EndsWith(".package", StringComparison.OrdinalIgnoreCase))
                                        {
                                            destPath = Path.Combine(eaDir, "The Sims 3", "Library", Path.GetFileName(itemPath));
                                        }

                                        if (!string.IsNullOrEmpty(destPath))
                                        {
                                            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                                            File.Copy(itemPath, destPath, true);
                                            newSyncedFiles.Add(destPath);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
              }

              Directory.CreateDirectory(_options.SetCacheFolderPath);
              File.WriteAllLines(syncRecordFile, newSyncedFiles);

              // 3. Make sure the main Resource.cfg has the scan line
            string mainResourceCfg = Path.Combine(sims3ModsDir, "Resource.cfg");
            string scanPath = "Cache/Config";
            string scanLine = $"Scan \"{scanPath}/\"";

            if (File.Exists(mainResourceCfg))
            {
                var lines = File.ReadAllLines(mainResourceCfg).ToList();
                // Remove any old scan lines that point to our config, whether absolute or relative
                lines.RemoveAll(l => l.StartsWith("Scan", StringComparison.OrdinalIgnoreCase) && l.Contains("Cache/Config", StringComparison.OrdinalIgnoreCase));

                // Add the clean relative scan line to the top
                lines.Insert(0, scanLine);
                File.WriteAllLines(mainResourceCfg, lines);
            }
            else
            {
                using (StreamWriter sw = new StreamWriter(mainResourceCfg, false))
                {
                    sw.WriteLine(scanLine);
                    sw.WriteLine("Priority 500");
                    sw.WriteLine("PackedFile Packages/*.package");
                    sw.WriteLine("PackedFile Packages/*/*.package");
                    sw.WriteLine("PackedFile Packages/*/*/*.package");
                    sw.WriteLine("PackedFile Packages/*/*/*/*.package");
                    sw.WriteLine("PackedFile Packages/*/*/*/*/*.package");
                }
            }

            onProgress?.Invoke("Successfully synced to The Sims 3.");
        }
        catch (Exception ex)
        {
            onProgress?.Invoke("Error syncing to The Sims 3: " + ex.Message);
        }
    }

    private void CreateFolders()
    {
        Directory.CreateDirectory(_options.DocumentBaseDir);
        Directory.CreateDirectory(_options.DownloadFolderPath);
        Directory.CreateDirectory(_options.ArchiveFolderPath);
        Directory.CreateDirectory(_options.TS3PackFolderPath);
        Directory.CreateDirectory(_options.ManagedPackageFolderPath);
        Directory.CreateDirectory(_options.SetCacheFolderPath);
        Directory.CreateDirectory(_options.LegacyPackageFolderPath);
        Directory.CreateDirectory(_options.TS3PackStoreFolderPath);
        Directory.CreateDirectory(Path.Combine(_options.DocumentBaseDir, "Thumbnails"));
    }

    private async Task CheckOrphanPackagesAsync()
    {
        var existingFiles = await _db.MetaEntities.Select(m => m.FileName).ToListAsync();

        if (!Directory.Exists(_options.ManagedPackageFolderPath))
            return;

        var defaultSet = await _db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
        if (defaultSet == null)
        {
            defaultSet = new SetsEntity { Name = "Default", FolderName = "Default", IsLegacy = false, Dirty = true, LongName = "Default", IsExpanded = true, IsDefault = true };
            _db.SetsEntities.Add(defaultSet);
        }

        string[] packageFiles = Directory.GetFiles(_options.ManagedPackageFolderPath, "*.package");
        string[] sims3packFiles = Directory.GetFiles(_options.ManagedPackageFolderPath, "*.sims3pack");
        string[] files = packageFiles.Concat(sims3packFiles).ToArray();

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            if (!existingFiles.Contains(fileName))
            {
                bool isSims3Pack = Path.GetExtension(fileName).Equals(".sims3pack", StringComparison.OrdinalIgnoreCase);

                var typeInfo = DetectPackageType(filePath, isSims3Pack);

                var meta = new MetaEntity
                {
                    FileName = fileName,
                    FileType = isSims3Pack ? "TS3PACK" : "DBPF",
                    PackageType = typeInfo.PackageType,
                    CASCategories = typeInfo.CASCategories,
                    CompleteFileName = filePath,
                    SetsEntity = defaultSet,
                    InstallDate = DateTime.Now.ToString(),
                    Manifest = string.Empty,
                    Enabled = true,
                    FileSize = new FileInfo(filePath).Length / 1024.0
                };

                defaultSet.Dirty = true;
                _db.MetaEntities.Add(meta);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task CheckSetParentingAsync()
    {
        var defaultSet = await _db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");

        var allSets = await _db.SetsEntities.ToListAsync();
        foreach (var set in allSets)
        {

            if (string.IsNullOrEmpty(set.Name))
            {
                set.Name = "Recovered Set";
            }

            if (string.IsNullOrWhiteSpace(set.FolderName))
            {
                var invalidChars = Path.GetInvalidFileNameChars();
                set.FolderName = new string(set.Name.Where(c => !invalidChars.Contains(c)).ToArray());
                if (string.IsNullOrWhiteSpace(set.FolderName)) set.FolderName = $"Set_{set.Id}";
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task RebuildCacheAsync(bool forceRebuild = false, Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
    {
        var allSets = await _db.SetsEntities.Include(s => s.MetaEntities).ToListAsync();
        int total = allSets.Count;
        int current = 0;

        foreach (var set in allSets)
        {
            current++;
            if (forceRebuild || set.Dirty)
            {
                onProgress?.Invoke($"Rebuilding cache for set: {set.Name} ({current}/{total})...");
                RebuildSet(set, onProgress, skippedFiles);
            }
        }
    }

    private string GetSetFolderName(SetsEntity activeSet)
    {
        string folderName = string.IsNullOrWhiteSpace(activeSet.FolderName) ? activeSet.Name : activeSet.FolderName;
        var invalidChars = Path.GetInvalidFileNameChars();
        folderName = new string(folderName.Where(c => !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(folderName)) folderName = $"Set_{activeSet.Id}";
        return folderName;
    }

    private string GetSetPath(SetsEntity activeSet)
    {
        if (activeSet == null) throw new ArgumentException("ActiveSet cannot be null");

        string folderName = GetSetFolderName(activeSet);

        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (eaDir != null)
        {
            return Path.Combine(eaDir, "The Sims 3", "Mods", "Cache", folderName);
        }

        // Fallback
        return Path.Combine(_options.SetCacheFolderPath, "Sets", folderName);
    }

    public string GetSetCachePath(string folderName)
    {
        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (!string.IsNullOrEmpty(eaDir))
        {
            return Path.Combine(eaDir, "The Sims 3", "Mods", "Cache", folderName);
        }
        return Path.Combine(_options.SetCacheFolderPath, "Sets", folderName);
    }

    private void RebuildSet(SetsEntity activeSet, Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
    {
        if (activeSet.IsLegacy) return;

        string setPath = GetSetPath(activeSet);
        Directory.CreateDirectory(setPath);

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
                onProgress?.Invoke($"Merging item {currentItem} of {totalItems} in '{activeSet.Name}'...");
            }
            if (!item.Enabled) continue;

            if (!File.Exists(item.CompleteFileName))
            {
                skippedFiles?.Add((item.FileName, "File not found on disk"));
                onProgress?.Invoke($"Skipping missing file '{item.FileName}'");
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
                    onProgress?.Invoke($"Skipping unreadable file '{item.FileName}': {ex.Message}");
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
                            onProgress?.Invoke($"Skipping invalid package '{item.FileName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        skippedFiles?.Add((item.FileName, ex.Message));
                        onProgress?.Invoke($"Error processing package '{item.FileName}': {ex.Message}");
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
                                onProgress?.Invoke($"Skipping invalid package in Sims3Pack '{item.FileName}'");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    skippedFiles?.Add((item.FileName, ex.Message));
                    onProgress?.Invoke($"Skipping unreadable Sims3Pack '{item.FileName}': {ex.Message}");
                }
            }
        }

        if (outputPkg != null)
        {
            try { outputPkg.Close(); } catch { }
            outputPkg = null;
        }

        // Clean up old rebuilds
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

        // Save non-package items for syncing
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

    private string GetTS3FolderPath(string folderName, string fileName)
    {
        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (eaDir != null)
        {
            return Path.Combine(eaDir, "The Sims 3", folderName, fileName);
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

    private string GetWorldName(DBPFPackage package, string name)
    {
        var resource = package.Resources.FirstOrDefault(r => r.Key.Type == 3653044489u);
        if (resource == null) return name;
        using var reader = new BinaryReader(resource.GetStream());
        int length = (int)reader.ReadUInt32();
        byte[] bytes = reader.ReadBytes(length * 2);
        string decoded = System.Text.Encoding.Unicode.GetString(bytes);
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            decoded = decoded.Replace(c, '_');
        }
        return decoded.Replace('/', '_').Replace('\\', '_');
    }

    private bool ValidatePackage(DBPFPackage package)
    {
        // Add basic validation to match the decompiled code's crash checks
        var dollDressedKey = new TGI_Key(832458525u, 0u, 4064452635095512314uL);
        foreach (var resource in package.Resources)
        {
            if (resource.Key.Type == dollDressedKey.Type && resource.Key.Group == dollDressedKey.Group && resource.Key.Instance == dollDressedKey.Instance)
            {
                return false; // Doll Dressed corrupt file
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
            if (resource.Key.Type == 3571055589u) // PTRN
            {
                FixPTRN(resource);
            }
            else if (resource.Key.Type == 53690476) // _XML
            {
                FixPTRN_XML(resource);
            }

            if (ValidateResource(resource.Key) && addedTgis.Add(resource.Key))
            {
                validResources.Add(resource);
            }
        }

        // Compress resources in parallel if enabled, but leave 1 core free so the PC remains responsive
        if (_options.CompressionLevel > 0)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.ForEach(validResources, parallelOptions, resource =>
            {
                resource.Compress(_options.CompressionLevel);
            });
        }

        // Add to package sequentially since AddResource is not thread-safe
        foreach (var resource in validResources)
        {
            if (outputPkg == null)
            {
                string path = Path.Combine(GetSetPath(activeSet), $"ModBUILD{packageCount++}.new");
                outputPkg = new DBPFPackageBuilder(path);
            }

            outputPkg.AddResource(resource);

            if (outputPkg.PackageSize >= 1073741824) // 1GB limit
            {
                outputPkg.Close();
                outputPkg = null;
            }
        }
    }

    private void FixPTRN(ResourceEntry resource)
    {
        XmlDocument xmlDocument = new XmlDocument();
        try { xmlDocument.Load(resource.GetStream()); } catch (XmlException) { return; }
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
        try { xmlDocument.Load(resource.GetStream()); } catch (XmlException) { return; }
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

    public async Task<string?> GetThumbnailPathAsync(long itemId)
    {
        string thumbDir = Path.Combine(_options.DocumentBaseDir, "Thumbnails");
        Directory.CreateDirectory(thumbDir);
        string thumbPath = Path.Combine(thumbDir, $"{itemId}.thumb");

        if (File.Exists(thumbPath))
        {
            return thumbPath;
        }

        var item = await _db.MetaEntities.FindAsync(itemId);
        if (item == null || !File.Exists(item.CompleteFileName)) return null;

        try
        {
            if (item.FileName.ToLower().EndsWith(".sims3pack"))
            {
                using var sims3Pack = new Sims3Pack(item.CompleteFileName);
                if (sims3Pack.Thumbnails != null && sims3Pack.Thumbnails.Count > 0)
                {
                    using var firstThumb = sims3Pack.Thumbnails[0];
                    using var fs = new FileStream(thumbPath, FileMode.Create, FileAccess.Write);
                    await firstThumb.CopyToAsync(fs);
                    return thumbPath;
                }
                else if (sims3Pack.Thumbnail != null)
                {
                    using var thumb = sims3Pack.Thumbnail;
                    using var fs = new FileStream(thumbPath, FileMode.Create, FileAccess.Write);
                    await thumb.CopyToAsync(fs);
                    return thumbPath;
                }
            }
            else
            {
                using var package = new DBPFPackage(item.CompleteFileName);
                var validThumbTypes = new uint[] {
                    0x626F60CC, 0x626F60CD, 0x626F60CE, // Custom thumbnails (highest priority)
                    0x2E75C765, 0x2E75C764, 0x2E75C766, // Auto-generated CAS / Object thumbnails
                    0x0B202AD9, // THUM
                    0x0580A2B4, 0x0580A2B5, 0x0580A2B6 // Other UI thumbnails
                };

                var res = validThumbTypes
                    .Select(typeId => package.Resources.FirstOrDefault(r => r.Key.Type == typeId))
                    .FirstOrDefault(r => r != null);

                if (res != null)
                {
                    var bytes = res.Read();
                    await File.WriteAllBytesAsync(thumbPath, bytes);
                    return thumbPath;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Thumbnails] Error extracting from {item.CompleteFileName}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        return null;
    }

    public (string PackageType, string CASCategories) DetectPackageType(string filePath, bool isSims3Pack)
    {
        try
        {
            var packages = new List<S3ForgeTools.GameFiles.Package.DBPFPackage>();
            S3ForgeTools.GameFiles.TS3Pack.Sims3Pack? s3p = null;

            if (isSims3Pack)
            {
                s3p = new S3ForgeTools.GameFiles.TS3Pack.Sims3Pack(filePath);
                packages.AddRange(s3p.Packages);
            }
            else
            {
                packages.Add(new S3ForgeTools.GameFiles.Package.DBPFPackage(filePath));
            }

            var categories = new HashSet<string>();
            bool isBuildBuy = false;
            bool hasCaspOverall = false;
            bool isWorld = false;

            foreach (var package in packages)
            {
                // Check if this package is a World
                if (package.Resources.Any(r => r.Key.Type == 107542056))
                {
                    isWorld = true;
                    break;
                }

                var caspList = package.Resources.Where(r => r.Key.Type == 0x034AEECB).ToList();
                if (caspList.Any())
                {
                    hasCaspOverall = true;
                    foreach (var caspEntry in caspList)
                    {
                        try
                        {
                            var data = caspEntry.Read();
                            var casp = new S3ForgeTools.GameFiles.Resources.ResourceCASP(data);
                            switch (casp.ClothingType)
                            {
                                case 0x1: categories.Add("Hair"); break;
                                case 0x4: categories.Add("Full body"); break;
                                case 0x5: categories.Add("Tops"); break;
                                case 0x6: categories.Add("Bottoms"); break;
                                case 0x7: categories.Add("Shoes"); break;
                                case 0x8: case 0x9: case 0xA: case 0xB: case 0xC: case 0xD: case 0xE: case 0xF:
                                    categories.Add("Accessories"); break;
                                case 0x10: case 0x11: case 0x12: case 0x13: case 0x14: case 0x15: case 0x16: case 0x17:
                                    categories.Add("Details"); break;
                                default: categories.Add("Other"); break;
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    // Check for BuildBuy (OBJD) first — many of the CAS-like resource types
                    // (0x73E93EEB, 0x220557DA, 0x00B2D882) also appear in build-buy objects
                    if (package.Resources.Any(r => r.Key.Type == 0x319E4F1D))
                    {
                        isBuildBuy = true;
                    }
                    else
                    {
                        // Only check for Sliders, Presets, Skintones if no CASP and no OBJD.
                        // Note: 0x0166038C appears in sliders too (as a reference), so sliders
                        // must be checked first — if Face Modifier or Blend Geometry is present,
                        // it's a slider regardless of whether 0x0166038C is also present.
                        bool hasFaceModifier = package.Resources.Any(r => r.Key.Type == 0x0358B08A);
                        bool hasBlendGeom = package.Resources.Any(r => r.Key.Type == 0x0355E0A6);
                        bool hasTone = package.Resources.Any(r => r.Key.Type == 0x0166038C);
                        bool hasPreset1 = package.Resources.Any(r => r.Key.Type == 0x051DF2DD);

                        if (hasFaceModifier || hasBlendGeom) categories.Add("Sliders");
                        else if (hasTone) categories.Add("Skins");
                        else if (hasPreset1) categories.Add("Presets");
                    }
                }
            }

            s3p?.Dispose();

            foreach (var package in packages)
            {
                if (!isSims3Pack) package.Dispose();
            }

            if (isWorld) return ("Other", "");

            // Remove Slider and Presets if CASP items were found (to prevent hairs/accessories from being double-tagged)
            if (hasCaspOverall)
            {
                categories.Remove("Sliders");
                categories.Remove("Presets");
                categories.Remove("Skins");
            }

            if (isBuildBuy) return ("BuildBuy", "");
            if (categories.Any())
            {
                string joined = string.Join(",", categories);
                return ("CAS", joined);
            }
        }
        catch { /* ignore parsing errors */ }

        return ("Other", "");
    }

    public async Task RecheckPackageTypesAsync(Action<string>? onProgress = null)
    {
        var items = await _db.MetaEntities.ToListAsync();
        int count = 0;
        int total = items.Count;
        int updatedCount = 0;

        foreach (var item in items)
        {
            count++;
            if (count % 25 == 0)
            {
                onProgress?.Invoke($"Rechecking items... {count}/{total}");
            }

            bool isSims3Pack = item.FileType == "TS3PACK";

            // Fix potentially outdated relative CompleteFileName paths from old DB entries
            string expectedPath = Path.Combine(_options.ManagedPackageFolderPath, item.FileName);
            if (item.CompleteFileName != expectedPath)
            {
                item.CompleteFileName = expectedPath;
                updatedCount++;
            }

            // Always re-evaluate CAS and Other items to apply new improved logic
            if (string.IsNullOrEmpty(item.PackageType) || item.PackageType == "Other" || item.PackageType == "CAS")
            {
                if (File.Exists(item.CompleteFileName))
                {
                    var typeInfo = DetectPackageType(item.CompleteFileName, isSims3Pack);

                    if (item.PackageType != typeInfo.PackageType || item.CASCategories != typeInfo.CASCategories)
                    {
                        item.PackageType = typeInfo.PackageType;
                        item.CASCategories = typeInfo.CASCategories;
                        updatedCount++;
                    }
                    else if (string.IsNullOrEmpty(item.PackageType))
                    {
                        item.PackageType = typeInfo.PackageType;
                        updatedCount++;
                    }
                }
            }
        }

        if (updatedCount > 0)
        {
            onProgress?.Invoke($"Saving {updatedCount} updated items to database...");
            await _db.SaveChangesAsync();
        }

        onProgress?.Invoke($"Finished. Updated {updatedCount} items.");
    }
}
