using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PlumbobForge.Backend.Configuration;
using PlumbobForge.Backend.Database;
using PlumbobForge.Backend.Services;

namespace PlumbobForge.Backend.Endpoints;

public record SaveSettingsRequest(PlumbobForgeOptions Options, bool MoveFolder);
public class ValidateGameFilesRequest { public string Path { get; set; } = ""; }

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app, string appSettingsFile)
    {
        // Read settings
        app.MapGet("/api/settings", (IOptionsSnapshot<PlumbobForgeOptions> options) =>
        {
            return Results.Ok(options.Value);
        });

        // Save settings
        app.MapPost("/api/settings", async (SaveSettingsRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var newOptions = req.Options;
            if (File.Exists(appSettingsFile))
            {
                var jsonText = await File.ReadAllTextAsync(appSettingsFile);
                var jObject = System.Text.Json.Nodes.JsonNode.Parse(jsonText) as System.Text.Json.Nodes.JsonObject;

                if (jObject != null)
                {
                    var oldBaseDir = jObject["PlumbobForge"]?["DocumentBaseDir"]?.ToString();

                    newOptions.ManagedPackageFolderName = "Library";
                    newOptions.SetCacheFolderName = "Builds";

                    var jsonObserved = new System.Text.Json.Nodes.JsonArray();
                    if (newOptions.ObservedFolders != null)
                    {
                        foreach (var folder in newOptions.ObservedFolders)
                        {
                            if (!string.IsNullOrWhiteSpace(folder)) jsonObserved.Add(folder);
                        }
                    }

                    var ccMagicNode = new System.Text.Json.Nodes.JsonObject
                    {
                        ["DocumentBaseDir"] = newOptions.DocumentBaseDir,
                        ["DownloadFolderName"] = newOptions.DownloadFolderName,
                        ["ArchiveFolderName"] = newOptions.ArchiveFolderName,
                        ["TS3PackFolderName"] = newOptions.TS3PackFolderName,
                        ["ManagedPackageFolderName"] = newOptions.ManagedPackageFolderName,
                        ["SetCacheFolderName"] = newOptions.SetCacheFolderName,
                        ["LegacyPackageFolderName"] = newOptions.LegacyPackageFolderName,
                        ["TS3PackStoreFolderName"] = newOptions.TS3PackStoreFolderName,
                        ["GameFilesDir"] = newOptions.GameFilesDir,
                        ["CompressionLevel"] = newOptions.CompressionLevel,
                        ["HasSeenWalkthrough"] = newOptions.HasSeenWalkthrough,
                        ["Language"] = newOptions.Language,
                        ["Theme"] = newOptions.Theme,
                        ["CacheMethod"] = newOptions.CacheMethod,
                        ["EnableAutoScan"] = newOptions.EnableAutoScan,
                        ["ObservedFolders"] = jsonObserved
                    };

                    jObject["PlumbobForge"] = ccMagicNode;

                    var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(appSettingsFile, jObject.ToJsonString(jsonOptions));

                    try
                    {
                        var config = ctx.RequestServices.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                        (config as Microsoft.Extensions.Configuration.IConfigurationRoot)?.Reload();

                        var watcherService = ctx.RequestServices.GetService<DownloadsWatcherService>();
                        watcherService?.ReloadWatchers(newOptions);
                    }
                    catch { }

                    if (req.MoveFolder && !string.IsNullOrEmpty(oldBaseDir) && !string.IsNullOrEmpty(newOptions.DocumentBaseDir) && oldBaseDir != newOptions.DocumentBaseDir)
                    {
                        if (Directory.Exists(oldBaseDir))
                        {
                            try
                            {
                                if (!Directory.Exists(newOptions.DocumentBaseDir))
                                {
                                    Directory.Move(oldBaseDir, newOptions.DocumentBaseDir);
                                }
                            }
                            catch { /* Best effort move */ }
                        }
                    }

                    if (!string.IsNullOrEmpty(oldBaseDir) && !string.IsNullOrEmpty(newOptions.DocumentBaseDir) && !string.Equals(oldBaseDir, newOptions.DocumentBaseDir, StringComparison.OrdinalIgnoreCase))
                    {
                        var newManagedPath = Path.Combine(newOptions.DocumentBaseDir, newOptions.ManagedPackageFolderName);
                        var allItems = await db.MetaEntities.ToListAsync();
                        foreach (var item in allItems)
                        {
                            item.CompleteFileName = Path.Combine(newManagedPath, item.FileName);
                        }
                        await db.SaveChangesAsync();
                    }
                }
            }

            return Results.Ok(new { message = "Settings saved." });
        });

        // Autodetect base folder
        app.MapPost("/api/settings/autodetect", () =>
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var basePath = Path.Combine(docs, "PlumbobForge");

            var autoOptions = new PlumbobForgeOptions
            {
                DocumentBaseDir = basePath,
                DownloadFolderName = "Downloads",
                ArchiveFolderName = string.Empty,
                TS3PackFolderName = string.Empty,
                ManagedPackageFolderName = "Library",
                SetCacheFolderName = "Builds",
                LegacyPackageFolderName = string.Empty,
                TS3PackStoreFolderName = string.Empty
            };

            return Results.Ok(autoOptions);
        });

        // Autodetect game files
        app.MapGet("/api/settings/autodetect-gamefiles", () =>
        {
            string detectedPath = GamePathValidator.AutodetectGameFilesPath();
            return Results.Ok(new { path = detectedPath });
        });

        // Validate game files
        app.MapPost("/api/settings/validate-gamefiles", (ValidateGameFilesRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Path)) return Results.Ok(new { valid = false, normalizedPath = "" });
            var (valid, normalizedPath) = GamePathValidator.ValidateAndNormalize(req.Path);
            return Results.Ok(new { valid, normalizedPath });
        });
    }
}
