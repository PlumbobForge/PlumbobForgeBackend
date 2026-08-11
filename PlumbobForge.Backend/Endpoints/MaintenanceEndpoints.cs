using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PlumbobForge.Backend.Configuration;
using PlumbobForge.Backend.Database;
using PlumbobForge.Backend.Services;

namespace PlumbobForge.Backend.Endpoints;

public record CheckDuplicatesRequest(string[] FileNames);
public record ImportFilesRequest(string[] FilePaths, string? DuplicateAction, long? TargetSetId);

public static class MaintenanceEndpoints
{
    public static void MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        // Scan library SSE
        app.MapPost("/api/scan", async (HttpContext ctx, PKGManager manager) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");

            Action<string> onProgress = (msg) =>
            {
                try
                {
                    ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
                    ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
                }
                catch { }
            };

            try
            {
                await manager.RunAsync(true, onProgress);
            }
            catch (Exception ex)
            {
                onProgress($"Error: {ex.Message}");
            }

            onProgress("DONE");
        });

        // Duplicate check
        app.MapPost("/api/import/check-duplicates", async (HttpContext ctx, PKGManager manager) =>
        {
            if (ctx.Request.HasFormContentType)
            {
                var form = await ctx.Request.ReadFormAsync();
                var duplicates = manager.CheckFormDuplicates(form.Files);
                return Results.Ok(new { hasDuplicates = duplicates.Count > 0, duplicates });
            }
            else
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                List<string>? fileNames = null;
                try
                {
                    var reqObj = System.Text.Json.JsonSerializer.Deserialize<CheckDuplicatesRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    fileNames = reqObj?.FileNames?.ToList();
                }
                catch { }
                fileNames ??= new List<string>();
                var duplicates = manager.GetDuplicateFiles(fileNames);
                return Results.Ok(new { hasDuplicates = duplicates.Count > 0, duplicates });
            }
        }).DisableAntiforgery();

        // Import files SSE
        app.MapPost("/api/import-files", async (HttpContext ctx, PKGManager manager) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();

            string[]? filePaths = null;
            string duplicateAction = "rename";
            long? targetSetId = null;

            try
            {
                var reqObj = System.Text.Json.JsonSerializer.Deserialize<ImportFilesRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (reqObj != null && reqObj.FilePaths != null)
                {
                    filePaths = reqObj.FilePaths;
                    duplicateAction = reqObj.DuplicateAction ?? "rename";
                    targetSetId = reqObj.TargetSetId;
                }
                else
                {
                    filePaths = System.Text.Json.JsonSerializer.Deserialize<string[]>(body);
                }
            }
            catch
            {
                filePaths = System.Text.Json.JsonSerializer.Deserialize<string[]>(body);
            }

            ctx.Response.Headers.Append("Content-Type", "text/event-stream");

            Action<string> onProgress = (msg) =>
            {
                try
                {
                    ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
                    ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
                }
                catch { }
            };

            try
            {
                if (filePaths != null && filePaths.Length > 0)
                {
                    await manager.ImportFilesAsync(filePaths, onProgress, duplicateAction, targetSetId);
                }
            }
            catch (Exception ex)
            {
                onProgress($"Error: {ex.Message}");
            }

            onProgress("DONE");
        });

        // Upload files SSE
        app.MapPost("/api/upload-files", async (HttpContext ctx, PKGManager manager) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");

            Action<string> onProgress = (msg) =>
            {
                try
                {
                    ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
                    ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
                }
                catch { }
            };

            try
            {
                var form = await ctx.Request.ReadFormAsync();
                string duplicateAction = form.ContainsKey("duplicateAction") ? form["duplicateAction"].ToString() : "rename";
                long? targetSetId = null;
                if (form.ContainsKey("targetSetId") && long.TryParse(form["targetSetId"].ToString(), out long sid) && sid > 0)
                {
                    targetSetId = sid;
                }

                if (form.Files.Count > 0)
                {
                    await manager.UploadFilesAsync(form.Files, onProgress, duplicateAction, targetSetId);
                }
            }
            catch (Exception ex)
            {
                onProgress($"CRASH: {ex.Message}");
            }

            onProgress("DONE");
        }).DisableAntiforgery();

        // Notification stream SSE
        app.MapGet("/api/notifications/stream", async (HttpContext ctx, NotificationService notifier, CancellationToken cancellationToken) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            var id = notifier.Subscribe(async (data) =>
            {
                await ctx.Response.WriteAsync(data, cancellationToken);
                await ctx.Response.Body.FlushAsync(cancellationToken);
            });

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                notifier.Unsubscribe(id);
            }
        });

        // Downloads duplicates check
        app.MapGet("/api/import-downloads/check-duplicates", (PKGManager manager) =>
        {
            var duplicates = manager.CheckDownloadsDuplicates();
            return Results.Ok(new { hasDuplicates = duplicates.Count > 0, duplicates });
        });

        // Import downloads SSE
        app.MapPost("/api/import-downloads", async (HttpContext ctx, string? duplicateAction, PKGManager manager, NotificationService notifier) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");

            Action<string> onProgress = (msg) =>
            {
                try
                {
                    ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
                    ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
                }
                catch { }
            };

            try
            {
                int count = await manager.ImportFromDownloadsAsync(onProgress, duplicateAction ?? "rename");
                if (count > 0)
                {
                    await notifier.BroadcastAsync("items_imported", new { count });
                }
            }
            catch (Exception ex)
            {
                onProgress($"Error: {ex.Message}");
            }

            onProgress("DONE");
        });

        // Database Autofix SSE
        app.MapPost("/api/fix", async (HttpContext ctx, PKGManager manager) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");

            Action<string> onProgress = (msg) =>
            {
                try
                {
                    ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
                    ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
                }
                catch { }
            };

            try
            {
                await manager.AutoFixAsync(onProgress);
            }
            catch (Exception ex)
            {
                onProgress($"Error: {ex.Message}");
            }

            onProgress("DONE");
        });

        // Recheck package types SSE
        app.MapPost("/api/settings/recheck-types", async (HttpContext ctx, PKGManager manager) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");

            bool skipUserTagged = true;
            if (ctx.Request.Query.ContainsKey("skipUserTagged") && bool.TryParse(ctx.Request.Query["skipUserTagged"], out bool skipVal))
            {
                skipUserTagged = skipVal;
            }

            Action<string> onProgress = (msg) =>
            {
                try
                {
                    ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
                    ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
                }
                catch { }
            };

            try
            {
                await manager.RecheckPackageTypesAsync(onProgress, skipUserTagged);
            }
            catch (Exception ex)
            {
                onProgress($"Error: {ex.Message}");
            }

            onProgress("DONE");
        });

        // Migrate CC Magic
        app.MapPost("/api/migrate", async (IOptionsSnapshot<PlumbobForgeOptions> options, AppDbContext db, PKGManager manager) =>
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var oldCCMagicPath = Path.Combine(docs, "Electronic Arts", "CC Magic");
            var libraryPath = options.Value.ManagedPackageFolderPath;

            if (!Directory.Exists(oldCCMagicPath))
            {
                return Results.BadRequest(new { message = "Legacy CC Magic folder not found." });
            }

            if (string.IsNullOrEmpty(libraryPath))
            {
                return Results.BadRequest(new { message = "Library path is not configured. Please save your settings first." });
            }

            Directory.CreateDirectory(libraryPath);

            var files = Directory.GetFiles(oldCCMagicPath, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".package", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase));

            int count = 0;
            foreach (var file in files)
            {
                var dest = Path.Combine(libraryPath, Path.GetFileName(file));
                if (!File.Exists(dest))
                {
                    File.Move(file, dest);
                    count++;
                }
                else
                {
                    try { File.Delete(file); } catch { /* Ignore */ }
                }
            }

            if (count > 0)
            {
                await manager.RunAsync(true);
            }

            return Results.Ok(new { message = $"Successfully migrated {count} files to PlumbobForge Library.", count = count, copied = count });
        });

        // Shutdown app
        app.MapPost("/api/shutdown", (IHostApplicationLifetime lifetime) =>
        {
            lifetime.StopApplication();
            return Results.Ok(new { message = "Shutting down..." });
        });
    }
}
