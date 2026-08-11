using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PlumbobForge.Backend.Configuration;

namespace PlumbobForge.Backend.Services;

public class DownloadsWatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DownloadsWatcherService> _logger;
    private readonly System.Collections.Generic.List<FileSystemWatcher> _watchers = new();
    private readonly SemaphoreSlim _importLock = new(1, 1);

    public DownloadsWatcherService(IServiceProvider serviceProvider, ILogger<DownloadsWatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ReloadWatchers();

        // Initial scan on startup
        await TriggerAutoImportAsync("Startup scan");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    public void ReloadWatchers()
    {
        try
        {
            StopWatchers();

            using var scope = _serviceProvider.CreateScope();
            var pkgManager = scope.ServiceProvider.GetRequiredService<PKGManager>();
            var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<PlumbobForgeOptions>>().Value;
            var folders = pkgManager.GetObservedFolders();

            // 1. Watch observed "Downloads" folders
            foreach (var dir in folders)
            {
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }

                if (Directory.Exists(dir))
                {
                    _logger.LogInformation("Starting observed folder watcher on: {Path}", dir);
                    var watcher = new FileSystemWatcher(dir)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                        Filter = "*.*",
                        EnableRaisingEvents = true
                    };
                    watcher.Created += OnFileCreatedOrChanged;
                    watcher.Changed += OnFileCreatedOrChanged;
                    watcher.Renamed += OnFileRenamed;
                    _watchers.Add(watcher);
                }
            }

            // 2. Watch main Library folder (including subdirectories for set folders and restored Recycle Bin files)
            string libraryDir = options.ManagedPackageFolderPath;
            if (!string.IsNullOrWhiteSpace(libraryDir))
            {
                if (!Directory.Exists(libraryDir))
                {
                    try { Directory.CreateDirectory(libraryDir); } catch { }
                }

                if (Directory.Exists(libraryDir))
                {
                    _logger.LogInformation("Starting main Library folder watcher on: {Path}", libraryDir);
                    var libWatcher = new FileSystemWatcher(libraryDir)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                        Filter = "*.*",
                        EnableRaisingEvents = true
                    };
                    libWatcher.Created += OnLibraryFileChanged;
                    libWatcher.Changed += OnLibraryFileChanged;
                    libWatcher.Deleted += OnLibraryFileChanged;
                    libWatcher.Renamed += OnLibraryFileRenamed;
                    _watchers.Add(libWatcher);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading observed folder watchers.");
        }
    }

    private void StopWatchers()
    {
        foreach (var w in _watchers)
        {
            try
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            catch { }
        }
        _watchers.Clear();
    }

    private void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
    {
        if (IsImportableExtension(e.FullPath))
        {
            _ = Task.Run(() => DebouncedImportAsync(e.FullPath));
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsImportableExtension(e.FullPath))
        {
            _ = Task.Run(() => DebouncedImportAsync(e.FullPath));
        }
    }

    private void OnLibraryFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsPackageExtension(e.FullPath))
        {
            _ = Task.Run(() => DebouncedLibraryScanAsync(e.FullPath, e.ChangeType.ToString()));
        }
    }

    private void OnLibraryFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsPackageExtension(e.FullPath) || IsPackageExtension(e.OldFullPath))
        {
            _ = Task.Run(() => DebouncedLibraryScanAsync(e.FullPath, "Renamed"));
        }
    }

    private static bool IsImportableExtension(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".package" || ext == ".sims3pack" || ext == ".zip" || ext == ".rar" || ext == ".7z";
    }

    private static bool IsPackageExtension(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".package" || ext == ".sims3pack";
    }

    private async Task DebouncedImportAsync(string filePath)
    {
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            if (!File.Exists(filePath)) return;
            if (IsFileReady(filePath)) break;
        }

        await TriggerAutoImportAsync($"New file detected: {Path.GetFileName(filePath)}");
    }

    private async Task DebouncedLibraryScanAsync(string filePath, string changeType)
    {
        // Wait up to 5 seconds for file restoration/copying to complete and locks to release
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            if (!File.Exists(filePath)) break; // If file was deleted/removed, proceed to scan
            if (IsFileReady(filePath)) break;
        }

        await TriggerLibraryScanAsync($"Library file {changeType}: {Path.GetFileName(filePath)}");
    }

    private async Task TriggerAutoImportAsync(string reason)
    {
        if (!await _importLock.WaitAsync(100)) return;

        try
        {
            _logger.LogInformation("Triggering automatic import from Downloads ({Reason})...", reason);
            using var scope = _serviceProvider.CreateScope();
            var pkgManager = scope.ServiceProvider.GetRequiredService<PKGManager>();
            var notifier = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var duplicates = pkgManager.CheckDownloadsDuplicates();
            if (duplicates.Count > 0)
            {
                _logger.LogInformation("Found {Count} duplicate file(s) in Downloads. Prompting user modal...", duplicates.Count);
                await notifier.BroadcastAsync("auto_import_duplicates", duplicates);
                return;
            }

            int count = await pkgManager.ImportFromDownloadsAsync(msg => _logger.LogInformation("[AutoImport] {Msg}", msg), "rename");
            if (count > 0)
            {
                _logger.LogInformation("Auto-imported {Count} package(s) from Downloads.", count);
                await notifier.BroadcastAsync("items_imported", new { count });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic import from Downloads.");
        }
        finally
        {
            _importLock.Release();
        }
    }

    private async Task TriggerLibraryScanAsync(string reason)
    {
        await _importLock.WaitAsync();

        try
        {
            _logger.LogInformation("Triggering automatic Library rescan ({Reason})...", reason);
            using var scope = _serviceProvider.CreateScope();
            var pkgManager = scope.ServiceProvider.GetRequiredService<PKGManager>();
            var notifier = scope.ServiceProvider.GetRequiredService<NotificationService>();

            await pkgManager.ScanLibraryDiskAsync(msg => _logger.LogInformation("[LibraryWatcher] {Msg}", msg));
            await notifier.BroadcastAsync("library_changed", new { reason });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic Library rescan.");
        }
        finally
        {
            _importLock.Release();
        }
    }

    private static bool IsFileReady(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return stream.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public override void Dispose()
    {
        StopWatchers();
        _importLock.Dispose();
        base.Dispose();
    }
}
