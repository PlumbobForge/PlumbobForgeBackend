using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
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
    private readonly LocalizationService _localizer;
    private readonly PackageTypeService _packageTypeService;
    private readonly ArchiveService _archiveService;
    private readonly ThumbnailService _thumbnailService;
    private readonly CacheBuilderService _cacheBuilderService;

    public PKGManager(
        AppDbContext db,
        IOptionsSnapshot<PlumbobForgeOptions> options,
        LocalizationService localizer,
        PackageTypeService packageTypeService,
        ArchiveService archiveService,
        ThumbnailService thumbnailService,
        CacheBuilderService cacheBuilderService)
    {
        _db = db;
        _options = options.Value;
        _localizer = localizer;
        _packageTypeService = packageTypeService;
        _archiveService = archiveService;
        _thumbnailService = thumbnailService;
        _cacheBuilderService = cacheBuilderService;
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

    public static bool IsArchiveExtension(string filePath) => ArchiveService.IsArchiveExtension(filePath);

    public static List<string> GetArchivePackageFileNames(string archivePath) => ArchiveService.GetArchivePackageFileNames(archivePath);

    public static List<string> GetArchivePackageFileNames(Stream stream) => ArchiveService.GetArchivePackageFileNames(stream);

    public List<string> CheckFormDuplicates(Microsoft.AspNetCore.Http.IFormFileCollection files) => _archiveService.CheckFormDuplicates(files);

    public List<string> ExtractPackagesFromArchive(string archivePath, string destinationFolder, string duplicateAction = "rename", Action<string>? onProgress = null)
        => _archiveService.ExtractPackagesFromArchive(archivePath, destinationFolder, duplicateAction, onProgress, MarkExistingFileDirty);

    public List<string> GetDuplicateFiles(IEnumerable<string> fileNames) => _archiveService.GetDuplicateFiles(fileNames);

    public async Task UploadFilesAsync(Microsoft.AspNetCore.Http.IFormFileCollection files, Action<string>? onProgress = null, string duplicateAction = "rename", long? targetSetId = null)
    {
        CreateFolders();
        int importedCount = 0;
        int totalFiles = files.Count;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            string fileName = Path.GetFileName(file.FileName);
            if (IsArchiveExtension(fileName))
            {
                string tempArchivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_" + fileName);
                try
                {
                    using (var stream = new FileStream(tempArchivePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    var extractedPaths = ExtractPackagesFromArchive(tempArchivePath, _options.ManagedPackageFolderPath, duplicateAction, onProgress);
                    importedCount += extractedPaths.Count;
                }
                finally
                {
                    try { if (File.Exists(tempArchivePath)) File.Delete(tempArchivePath); } catch { }
                }
            }
            else
            {
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (ext != ".package" && ext != ".sims3pack") continue;

                string destPath = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                if (File.Exists(destPath))
                {
                    if (string.Equals(duplicateAction, "skip", StringComparison.OrdinalIgnoreCase))
                    {
                        onProgress?.Invoke($"Skipped duplicate file: {fileName}");
                        continue;
                    }
                    else if (string.Equals(duplicateAction, "replace", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(destPath); } catch { }
                        MarkExistingFileDirty(fileName);
                    }
                    else
                    {
                        destPath = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                    }
                }

                using (var stream = new FileStream(destPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                importedCount++;
                onProgress?.Invoke($"Imported file: {fileName}");
            }
        }

        if (importedCount > 0)
        {
            onProgress?.Invoke("Registering imported files...");
            await CheckOrphanPackagesAsync(targetSetId);
            onProgress?.Invoke("Import finished.");
        }
    }

    public async Task ImportFilesAsync(string[] files, Action<string>? onProgress = null, string duplicateAction = "rename", long? targetSetId = null)
    {
        CreateFolders();
        int importedCount = 0;

        foreach (var filePath in files)
        {
            if (!File.Exists(filePath)) continue;

            string fileName = Path.GetFileName(filePath);
            if (IsArchiveExtension(fileName))
            {
                var extractedPaths = ExtractPackagesFromArchive(filePath, _options.ManagedPackageFolderPath, duplicateAction, onProgress);
                importedCount += extractedPaths.Count;
            }
            else
            {
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (ext != ".package" && ext != ".sims3pack") continue;

                string destPath = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                if (File.Exists(destPath))
                {
                    if (string.Equals(duplicateAction, "skip", StringComparison.OrdinalIgnoreCase))
                    {
                        onProgress?.Invoke($"Skipped duplicate file: {fileName}");
                        continue;
                    }
                    else if (string.Equals(duplicateAction, "replace", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(destPath); } catch { }
                        MarkExistingFileDirty(fileName);
                    }
                    else
                    {
                        destPath = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                    }
                }

                File.Copy(filePath, destPath, overwrite: true);
                importedCount++;
                onProgress?.Invoke($"Imported file: {fileName}");
            }
        }

        if (importedCount > 0)
        {
            onProgress?.Invoke("Registering imported files...");
            await CheckOrphanPackagesAsync(targetSetId);
            onProgress?.Invoke("Import finished.");
        }
    }

    public string GetDownloadsFolderPath()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "Downloads");
    }

    public List<string> GetObservedFolders()
    {
        var result = new List<string>();
        string downloads = GetDownloadsFolderPath();
        if (Directory.Exists(downloads)) result.Add(downloads);
        return result;
    }

    public List<string> CheckDownloadsDuplicates()
    {
        string downloadsDir = GetDownloadsFolderPath();
        if (!Directory.Exists(downloadsDir)) return new List<string>();

        var validExts = new[] { ".package", ".sims3pack", ".zip", ".rar", ".7z" };
        var candidateNames = new List<string>();

        foreach (var file in Directory.GetFiles(downloadsDir))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (!validExts.Contains(ext)) continue;

            string fileName = Path.GetFileName(file);
            if (IsArchiveExtension(fileName))
            {
                try
                {
                    candidateNames.AddRange(GetArchivePackageFileNames(file));
                }
                catch { }
            }
            else
            {
                candidateNames.Add(fileName);
            }
        }

        return GetDuplicateFiles(candidateNames);
    }

    public async Task<int> ImportFromDownloadsAsync(Action<string>? onProgress = null, string duplicateAction = "rename")
    {
        string downloadsDir = GetDownloadsFolderPath();
        if (!Directory.Exists(downloadsDir)) return 0;

        CreateFolders();

        var validExts = new[] { ".package", ".sims3pack", ".zip", ".rar", ".7z" };
        var filesToImport = Directory.GetFiles(downloadsDir)
            .Where(f => validExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        int importedCount = 0;

        foreach (var file in filesToImport)
        {
            string fileName = Path.GetFileName(file);

            if (IsArchiveExtension(fileName))
            {
                var extracted = ExtractPackagesFromArchive(file, _options.ManagedPackageFolderPath, duplicateAction, onProgress);
                if (extracted.Count > 0)
                {
                    try { File.Delete(file); } catch { }
                    importedCount += extracted.Count;
                }
            }
            else
            {
                string destPath = Path.Combine(_options.ManagedPackageFolderPath, fileName);

                if (File.Exists(destPath))
                {
                    if (string.Equals(duplicateAction, "skip", StringComparison.OrdinalIgnoreCase))
                    {
                        onProgress?.Invoke($"Skipped duplicate file: {fileName}");
                        continue;
                    }
                    else if (string.Equals(duplicateAction, "replace", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(destPath); } catch { }
                        MarkExistingFileDirty(fileName);
                    }
                    else
                    {
                        destPath = Path.Combine(_options.ManagedPackageFolderPath, Guid.NewGuid().ToString().Substring(0, 8) + "_" + fileName);
                    }
                }

                try
                {
                    File.Move(file, destPath, overwrite: true);
                    importedCount++;
                    onProgress?.Invoke($"Moved from Downloads: {fileName}");
                }
                catch (Exception ex)
                {
                    onProgress?.Invoke($"Failed to move {fileName}: {ex.Message}");
                }
            }
        }

        if (importedCount > 0)
        {
            onProgress?.Invoke("Registering imported files...");
            await CheckOrphanPackagesAsync();
            onProgress?.Invoke("Import finished.");
        }

        return importedCount;
    }

    public async Task AutoFixAsync(Action<string>? onProgress = null)
    {
        onProgress?.Invoke("Starting Auto-Fix System...");

        var defaultSet = await _db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
        if (defaultSet == null)
        {
            defaultSet = new SetsEntity { Name = "Default", FolderName = "Default" };
            _db.SetsEntities.Add(defaultSet);
            await _db.SaveChangesAsync();
            onProgress?.Invoke("Created missing 'Default' set.");
        }

        var unassignedItems = await _db.MetaEntities.Where(m => m.SetsEntityId == null).ToListAsync();
        if (unassignedItems.Count > 0)
        {
            foreach (var item in unassignedItems)
            {
                item.SetsEntityId = defaultSet.Id;
            }
            defaultSet.Dirty = true;
            await _db.SaveChangesAsync();
            onProgress?.Invoke($"Assigned {unassignedItems.Count} unassigned items to Default set.");
        }

        var allSets = await _db.SetsEntities.ToListAsync();
        foreach (var set in allSets)
        {
            set.Dirty = true;
        }
        await _db.SaveChangesAsync();

        onProgress?.Invoke("Rebuilding all sets cache...");
        await RunAsync(isRefresh: true, onProgress);
    }

    public async Task SyncToSims3Async(Action<string>? onProgress = null, bool forceRebuildStatic = false)
        => await _cacheBuilderService.SyncToSims3Async(onProgress, forceRebuildStatic);

    private void CreateFolders()
    {
        Directory.CreateDirectory(_options.ManagedPackageFolderPath);
        Directory.CreateDirectory(_options.SetCacheFolderPath);
        Directory.CreateDirectory(Path.Combine(_options.DocumentBaseDir, "Thumbnails"));
    }

    private void MarkExistingFileDirty(string fileName)
    {
        var existing = _db.MetaEntities.FirstOrDefault(m => m.FileName == fileName);
        if (existing != null && existing.SetsEntityId.HasValue)
        {
            var set = _db.SetsEntities.Find(existing.SetsEntityId.Value);
            if (set != null) set.Dirty = true;
        }
    }

    private async Task CheckOrphanPackagesAsync(long? targetSetId = null)
    {
        if (!Directory.Exists(_options.ManagedPackageFolderPath)) return;

        var setEntities = await _db.SetsEntities.ToListAsync();

        SetsEntity assignSet;
        if (targetSetId.HasValue)
        {
            var requestedSet = setEntities.FirstOrDefault(s => s.Id == targetSetId.Value);
            assignSet = requestedSet ?? setEntities.FirstOrDefault(s => s.Name == "Default") ?? setEntities.First();
        }
        else
        {
            assignSet = setEntities.FirstOrDefault(s => s.Name == "Default") ?? setEntities.First();
        }

        var metaEntities = await _db.MetaEntities.ToListAsync();
        var currentFilesOnDisk = Directory.GetFiles(_options.ManagedPackageFolderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".package", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var diskFileMap = currentFilesOnDisk.ToDictionary(f => Path.GetFileName(f), f => f, StringComparer.OrdinalIgnoreCase);

        var toRemoveFromDb = metaEntities.Where(m => !diskFileMap.ContainsKey(m.FileName)).ToList();
        if (toRemoveFromDb.Count > 0)
        {
            var setIdsToDirty = toRemoveFromDb.Where(m => m.SetsEntityId.HasValue).Select(m => m.SetsEntityId!.Value).Distinct().ToList();
            var setsToDirty = setEntities.Where(s => setIdsToDirty.Contains(s.Id)).ToList();
            foreach (var s in setsToDirty) s.Dirty = true;

            _db.MetaEntities.RemoveRange(toRemoveFromDb);
        }

        bool metaAddedOrUpdated = false;

        foreach (var kvp in diskFileMap)
        {
            string fileName = kvp.Key;
            string filePath = kvp.Value;
            var fileInfo = new FileInfo(filePath);
            double currentSizeKb = fileInfo.Length / 1024.0;

            var existingMeta = metaEntities.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (existingMeta == null)
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
                    FileSize = currentSizeKb
                };

                assignSet.Dirty = true;
                _db.MetaEntities.Add(meta);
                metaAddedOrUpdated = true;
            }
            else
            {
                bool isSims3Pack = Path.GetExtension(fileName).Equals(".sims3pack", StringComparison.OrdinalIgnoreCase);
                bool sizeChanged = Math.Abs(existingMeta.FileSize - currentSizeKb) > 0.01;

                if (sizeChanged || existingMeta.CompleteFileName != filePath || (existingMeta.SetsEntity != null && existingMeta.SetsEntity.Dirty))
                {
                    var typeInfo = DetectPackageType(filePath, isSims3Pack);
                    existingMeta.FileType = isSims3Pack ? "TS3PACK" : "DBPF";
                    existingMeta.PackageType = typeInfo.PackageType;
                    existingMeta.CASCategories = typeInfo.CASCategories;
                    existingMeta.CASAge = typeInfo.CASAge;
                    existingMeta.CASGender = typeInfo.CASGender;
                    existingMeta.CASOutfitCategory = typeInfo.CASOutfitCategory;
                    existingMeta.CompleteFileName = filePath;
                    existingMeta.FileSize = currentSizeKb;
                    existingMeta.InstallDate = DateTime.Now.ToString();

                    if (existingMeta.SetsEntity != null)
                    {
                        existingMeta.SetsEntity.Dirty = true;
                    }
                    else
                    {
                        existingMeta.SetsEntity = assignSet;
                        assignSet.Dirty = true;
                    }
                    metaAddedOrUpdated = true;
                }
            }
        }

        if (toRemoveFromDb.Count > 0 || metaAddedOrUpdated)
        {
            await _db.SaveChangesAsync();
        }
    }

    private async Task CheckSetParentingAsync()
    {
        var allSets = await _db.SetsEntities.ToListAsync();
        bool modified = false;

        foreach (var set in allSets)
        {
            if (set.ParentSetsEntityId.HasValue)
            {
                var visited = new HashSet<long> { set.Id };
                var current = allSets.FirstOrDefault(s => s.Id == set.ParentSetsEntityId.Value);
                bool isCycleOrMissing = false;

                while (current != null)
                {
                    if (visited.Contains(current.Id))
                    {
                        isCycleOrMissing = true;
                        break;
                    }
                    visited.Add(current.Id);
                    current = current.ParentSetsEntityId.HasValue
                        ? allSets.FirstOrDefault(s => s.Id == current.ParentSetsEntityId.Value)
                        : null;
                }

                if (isCycleOrMissing || !allSets.Any(s => s.Id == set.ParentSetsEntityId.Value))
                {
                    set.ParentSetsEntityId = null;
                    modified = true;
                }
            }
        }

        if (modified)
        {
            await _db.SaveChangesAsync();
        }
    }

    private async Task CheckConfigurationsAsync()
    {
        var configs = await _db.ConfigEntities.Include(c => c.ConfigSetsEntities).ToListAsync();
        if (configs.Count == 0)
        {
            var defaultConfig = new ConfigEntity { Name = "Default", Description = "Default Configuration", Active = true };
            _db.ConfigEntities.Add(defaultConfig);
            await _db.SaveChangesAsync();

            var allSets = await _db.SetsEntities.ToListAsync();
            foreach (var set in allSets)
            {
                _db.ConfigSetsEntities.Add(new ConfigSetsEntity { ConfigEntityId = defaultConfig.Id, SetsEntityId = set.Id });
            }
            await _db.SaveChangesAsync();
        }
        else if (!configs.Any(c => c.Active))
        {
            configs[0].Active = true;
            await _db.SaveChangesAsync();
        }

        var activeConfig = await _db.ConfigEntities.Include(c => c.ConfigSetsEntities).FirstOrDefaultAsync(c => c.Active);
        if (activeConfig != null)
        {
            var existingSetIds = activeConfig.ConfigSetsEntities.Select(cs => cs.SetsEntityId).ToHashSet();
            var allSets = await _db.SetsEntities.ToListAsync();
            bool added = false;
            foreach (var set in allSets)
            {
                if (!existingSetIds.Contains(set.Id))
                {
                    _db.ConfigSetsEntities.Add(new ConfigSetsEntity { ConfigEntityId = activeConfig.Id, SetsEntityId = set.Id });
                    added = true;
                }
            }
            if (added) await _db.SaveChangesAsync();
        }
    }

    public async Task RebuildCacheAsync(bool forceRebuild = false, Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
        => await _cacheBuilderService.RebuildCacheAsync(forceRebuild, onProgress, skippedFiles);

    public async Task RebuildStaticCacheAsync(Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
        => await _cacheBuilderService.RebuildStaticCacheAsync(onProgress, skippedFiles);

    public string GetSetFolderName(SetsEntity activeSet) => _cacheBuilderService.GetSetFolderName(activeSet);
    public string GetSetPath(SetsEntity activeSet) => _cacheBuilderService.GetSetPath(activeSet);
    public string GetSetCachePath(string folderName) => _cacheBuilderService.GetSetCachePath(folderName);
    public void RebuildSet(SetsEntity activeSet, Action<string>? onProgress = null, List<(string FileName, string Reason)>? skippedFiles = null)
        => _cacheBuilderService.RebuildSet(activeSet, onProgress, skippedFiles);

    public async Task<string?> GetThumbnailPathAsync(long itemId) => await _thumbnailService.GetThumbnailPathAsync(itemId);

    public (string PackageType, string CASCategories, string CASAge, string CASGender, string CASOutfitCategory) DetectPackageType(string filePath, bool isSims3Pack)
        => _packageTypeService.DetectPackageType(filePath, isSims3Pack);

    public async Task RecheckPackageTypesAsync(Action<string>? onProgress = null, bool skipUserTagged = true)
        => await _packageTypeService.RecheckPackageTypesAsync(onProgress, skipUserTagged);
}
