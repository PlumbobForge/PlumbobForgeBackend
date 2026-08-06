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
using SharpCompress.Archives;
using SharpCompress.Common;

namespace PlumbobForge.Backend.Services;

public class PKGManager
{
    private readonly AppDbContext _db;
    private readonly PlumbobForgeOptions _options;
    private readonly LocalizationService _localizer;

    public PKGManager(AppDbContext db, IOptionsSnapshot<PlumbobForgeOptions> options, LocalizationService localizer)
    {
        _db = db;
        _options = options.Value;
        _localizer = localizer;
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

                onProgress?.Invoke(_localizer.GetString("checking_orphan_packages"));
                await CheckOrphanPackagesAsync();
                onProgress?.Invoke(_localizer.GetString("validating_set_hierarchy"));
                await CheckSetParentingAsync();
                onProgress?.Invoke(_localizer.GetString("initializing_config_profile"));
                await CheckConfigurationsAsync();
                onProgress?.Invoke(_localizer.GetString("scanning_cache_requirements"));
                bool isStatic = string.Equals(_options.CacheMethod, "Static", StringComparison.OrdinalIgnoreCase);
                await RebuildCacheAsync(isStatic, onProgress, skippedFiles);
                await SyncToSims3Async(onProgress, forceRebuildStatic: false);

                if (skippedFiles.Count > 0)
                {
                    onProgress?.Invoke(_localizer.GetString("rebuild_partially_completed", skippedFiles.Count));
                    foreach (var (fileName, reason) in skippedFiles)
                    {
                        onProgress?.Invoke($"  - {fileName}: {reason}");
                    }
                }
                else
                {
                    onProgress?.Invoke(_localizer.GetString("scan_complete"));
                }
            }
            catch (Exception ex)
            {
                onProgress?.Invoke(_localizer.GetString("rebuild_error", ex.Message));
            }
        }
    }

    public static bool IsArchiveExtension(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".zip" || ext == ".rar" || ext == ".7z";
    }

    public static List<string> GetArchivePackageFileNames(string archivePath)
    {
        var names = new List<string>();
        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory && !string.IsNullOrEmpty(entry.Key))
                {
                    string ext = Path.GetExtension(entry.Key).ToLowerInvariant();
                    if (ext == ".package" || ext == ".sims3pack")
                    {
                        string? fn = Path.GetFileName(entry.Key);
                        if (!string.IsNullOrEmpty(fn)) names.Add(fn);
                    }
                }
            }
        }
        catch { }
        return names;
    }

    public static List<string> GetArchivePackageFileNames(Stream stream)
    {
        var names = new List<string>();
        try
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory && !string.IsNullOrEmpty(entry.Key))
                {
                    string ext = Path.GetExtension(entry.Key).ToLowerInvariant();
                    if (ext == ".package" || ext == ".sims3pack")
                    {
                        string? fn = Path.GetFileName(entry.Key);
                        if (!string.IsNullOrEmpty(fn)) names.Add(fn);
                    }
                }
            }
        }
        catch { }
        return names;
    }

    public List<string> CheckFormDuplicates(Microsoft.AspNetCore.Http.IFormFileCollection files)
    {
        var targetFileNames = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            string fileName = Path.GetFileName(file.FileName);
            if (IsArchiveExtension(fileName))
            {
                try
                {
                    using var stream = file.OpenReadStream();
                    targetFileNames.AddRange(GetArchivePackageFileNames(stream));
                }
                catch { }
            }
            else
            {
                if (!string.IsNullOrEmpty(fileName)) targetFileNames.Add(fileName);
            }
        }

        return GetDuplicateFiles(targetFileNames);
    }

    public List<string> ExtractPackagesFromArchive(string archivePath, string destinationFolder, string duplicateAction = "rename", Action<string>? onProgress = null)
    {
        var extractedPaths = new List<string>();
        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key)) continue;

                string ext = Path.GetExtension(entry.Key).ToLowerInvariant();
                if (ext != ".package" && ext != ".sims3pack") continue;

                string? fileName = Path.GetFileName(entry.Key);
                if (string.IsNullOrEmpty(fileName)) continue;

                string destPath = Path.Combine(destinationFolder, fileName);
                if (File.Exists(destPath))
                {
                    if (string.Equals(duplicateAction, "skip", StringComparison.OrdinalIgnoreCase))
                    {
                        onProgress?.Invoke($"Skipped duplicate file in archive: {fileName}");
                        continue;
                    }
                    else if (string.Equals(duplicateAction, "replace", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(destPath); } catch { }
                    }
                    else
                    {
                        destPath = Path.Combine(destinationFolder, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                    }
                }

                entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
                extractedPaths.Add(destPath);
                onProgress?.Invoke($"Extracted from archive: {fileName}");
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"Failed to extract archive {Path.GetFileName(archivePath)}: {ex.Message}");
        }
        return extractedPaths;
    }

    public List<string> GetDuplicateFiles(IEnumerable<string> fileNames)
    {
        var duplicates = new List<string>();
        if (!Directory.Exists(_options.ManagedPackageFolderPath)) return duplicates;

        var existingInDb = _db.MetaEntities.Select(m => m.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in fileNames)
        {
            string fileName = Path.GetFileName(name);
            if (string.IsNullOrEmpty(fileName)) continue;

            string dest = Path.Combine(_options.ManagedPackageFolderPath, fileName);
            if (File.Exists(dest) || existingInDb.Contains(fileName))
            {
                duplicates.Add(fileName);
            }
        }

        return duplicates;
    }

    public async Task UploadFilesAsync(Microsoft.AspNetCore.Http.IFormFileCollection files, Action<string>? onProgress = null, string duplicateAction = "rename", long? targetSetId = null)
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

                if (IsArchiveExtension(fileName))
                {
                    string tempPath = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString("N") + "_" + fileName);
                    try
                    {
                        using (var stream = new FileStream(tempPath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        var extracted = ExtractPackagesFromArchive(tempPath, _options.ManagedPackageFolderPath, duplicateAction, onProgress);
                        movedCount += extracted.Count;
                    }
                    finally
                    {
                        try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    }
                }
                else
                {
                    string dest = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                    try
                    {
                        bool exists = File.Exists(dest);
                        if (exists)
                        {
                            if (string.Equals(duplicateAction, "skip", StringComparison.OrdinalIgnoreCase))
                            {
                                onProgress?.Invoke($"Skipped duplicate file: {fileName}");
                                continue;
                            }
                            else if (string.Equals(duplicateAction, "replace", StringComparison.OrdinalIgnoreCase))
                            {
                                // Overwrite existing
                            }
                            else
                            {
                                dest = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                            }
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
            }

            if (movedCount > 0)
            {
                onProgress?.Invoke($"Imported {movedCount} files to Library. Registering in database...");
                await CheckOrphanPackagesAsync(targetSetId);
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

    public async Task ImportFilesAsync(string[] files, Action<string>? onProgress = null, string duplicateAction = "rename", long? targetSetId = null)
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

                if (IsArchiveExtension(file))
                {
                    onProgress?.Invoke($"Extracting archive: {Path.GetFileName(file)}");
                    var extracted = ExtractPackagesFromArchive(file, _options.ManagedPackageFolderPath, duplicateAction, onProgress);
                    movedCount += extracted.Count;
                }
                else
                {
                    string fileName = Path.GetFileName(file);
                    string dest = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                    try
                    {
                        bool exists = File.Exists(dest);
                        if (exists)
                        {
                            if (string.Equals(duplicateAction, "skip", StringComparison.OrdinalIgnoreCase))
                            {
                                onProgress?.Invoke($"Skipped duplicate file: {fileName}");
                                continue;
                            }
                            else if (string.Equals(duplicateAction, "replace", StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(file, dest, true);
                                movedCount++;
                                continue;
                            }
                            else
                            {
                                dest = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                            }
                        }

                        File.Copy(file, dest);
                        movedCount++;
                    }
                    catch (Exception ex)
                    {
                        onProgress?.Invoke($"Failed to import {fileName}: {ex.Message}");
                    }
                }
            }

            if (movedCount > 0)
            {
                onProgress?.Invoke($"Imported {movedCount} files to Library. Registering in database...");
                await CheckOrphanPackagesAsync(targetSetId);
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

    public string GetDownloadsFolderPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.DocumentBaseDir))
        {
            string configured = Path.Combine(_options.DocumentBaseDir, "Downloads");
            if (Directory.Exists(configured)) return configured;
            string ts3Configured = Path.Combine(_options.DocumentBaseDir, "The Sims 3", "Downloads");
            if (Directory.Exists(ts3Configured)) return ts3Configured;
        }

        string defaultEa = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts", "The Sims 3", "Downloads");
        return defaultEa;
    }

    public List<string> GetObservedFolders()
    {
        var list = new List<string>();
        if (_options.ObservedFolders != null && _options.ObservedFolders.Count > 0)
        {
            foreach (var folder in _options.ObservedFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder) && !list.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(folder);
                }
            }
        }

        if (list.Count == 0)
        {
            string defaultDir = GetDownloadsFolderPath();
            if (!string.IsNullOrEmpty(defaultDir)) list.Add(defaultDir);
        }

        return list;
    }

    public List<string> CheckDownloadsDuplicates()
    {
        var observedFolders = GetObservedFolders();
        var allFiles = new List<string>();

        foreach (var dir in observedFolders)
        {
            if (Directory.Exists(dir))
            {
                allFiles.AddRange(Directory.GetFiles(dir, "*.*")
                    .Where(f => {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext == ".package" || ext == ".sims3pack" || ext == ".zip" || ext == ".rar" || ext == ".7z";
                    }));
            }
        }

        if (allFiles.Count == 0) return new List<string>();

        var targetFileNames = new List<string>();
        foreach (var file in allFiles)
        {
            if (IsArchiveExtension(file))
            {
                targetFileNames.AddRange(GetArchivePackageFileNames(file));
            }
            else
            {
                string fn = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(fn)) targetFileNames.Add(fn);
            }
        }

        return GetDuplicateFiles(targetFileNames);
    }

    public async Task<int> ImportFromDownloadsAsync(Action<string>? onProgress = null, string duplicateAction = "rename")
    {
        try
        {
            var observedFolders = GetObservedFolders();
            var filesList = new List<string>();

            foreach (var dir in observedFolders)
            {
                if (Directory.Exists(dir))
                {
                    filesList.AddRange(Directory.GetFiles(dir, "*.*")
                        .Where(f => {
                            string ext = Path.GetExtension(f).ToLowerInvariant();
                            return ext == ".package" || ext == ".sims3pack" || ext == ".zip" || ext == ".rar" || ext == ".7z";
                        }));
                }
            }

            var files = filesList.ToArray();
            if (files.Length == 0)
            {
                onProgress?.Invoke(_localizer.GetString("import_no_files_found"));
                return 0;
            }

            if (!Directory.Exists(_options.ManagedPackageFolderPath))
            {
                Directory.CreateDirectory(_options.ManagedPackageFolderPath);
            }
            int movedCount = 0;

            foreach (var file in files)
            {
                if (IsArchiveExtension(file))
                {
                    onProgress?.Invoke($"Extracting archive: {Path.GetFileName(file)}");
                    var extracted = ExtractPackagesFromArchive(file, _options.ManagedPackageFolderPath, duplicateAction, onProgress);
                    movedCount += extracted.Count;
                    try { File.Delete(file); } catch { }
                }
                else
                {
                    string fileName = Path.GetFileName(file);
                    string dest = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                    try
                    {
                        if (File.Exists(dest))
                        {
                            if (duplicateAction == "skip")
                            {
                                onProgress?.Invoke($"Skipped duplicate: {fileName}");
                                continue;
                            }
                            else if (duplicateAction == "replace")
                            {
                                try { File.Delete(dest); } catch { }
                            }
                            else // "rename"
                            {
                                dest = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                            }
                        }
                        File.Move(file, dest);
                        movedCount++;
                    }
                    catch (Exception ex)
                    {
                        onProgress?.Invoke(_localizer.GetString("import_failed_move", fileName, ex.Message));
                    }
                }
            }

            if (movedCount > 0)
            {
                onProgress?.Invoke(_localizer.GetString("import_moved_files", movedCount));
                await CheckOrphanPackagesAsync();
                onProgress?.Invoke(_localizer.GetString("import_complete"));
            }
            else
            {
                onProgress?.Invoke(_localizer.GetString("import_no_files_moved"));
            }

            return movedCount;
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"CRASH: {ex.Message} {ex.StackTrace}");
            return 0;
        }
    }

    public async Task AutoFixAsync(Action<string>? onProgress = null)
    {
        onProgress?.Invoke(_localizer.GetString("autofix_starting"));

        // 1. Ensure Default Set exists
        var defaultSet = await _db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
        if (defaultSet == null)
        {
            defaultSet = new SetsEntity { Name = "Default", FolderName = "Default", IsLegacy = false, Dirty = true, LongName = "Default", IsExpanded = true, IsDefault = true };
            _db.SetsEntities.Add(defaultSet);
            await _db.SaveChangesAsync();
        }

        // 2. Fix Orphaned Items
        onProgress?.Invoke(_localizer.GetString("autofix_checking_missing_sets"));
        var orphans = await _db.MetaEntities.Where(m => m.SetsEntityId == null).ToListAsync();
        if (orphans.Any())
        {
            onProgress?.Invoke(_localizer.GetString("autofix_found_unassigned", orphans.Count));
            foreach (var item in orphans)
            {
                item.SetsEntityId = defaultSet.Id;
            }
            defaultSet.Dirty = true;
        }

        // 3. Mark ALL Sets as Dirty for a full rebuild
        onProgress?.Invoke(_localizer.GetString("autofix_marking_all_sets"));
        var allSets = await _db.SetsEntities.ToListAsync();
        foreach (var set in allSets)
        {
            set.Dirty = true;
        }
        await _db.SaveChangesAsync();

        // 4. Force a complete rebuild
        onProgress?.Invoke(_localizer.GetString("autofix_delegating_rebuild"));
        await RunAsync(true, onProgress);

        onProgress?.Invoke(_localizer.GetString("autofix_complete"));
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

    public async Task SyncToSims3Async(Action<string>? onProgress = null, bool forceRebuildStatic = false)
    {
        try
        {
            onProgress?.Invoke(_localizer.GetString("syncing_cache_to_sims3"));

            string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
            if (eaDir == null) return;

            string sims3ModsDir = Path.Combine(eaDir, "The Sims 3", "Mods");
            string sims3CacheDir = Path.Combine(sims3ModsDir, "Cache");
            string sims3ConfigDir = Path.Combine(sims3CacheDir, "Config");
            string staticCacheDir = Path.Combine(sims3CacheDir, "StaticCache");

            Directory.CreateDirectory(sims3ModsDir);
            Directory.CreateDirectory(sims3CacheDir);
            Directory.CreateDirectory(sims3ConfigDir);

            bool isStatic = string.Equals(_options.CacheMethod, "Static", StringComparison.OrdinalIgnoreCase);
            if (isStatic)
            {
                string configResourceCfgStatic = Path.Combine(sims3ConfigDir, "Resource.cfg");
                using (StreamWriter sw = new StreamWriter(configResourceCfgStatic, false))
                {
                    sw.WriteLine("Priority 500");
                    sw.WriteLine(@"PackedFile ../StaticCache/*.package");
                    sw.WriteLine(@"PackedFile ../StaticCache/*/*.package");
                }

                bool staticCacheExists = Directory.Exists(staticCacheDir) && Directory.GetFiles(staticCacheDir, "*.package").Length > 0;
                if (forceRebuildStatic || !staticCacheExists)
                {
                    await RebuildStaticCacheAsync(onProgress);
                }
                return;
            }

            // Clean up old orphaned cache sets (any folder that no longer exists in DB)
            var allSetFolderNames = _db.SetsEntities.Select(s => s.FolderName).ToList();
            allSetFolderNames.Add("Config"); // Always keep Config
            allSetFolderNames.Add("StaticCache");

            foreach (var dir in Directory.GetDirectories(sims3CacheDir))
            {
                string dirName = Path.GetFileName(dir);
                if (!allSetFolderNames.Contains(dirName))
                {
                    Directory.Delete(dir, true);
                }
            }

            // 1. Gather all files that SHOULD be synced for the active configuration
            var activeConfig = await _db.ConfigEntities
                .Include(c => c.ConfigSetsEntities)
                .ThenInclude(cs => cs.SetsEntity)
                .FirstOrDefaultAsync(c => c.Active);

            // Dictionary: destPath -> sourceItemPath
            var targetFilesToSync = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 2. Make a Resource.cfg in Electronic Arts\The Sims 3\Mods\Cache\Config
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
                            sw.WriteLine($@"PackedFile ../{folderName}/*.package");
                            sw.WriteLine($@"PackedFile ../{folderName}/*/*.package");

                            // Collect Non-Package items for this active set
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
                                            targetFilesToSync[destPath] = itemPath;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 3. Read previously synced files list
            string syncRecordFile = Path.Combine(_options.SetCacheFolderPath, "SyncedItems.txt");
            var previouslySyncedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(syncRecordFile))
            {
                var oldLines = File.ReadAllLines(syncRecordFile);
                foreach (var line in oldLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string clean = line.Trim();
                        if (clean.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            clean = clean.Substring(0, clean.Length - 9);
                        }
                        previouslySyncedFiles.Add(clean);
                    }
                }
            }

            // Union previously synced files with targetFilesToSync keys so we manage all known paths
            var allManagedPaths = new HashSet<string>(previouslySyncedFiles, StringComparer.OrdinalIgnoreCase);
            foreach (var path in targetFilesToSync.Keys)
            {
                allManagedPaths.Add(path);
            }

            foreach (var destPath in allManagedPaths)
            {
                bool isEnabledInActiveConfig = targetFilesToSync.TryGetValue(destPath, out string? sourcePath);
                string disabledPath = destPath + ".disabled";

                if (isEnabledInActiveConfig && sourcePath != null)
                {
                    // Should be ENABLED in TS3
                    try
                    {
                        // If it currently exists as .disabled, rename it back to enabled!
                        if (File.Exists(disabledPath))
                        {
                            if (File.Exists(destPath))
                            {
                                try { File.Delete(destPath); } catch { }
                            }
                            File.Move(disabledPath, destPath);
                        }

                        // Verify if file exists and matches source file
                        var sourceInfo = new FileInfo(sourcePath);
                        bool needCopy = true;

                        if (File.Exists(destPath))
                        {
                            var destInfo = new FileInfo(destPath);
                            if (destInfo.Length == sourceInfo.Length && Math.Abs((destInfo.LastWriteTimeUtc - sourceInfo.LastWriteTimeUtc).TotalSeconds) < 2)
                            {
                                needCopy = false;
                            }
                        }

                        if (needCopy)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                            File.Copy(sourcePath, destPath, true);
                            File.SetLastWriteTimeUtc(destPath, sourceInfo.LastWriteTimeUtc);
                        }
                    }
                    catch (Exception ex)
                    {
                        onProgress?.Invoke($"Warning: Failed to enable synced item '{Path.GetFileName(destPath)}': {ex.Message}");
                    }
                }
                else
                {
                    // Should be DISABLED in TS3: rename destPath to destPath + .disabled
                    try
                    {
                        if (File.Exists(destPath))
                        {
                            if (File.Exists(disabledPath))
                            {
                                try { File.Delete(disabledPath); } catch { }
                            }
                            File.Move(destPath, disabledPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        onProgress?.Invoke($"Warning: Failed to disable synced item '{Path.GetFileName(destPath)}': {ex.Message}");
                    }
                }
            }

            Directory.CreateDirectory(_options.SetCacheFolderPath);
            File.WriteAllLines(syncRecordFile, allManagedPaths);

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

            onProgress?.Invoke(_localizer.GetString("sync_success"));
        }
        catch (Exception ex)
        {
            onProgress?.Invoke(_localizer.GetString("sync_error", ex.Message));
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

    private async Task CheckOrphanPackagesAsync(long? targetSetId = null)
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

        SetsEntity assignSet = defaultSet;
        if (targetSetId.HasValue && targetSetId.Value > 0)
        {
            var foundSet = await _db.SetsEntities.FindAsync(targetSetId.Value);
            if (foundSet != null) assignSet = foundSet;
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
                    CASAge = typeInfo.CASAge,
                    CASGender = typeInfo.CASGender,
                    CASOutfitCategory = typeInfo.CASOutfitCategory,
                    CompleteFileName = filePath,
                    SetsEntity = assignSet,
                    InstallDate = DateTime.Now.ToString(),
                    Manifest = string.Empty,
                    Enabled = true,
                    FileSize = new FileInfo(filePath).Length / 1024.0
                };

                assignSet.Dirty = true;
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
        bool isStatic = string.Equals(_options.CacheMethod, "Static", StringComparison.OrdinalIgnoreCase);
        if (isStatic)
        {
            await RebuildStaticCacheAsync(onProgress, skippedFiles);
            return;
        }

        // Clean up static cache directory if switching back to Dynamic mode
        try
        {
            string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
            if (!string.IsNullOrEmpty(eaDir))
            {
                string staticDir = Path.Combine(eaDir, "The Sims 3", "Mods", "Cache", "StaticCache");
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

    private async Task RebuildStaticCacheAsync(Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
    {
        string eaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts");
        if (string.IsNullOrEmpty(eaDir)) return;

        string staticCachePath = Path.Combine(eaDir, "The Sims 3", "Mods", "Cache", "StaticCache");
        Directory.CreateDirectory(staticCachePath);

        foreach (var file in Directory.GetFiles(staticCachePath, "*.package"))
        {
            try { File.Delete(file); } catch { }
        }

        var allSetFolders = await _db.SetsEntities.Select(s => s.FolderName).ToListAsync();
        string sims3CacheDir = Path.Combine(eaDir, "The Sims 3", "Mods", "Cache");
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

    private void RebuildPackageStatic(ref DBPFPackageBuilder? outputPkg, ref int packageCount, string staticCachePath, DBPFPackage inputPkg, HashSet<TGI_Key> addedTgis)
    {
        var validResources = new List<ResourceEntry>();

        foreach (var resource in inputPkg.Resources)
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

        if (_options.CompressionLevel > 0)
        {
            var failedResources = new System.Collections.Concurrent.ConcurrentBag<ResourceEntry>();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.ForEach(validResources, parallelOptions, resource =>
            {
                try
                {
                    resource.Compress(_options.CompressionLevel);
                }
                catch (Exception)
                {
                    failedResources.Add(resource);
                }
            });
            if (!failedResources.IsEmpty)
            {
                validResources.RemoveAll(r => failedResources.Contains(r));
            }
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

                if (outputPkg.PackageSize >= 1073741824) // 1GB limit
                {
                    outputPkg.Close();
                    outputPkg = null;
                }
            }
            catch (Exception)
            {
                // Skip resources that fail to write (corrupt data)
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
            var failedResources = new System.Collections.Concurrent.ConcurrentBag<ResourceEntry>();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.ForEach(validResources, parallelOptions, resource =>
            {
                try
                {
                    resource.Compress(_options.CompressionLevel);
                }
                catch (Exception)
                {
                    failedResources.Add(resource);
                }
            });
            // Remove failed resources so they don't get added to the output package
            if (!failedResources.IsEmpty)
            {
                validResources.RemoveAll(r => failedResources.Contains(r));
            }
        }

        // Add to package sequentially since AddResource is not thread-safe
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

                if (outputPkg.PackageSize >= 1073741824) // 1GB limit
                {
                    outputPkg.Close();
                    outputPkg = null;
                }
            }
            catch (Exception)
            {
                // Skip resources that fail to write (corrupt data)
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

    public (string PackageType, string CASCategories, string CASAge, string CASGender, string CASOutfitCategory) DetectPackageType(string filePath, bool isSims3Pack)
    {
        try
        {
            var packages = new List<S3ForgeTools.GameFiles.Package.DBPFPackage>();
            S3ForgeTools.GameFiles.TS3Pack.Sims3Pack? s3p = null;

            bool isWorld = filePath.EndsWith(".world", StringComparison.OrdinalIgnoreCase);
            bool isSim = filePath.EndsWith(".sim", StringComparison.OrdinalIgnoreCase);
            bool isLot = false;

            if (isSims3Pack)
            {
                s3p = new S3ForgeTools.GameFiles.TS3Pack.Sims3Pack(filePath);
                packages.AddRange(s3p.Packages);

                if (!string.IsNullOrEmpty(s3p.Type))
                {
                    if (s3p.Type.Equals("World", StringComparison.OrdinalIgnoreCase) ||
                        (s3p.SubType != null && s3p.SubType.Equals("World", StringComparison.OrdinalIgnoreCase)))
                    {
                        isWorld = true;
                    }
                    else if (s3p.Type.Equals("Lot", StringComparison.OrdinalIgnoreCase) ||
                             s3p.Type.Equals("House", StringComparison.OrdinalIgnoreCase) ||
                             (s3p.SubType != null && s3p.SubType.Equals("Lot", StringComparison.OrdinalIgnoreCase)))
                    {
                        isLot = true;
                    }
                    else if (s3p.Type.Equals("Sim", StringComparison.OrdinalIgnoreCase) ||
                             s3p.Type.Equals("Household", StringComparison.OrdinalIgnoreCase) ||
                             (s3p.SubType != null && s3p.SubType.Equals("Sim", StringComparison.OrdinalIgnoreCase)))
                    {
                        isSim = true;
                    }
                }
            }
            else
            {
                packages.Add(new S3ForgeTools.GameFiles.Package.DBPFPackage(filePath));
            }

            if (isWorld)
            {
                s3p?.Dispose();
                foreach (var p in packages) if (!isSims3Pack) p.Dispose();
                return ("World", "", "", "", "");
            }
            if (isLot)
            {
                s3p?.Dispose();
                foreach (var p in packages) if (!isSims3Pack) p.Dispose();
                return ("Lot", "", "", "", "");
            }
            if (isSim)
            {
                s3p?.Dispose();
                foreach (var p in packages) if (!isSims3Pack) p.Dispose();
                return ("Sim", "", "", "", "");
            }

            var categories = new HashSet<string>();
            var ages = new HashSet<string>();
            var genders = new HashSet<string>();
            var outfitCategories = new HashSet<string>();

            bool isBuildBuy = false;
            bool hasCaspOverall = false;
            bool hasWorldRes = false;
            bool hasLotRes = false;
            bool hasSimRes = false;

            foreach (var package in packages)
            {
                if (package.Resources.Any(r => r.Key.Type == 107542056)) hasWorldRes = true;
                if (package.Resources.Any(r => r.Key.Type == 3496170587u)) hasLotRes = true;
                if (package.Resources.Any(r => r.Key.Type == 83396964)) hasSimRes = true;

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

                            if ((casp.AgeGender & 0x0001) != 0) ages.Add("Baby");
                            if ((casp.AgeGender & 0x0002) != 0) ages.Add("Toddler");
                            if ((casp.AgeGender & 0x0004) != 0) ages.Add("Child");
                            if ((casp.AgeGender & 0x0008) != 0) ages.Add("Teen");
                            if ((casp.AgeGender & 0x0010) != 0) ages.Add("YoungAdult");
                            if ((casp.AgeGender & 0x0020) != 0) ages.Add("Adult");
                            if ((casp.AgeGender & 0x0040) != 0) ages.Add("Elder");

                            if ((casp.AgeGender & 0x1000) != 0) genders.Add("Male");
                            if ((casp.AgeGender & 0x2000) != 0) genders.Add("Female");

                            if ((casp.Category & 0x0001) != 0) outfitCategories.Add("Everyday");
                            if ((casp.Category & 0x0002) != 0) outfitCategories.Add("Formal");
                            if ((casp.Category & 0x0004) != 0) outfitCategories.Add("Sleepwear");
                            if ((casp.Category & 0x0008) != 0) outfitCategories.Add("Swimwear");
                            if ((casp.Category & 0x0010) != 0) outfitCategories.Add("Athletic");
                            if ((casp.Category & 0x0020) != 0) outfitCategories.Add("Career");
                            if ((casp.Category & 0x0040) != 0) outfitCategories.Add("Outerwear");
                        }
                        catch { }
                    }
                }
                else
                {
                    if (package.Resources.Any(r => r.Key.Type == 0x319E4F1D))
                    {
                        isBuildBuy = true;
                    }
                    else
                    {
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

            if (hasWorldRes) return ("World", "", "", "", "");
            if (hasLotRes) return ("Lot", "", "", "", "");

            if (hasCaspOverall)
            {
                categories.Remove("Sliders");
                categories.Remove("Presets");
                categories.Remove("Skins");
                string joinedCat = string.Join(",", categories);
                string joinedAge = string.Join(",", ages);
                string joinedGender = string.Join(",", genders);
                string joinedOutfit = string.Join(",", outfitCategories);
                return ("CAS", joinedCat, joinedAge, joinedGender, joinedOutfit);
            }

            if (isBuildBuy) return ("BuildBuy", "", "", "", "");
            if (hasSimRes) return ("Sim", "", "", "", "");

            if (categories.Any())
            {
                string joinedCat = string.Join(",", categories);
                return ("CAS", joinedCat, "", "", "");
            }
        }
        catch { /* ignore parsing errors */ }

        return ("Other", "", "", "", "");
    }

    public async Task RecheckPackageTypesAsync(Action<string>? onProgress = null, bool skipUserTagged = true)
    {
        var items = await _db.MetaEntities.ToListAsync();
        int count = 0;
        int updatedCount = 0;
        int total = items.Count;

        onProgress?.Invoke(_localizer.GetString("rechecking_items_start", total));

        foreach (var item in items)
        {
            count++;
            if (count % 25 == 0)
            {
                onProgress?.Invoke(_localizer.GetString("rechecking_items_progress", count, total));
            }

            if (skipUserTagged && item.IsUserTagged)
            {
                continue;
            }

            bool isSims3Pack = item.FileType == "TS3PACK";

            // Fix potentially outdated relative CompleteFileName paths from old DB entries
            string expectedPath = Path.Combine(_options.ManagedPackageFolderPath, item.FileName);
            if (item.CompleteFileName != expectedPath)
            {
                item.CompleteFileName = expectedPath;
                updatedCount++;
            }

            // Always re-evaluate items to apply new improved logic
            if (string.IsNullOrEmpty(item.PackageType) || item.PackageType == "Other" || item.PackageType == "CAS" || item.PackageType == "World" || item.PackageType == "Lot" || item.PackageType == "Sim")
            {
                if (File.Exists(item.CompleteFileName))
                {
                    var typeInfo = DetectPackageType(item.CompleteFileName, isSims3Pack);

                    if (item.PackageType != typeInfo.PackageType ||
                        item.CASCategories != typeInfo.CASCategories ||
                        item.CASAge != typeInfo.CASAge ||
                        item.CASGender != typeInfo.CASGender ||
                        item.CASOutfitCategory != typeInfo.CASOutfitCategory)
                    {
                        item.PackageType = typeInfo.PackageType;
                        item.CASCategories = typeInfo.CASCategories;
                        item.CASAge = typeInfo.CASAge;
                        item.CASGender = typeInfo.CASGender;
                        item.CASOutfitCategory = typeInfo.CASOutfitCategory;
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
            onProgress?.Invoke(_localizer.GetString("rechecking_saving_items", updatedCount));
            await _db.SaveChangesAsync();
        }

        onProgress?.Invoke(_localizer.GetString("rechecking_finished", updatedCount));
    }
}
