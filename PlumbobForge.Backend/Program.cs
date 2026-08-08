using PlumbobForge.Backend.Database;
using Microsoft.EntityFrameworkCore;
using PlumbobForge.Backend.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plumbobforge-app");
if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);

var appSettingsPath = Path.Combine(appDataPath, "appsettings.json");
if (!System.IO.File.Exists(appSettingsPath))
{
    System.IO.File.WriteAllText(appSettingsPath, "{\n  \"PlumbobForge\": {}\n}");
}

builder.Configuration.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null; // Unlimited for large Sims3Packs
});

// Add Configuration
builder.Services.Configure<PlumbobForgeOptions>(
    builder.Configuration.GetSection(PlumbobForgeOptions.SectionName));

// Add services to the container.
builder.Services.AddSingleton<PlumbobForge.Backend.Services.NotificationService>();
builder.Services.AddScoped<PlumbobForge.Backend.Services.LocalizationService>();
builder.Services.AddScoped<PlumbobForge.Backend.Services.PKGManager>();
builder.Services.AddHostedService<PlumbobForge.Backend.Services.DownloadsWatcherService>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(appDataPath, "plumbobforge.db")}"));

// Swagger/OpenAPI registered only in Development mode to avoid reflection scanning overhead in Production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

app.UseCors("AllowFrontend");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Enable Write-Ahead Logging (WAL) for faster SQLite operations
    try { await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;"); } catch { }

    var defaultSet = await db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
    if (defaultSet == null)
    {
        defaultSet = new PlumbobForge.Backend.Database.SetsEntity { Name = "Default", FolderName = "Default", IsDefault = true };
        db.SetsEntities.Add(defaultSet);
        await db.SaveChangesAsync();
    }

    var defaultConfig = await db.ConfigEntities.Include(c => c.ConfigSetsEntities).FirstOrDefaultAsync(c => c.Name == "Default" || c.Default);
    if (defaultConfig == null)
    {
        defaultConfig = new PlumbobForge.Backend.Database.ConfigEntity { Name = "Default", Active = true, Default = true };
        db.ConfigEntities.Add(defaultConfig);
        await db.SaveChangesAsync();

        var allSets = await db.SetsEntities.ToListAsync();
        foreach (var set in allSets)
        {
            db.ConfigSetsEntities.Add(new PlumbobForge.Backend.Database.ConfigSetsEntity { ConfigEntityId = defaultConfig.Id, SetsEntityId = set.Id });
        }
        await db.SaveChangesAsync();
    }

    var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<PlumbobForgeOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.DocumentBaseDir))
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var basePath = Path.Combine(docs, "PlumbobForge");

        if (System.IO.File.Exists(appSettingsPath))
        {
            try
            {
                var jsonText = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                var jObject = System.Text.Json.Nodes.JsonNode.Parse(jsonText) as System.Text.Json.Nodes.JsonObject;
                if (jObject != null)
                {
                    var ccMagicNode = jObject["PlumbobForge"] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
                    ccMagicNode["DocumentBaseDir"] = basePath;
                    ccMagicNode["ManagedPackageFolderName"] = "Library";
                    ccMagicNode["SetCacheFolderName"] = "Builds";

                    jObject["PlumbobForge"] = ccMagicNode;
                    var serializerOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    await System.IO.File.WriteAllTextAsync(appSettingsPath, jObject.ToJsonString(serializerOptions));
                }
            }
            catch { }
        }

        options.DocumentBaseDir = basePath;
        options.ManagedPackageFolderName = "Library";
        options.SetCacheFolderName = "Builds";
    }

    // Re-derive all MetaEntity.CompleteFileName paths from current DocumentBaseDir
    // This handles the case where the user manually moved the PlumbobForge folder
    if (!string.IsNullOrWhiteSpace(options.ManagedPackageFolderName))
    {
        var managedPath = Path.Combine(options.DocumentBaseDir, options.ManagedPackageFolderName);
        var allItems = await db.MetaEntities.ToListAsync();
        bool anyChanged = false;
        foreach (var item in allItems)
        {
            var expectedPath = Path.Combine(managedPath, item.FileName);
            if (!string.Equals(item.CompleteFileName, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                item.CompleteFileName = expectedPath;
                anyChanged = true;
            }
        }
        if (anyChanged)
        {
            await db.SaveChangesAsync();
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/sets", async (AppDbContext db) =>
{
    return await db.SetsEntities.Include(s => s.Children).ToListAsync();
});

app.MapGet("/api/items", async (AppDbContext db) =>
{
    return await db.MetaEntities.ToListAsync();
});

app.MapGet("/api/items/{id}/thumbnail", async (long id, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    var thumbPath = await manager.GetThumbnailPathAsync(id);
    if (thumbPath == null) return Results.NoContent();

    // We stream the raw bytes with a generic image content type so the browser can sniff it.
    return Results.File(thumbPath, "image/png");
});

app.MapPost("/api/scan", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
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

app.MapPost("/api/import/check-duplicates", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    if (ctx.Request.HasFormContentType)
    {
        var form = await ctx.Request.ReadFormAsync();
        var duplicates = manager.CheckFormDuplicates(form.Files);
        return Results.Ok(new { hasDuplicates = duplicates.Count > 0, duplicates });
    }
    else
    {
        using var reader = new System.IO.StreamReader(ctx.Request.Body);
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

app.MapPost("/api/import-files", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    using var reader = new System.IO.StreamReader(ctx.Request.Body);
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

app.MapPost("/api/upload-files", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
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

app.MapGet("/api/notifications/stream", async (HttpContext ctx, PlumbobForge.Backend.Services.NotificationService notifier, CancellationToken cancellationToken) =>
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

app.MapGet("/api/import-downloads/check-duplicates", (PlumbobForge.Backend.Services.PKGManager manager) =>
{
    var duplicates = manager.CheckDownloadsDuplicates();
    return Results.Ok(new { hasDuplicates = duplicates.Count > 0, duplicates });
});

app.MapPost("/api/import-downloads", async (HttpContext ctx, string? duplicateAction, PlumbobForge.Backend.Services.PKGManager manager, PlumbobForge.Backend.Services.NotificationService notifier) =>
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

app.MapPost("/api/fix", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
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

app.MapPost("/api/settings/recheck-types", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
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

app.MapGet("/api/configurations", async (AppDbContext db) =>
{
    var configs = await db.ConfigEntities
        .Include(c => c.ConfigSetsEntities)
        .ToListAsync();

    var result = configs.Select(c => new
    {
        c.Id,
        c.Name,
        c.Description,
        c.Default,
        c.Active,
        SetIds = c.ConfigSetsEntities.Select(cs => cs.SetsEntityId).ToList()
    });

    return Results.Ok(result);
});

app.MapPost("/api/configurations", async (AppDbContext db, HttpContext ctx) =>
{
    var config = new ConfigEntity { Name = "New Configuration", Active = false, Default = false };
    db.ConfigEntities.Add(config);
    await db.SaveChangesAsync();

    // Enable all existing sets by default in the new configuration
    var allSets = await db.SetsEntities.ToListAsync();
    foreach (var set in allSets)
    {
        config.ConfigSetsEntities.Add(new ConfigSetsEntity { ConfigEntityId = config.Id, SetsEntityId = set.Id });
    }
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        config.Id,
        config.Name,
        config.Description,
        config.Default,
        config.Active,
        SetIds = config.ConfigSetsEntities.Select(cs => cs.SetsEntityId).ToList()
    });
});

app.MapPost("/api/configurations/{id}/duplicate", async (long id, AppDbContext db) =>
{
    var sourceConfig = await db.ConfigEntities
        .Include(c => c.ConfigSetsEntities)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (sourceConfig == null) return Results.NotFound();

    string newName = $"{sourceConfig.Name} (Copy)";
    int copyCount = 1;
    while (await db.ConfigEntities.AnyAsync(c => c.Name == newName))
    {
        copyCount++;
        newName = $"{sourceConfig.Name} (Copy {copyCount})";
    }

    var newConfig = new ConfigEntity
    {
        Name = newName,
        Description = sourceConfig.Description,
        Active = false,
        Default = false
    };
    db.ConfigEntities.Add(newConfig);
    await db.SaveChangesAsync();

    foreach (var cs in sourceConfig.ConfigSetsEntities)
    {
        newConfig.ConfigSetsEntities.Add(new ConfigSetsEntity
        {
            ConfigEntityId = newConfig.Id,
            SetsEntityId = cs.SetsEntityId
        });
    }
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        newConfig.Id,
        newConfig.Name,
        newConfig.Description,
        newConfig.Default,
        newConfig.Active,
        SetIds = newConfig.ConfigSetsEntities.Select(cs => cs.SetsEntityId).ToList()
    });
});

app.MapDelete("/api/configurations/{id}", async (long id, AppDbContext db) =>
{
    var config = await db.ConfigEntities.FindAsync(id);
    if (config == null || config.Default || config.Active) return Results.BadRequest();
    db.ConfigEntities.Remove(config);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPut("/api/configurations/{id}", async (long id, string? name, string? description, AppDbContext db) =>
{
    var config = await db.ConfigEntities.FindAsync(id);
    if (config == null) return Results.NotFound();
    if (name != null) config.Name = name;
    if (description != null) config.Description = description;
    await db.SaveChangesAsync();
    return Results.Ok(config);
});

app.MapPut("/api/configurations/{id}/sets", async (long id, ConfigSetsUpdateDto dto, AppDbContext db, HttpContext ctx) =>
{
    var config = await db.ConfigEntities
        .Include(c => c.ConfigSetsEntities)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (config == null) return Results.NotFound();

    config.ConfigSetsEntities.Clear();
    foreach(var setId in dto.SetIds)
    {
        config.ConfigSetsEntities.Add(new ConfigSetsEntity { ConfigEntityId = id, SetsEntityId = setId });
    }

    await db.SaveChangesAsync();

    // If it's active, mark sets dirty and regenerate resource.cfg!
    if (config.Active)
    {
        var allSets = await db.SetsEntities.ToListAsync();
        foreach (var s in allSets)
        {
            s.Dirty = true;
        }
        await db.SaveChangesAsync();

        var manager = ctx.RequestServices.GetRequiredService<PlumbobForge.Backend.Services.PKGManager>();
        await manager.SyncToSims3Async(null, forceRebuildStatic: true);
    }

    return Results.Ok();
});

app.MapPut("/api/configurations/{id}/active", async (long id, AppDbContext db, HttpContext ctx) =>
{
    var allConfigs = await db.ConfigEntities.ToListAsync();
    foreach(var c in allConfigs)
    {
        c.Active = c.Id == id;
    }

    var allSets = await db.SetsEntities.ToListAsync();
    foreach (var s in allSets)
    {
        s.Dirty = true;
    }

    await db.SaveChangesAsync();

    var manager = ctx.RequestServices.GetRequiredService<PlumbobForge.Backend.Services.PKGManager>();
    await manager.SyncToSims3Async(null, forceRebuildStatic: true);

    return Results.Ok();
});

app.MapGet("/api/settings", (Microsoft.Extensions.Options.IOptionsSnapshot<PlumbobForgeOptions> options) =>
{
    return Results.Ok(options.Value);
});

app.MapPost("/api/settings", async (SaveSettingsRequest req, IWebHostEnvironment env, AppDbContext db) =>
{
    var newOptions = req.Options;
    var appSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plumbobforge-app", "appsettings.json");
    if (System.IO.File.Exists(appSettingsFile))
    {
        var jsonText = await System.IO.File.ReadAllTextAsync(appSettingsFile);
        var jObject = System.Text.Json.Nodes.JsonNode.Parse(jsonText) as System.Text.Json.Nodes.JsonObject;

        if (jObject != null)
        {
            // Extract old DocumentBaseDir
            var oldBaseDir = jObject["PlumbobForge"]?["DocumentBaseDir"]?.ToString();

            // Hardcode subfolders
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
                ["ObservedFolders"] = jsonObserved
            };

            jObject["PlumbobForge"] = ccMagicNode;

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            await System.IO.File.WriteAllTextAsync(appSettingsFile, jObject.ToJsonString(options));

            try
            {
                var watcherService = app.Services.GetService<PlumbobForge.Backend.Services.DownloadsWatcherService>();
                watcherService?.ReloadWatchers();
            }
            catch { }

            // Perform directory move if requested
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
                    catch { /* If it fails, we gracefully continue with settings saved */ }
                }
            }

            // Update all MetaEntity.CompleteFileName paths when DocumentBaseDir changes
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

app.MapPost("/api/settings/autodetect", () =>
{
    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    var basePath = Path.Combine(docs, "PlumbobForge");

    var autoOptions = new PlumbobForgeOptions
    {
        DocumentBaseDir = basePath,
        DownloadFolderName = string.Empty, // unused
        ArchiveFolderName = string.Empty, // unused
        TS3PackFolderName = string.Empty, // unused
        ManagedPackageFolderName = "Library",
        SetCacheFolderName = "Builds",
        LegacyPackageFolderName = string.Empty, // unused
        TS3PackStoreFolderName = string.Empty // unused
    };

    return Results.Ok(autoOptions);
});

app.MapGet("/api/settings/autodetect-gamefiles", () =>
{
    string detectedPath = PlumbobForge.Backend.Services.GamePathValidator.AutodetectGameFilesPath();
    return Results.Ok(new { path = detectedPath });
});

app.MapPost("/api/settings/validate-gamefiles", (ValidateGameFilesRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Path)) return Results.Ok(new { valid = false, normalizedPath = "" });
    var (valid, normalizedPath) = PlumbobForge.Backend.Services.GamePathValidator.ValidateAndNormalize(req.Path);
    return Results.Ok(new { valid, normalizedPath });
});

app.MapPost("/api/migrate", async (Microsoft.Extensions.Options.IOptionsSnapshot<PlumbobForgeOptions> options, PlumbobForge.Backend.Database.AppDbContext db, PlumbobForge.Backend.Services.PKGManager manager) =>
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
        if (!System.IO.File.Exists(dest))
        {
            System.IO.File.Move(file, dest);
            count++;
        }
        else
        {
            try { System.IO.File.Delete(file); } catch { /* Ignore */ }
        }
    }

    if (count > 0)
    {
        await manager.RunAsync(true);
    }

    return Results.Ok(new { message = $"Successfully migrated {count} files to PlumbobForge Library.", count = count, copied = count });
});

// Create Set
app.MapPost("/api/sets", async (AppDbContext db, HttpContext context) =>
{
    var dto = await context.Request.ReadFromJsonAsync<PlumbobForge.Backend.Database.SetsEntity>();
    if (dto == null || string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest("Invalid set data");

    dto.Id = 0; // Ensure EF treats as new

    if (string.IsNullOrWhiteSpace(dto.FolderName))
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        dto.FolderName = new string(dto.Name.Where(c => !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(dto.FolderName)) dto.FolderName = $"Set_{Guid.NewGuid().ToString().Substring(0,8)}";
    }

    db.SetsEntities.Add(dto);
    await db.SaveChangesAsync();

    // Enable new set by default in the Default configuration
    var defaultConfig = await db.ConfigEntities
        .Include(c => c.ConfigSetsEntities)
        .FirstOrDefaultAsync(c => c.Default);

    if (defaultConfig != null && !defaultConfig.ConfigSetsEntities.Any(cs => cs.SetsEntityId == dto.Id))
    {
        db.ConfigSetsEntities.Add(new ConfigSetsEntity { ConfigEntityId = defaultConfig.Id, SetsEntityId = dto.Id });
        await db.SaveChangesAsync();

        if (defaultConfig.Active)
        {
            var manager = context.RequestServices.GetRequiredService<PlumbobForge.Backend.Services.PKGManager>();
            await manager.SyncToSims3Async();
        }
    }

    return Results.Ok(dto);
});

// Move Set (Nesting)
app.MapPut("/api/sets/{id}/move", async (long id, AppDbContext db, HttpContext context) =>
{
    var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, long?>>();
    if (payload == null || !payload.ContainsKey("parentSetsEntityId")) return Results.BadRequest();

    var set = await db.SetsEntities.FindAsync(id);
    if (set == null) return Results.NotFound();

    set.ParentSetsEntityId = payload["parentSetsEntityId"];
    await db.SaveChangesAsync();
    return Results.Ok(set);
});

// Rename Set
app.MapPut("/api/sets/{id}", async (long id, AppDbContext db, HttpContext context) =>
{
    var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, string>>();
    if (payload == null || !payload.ContainsKey("name") || string.IsNullOrWhiteSpace(payload["name"])) return Results.BadRequest();

    var set = await db.SetsEntities.FindAsync(id);
    if (set == null) return Results.NotFound();

    var manager = context.RequestServices.GetRequiredService<PlumbobForge.Backend.Services.PKGManager>();

    // Compute old and new folder paths before renaming
    string oldFolderName = set.FolderName ?? set.Name;
    string newName = payload["name"].Trim();
    var invalidChars = Path.GetInvalidFileNameChars();
    string newFolderName = new string(newName.Where(c => !invalidChars.Contains(c)).ToArray());
    if (string.IsNullOrWhiteSpace(newFolderName)) newFolderName = $"Set_{set.Id}";

    // Rename the cache folder on disk if it exists
    string oldCachePath = manager.GetSetCachePath(oldFolderName);
    string newCachePath = manager.GetSetCachePath(newFolderName);
    if (Directory.Exists(oldCachePath) && oldCachePath != newCachePath)
    {
        try
        {
            // If the destination already exists (e.g. from a previous rename), merge by moving contents
            if (Directory.Exists(newCachePath))
            {
                foreach (var file in Directory.GetFiles(oldCachePath))
                {
                    string dest = Path.Combine(newCachePath, Path.GetFileName(file));
                    try { File.Move(file, dest, overwrite: true); } catch { }
                }
                try { Directory.Delete(oldCachePath, true); } catch { }
            }
            else
            {
                Directory.Move(oldCachePath, newCachePath);
            }
        }
        catch { /* Best-effort rename; sync will correct it */ }
    }

    set.Name = newName;
    set.FolderName = newFolderName;
    await db.SaveChangesAsync();

    await manager.SyncToSims3Async();

    return Results.Ok(set);
});

// Delete Set (Recursive)
app.MapDelete("/api/sets/{id}", async (long id, bool deleteItems, AppDbContext db, HttpContext context) =>
{
    var set = await db.SetsEntities.FindAsync(id);
    if (set == null) return Results.NotFound();

    // We cannot delete Default or Legacy
    if (set.Name == "Default" || set.Name == "Legacy")
        return Results.BadRequest(new { message = "Cannot delete built-in sets." });

    // Recursively collect all subsets
    var allSets = await db.SetsEntities.Include(s => s.Children).ToListAsync();
    var setsToDelete = new List<SetsEntity>();

    void CollectSets(SetsEntity current)
    {
        setsToDelete.Add(current);
        foreach (var child in allSets.Where(s => s.ParentSetsEntityId == current.Id))
        {
            CollectSets(child);
        }
    }
    CollectSets(set);

    var setIdsToDelete = setsToDelete.Select(s => s.Id).ToList();
    var itemsInSets = await db.MetaEntities.Where(m => m.SetsEntityId.HasValue && setIdsToDelete.Contains(m.SetsEntityId.Value)).ToListAsync();

    if (deleteItems)
    {
        // Delete physical files
        foreach (var item in itemsInSets)
        {
            if (System.IO.File.Exists(item.CompleteFileName))
            {
                try { System.IO.File.Delete(item.CompleteFileName); } catch { /* Ignore locked files */ }
            }
        }
        db.MetaEntities.RemoveRange(itemsInSets);
    }
    else
    {
        // Move items to 'All Items'
        foreach (var item in itemsInSets)
        {
            item.SetsEntityId = null;
        }
    }

    // Mark default set as dirty if items were moved or deleted, to rebuild cache
    var defaultSet = allSets.FirstOrDefault(s => s.Name == "Default");
    if (defaultSet != null) defaultSet.Dirty = true;

    db.SetsEntities.RemoveRange(setsToDelete);
    await db.SaveChangesAsync();

    var manager = context.RequestServices.GetRequiredService<PlumbobForge.Backend.Services.PKGManager>();
    await manager.SyncToSims3Async();

    return Results.Ok(new { message = $"Deleted {setsToDelete.Count} sets and handled {itemsInSets.Count} items." });
});

// Move Items
app.MapPut("/api/items/move", async (AppDbContext db, HttpContext context) =>
{
    var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
    if (payload == null || !payload.ContainsKey("itemIds") || !payload.ContainsKey("targetSetId")) return Results.BadRequest();

    var targetSetId = payload["targetSetId"]?.ToString();
    long? parsedTargetId = null;
    if (long.TryParse(targetSetId, out long tempId)) { parsedTargetId = tempId; }

    var itemIdsObj = payload["itemIds"] as System.Text.Json.JsonElement?;
    if (!itemIdsObj.HasValue || itemIdsObj.Value.ValueKind != System.Text.Json.JsonValueKind.Array) return Results.BadRequest();

    var itemIds = itemIdsObj.Value.EnumerateArray().Select(x => x.GetInt64()).ToList();

    var itemsToMove = await db.MetaEntities.Where(m => itemIds.Contains(m.Id)).ToListAsync();
    foreach (var item in itemsToMove)
    {
        if (item.SetsEntityId.HasValue)
        {
            var oldSet = await db.SetsEntities.FindAsync(item.SetsEntityId.Value);
            if (oldSet != null) oldSet.Dirty = true;
        }

        item.SetsEntityId = parsedTargetId;
    }

    if (parsedTargetId.HasValue)
    {
        var newSet = await db.SetsEntities.FindAsync(parsedTargetId.Value);
        if (newSet != null) newSet.Dirty = true;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = $"Moved {itemsToMove.Count} items." });
});

// Enable/Disable Items
app.MapPut("/api/items/enabled", async (AppDbContext db, HttpContext context) =>
{
    var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
    if (payload == null || !payload.ContainsKey("itemIds") || !payload.ContainsKey("enabled")) return Results.BadRequest();

    var itemIdsObj = payload["itemIds"] as System.Text.Json.JsonElement?;
    if (!itemIdsObj.HasValue || itemIdsObj.Value.ValueKind != System.Text.Json.JsonValueKind.Array) return Results.BadRequest();

    var itemIds = itemIdsObj.Value.EnumerateArray().Select(x => x.GetInt64()).ToList();
    var enabledObj = payload["enabled"] as System.Text.Json.JsonElement?;
    var isEnabled = enabledObj?.GetBoolean() ?? true;

    var itemsToToggle = await db.MetaEntities.Where(m => itemIds.Contains(m.Id)).ToListAsync();
    foreach (var item in itemsToToggle)
    {
        item.Enabled = isEnabled;
        if (item.SetsEntityId.HasValue)
        {
            var set = await db.SetsEntities.FindAsync(item.SetsEntityId.Value);
            if (set != null) set.Dirty = true;
        }
        else
        {
            var defaultSet = await db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
            if (defaultSet != null) defaultSet.Dirty = true;
        }
    }

    return Results.Ok(new { message = $"Toggled {itemsToToggle.Count} items." });
});

// Retag Items
app.MapPut("/api/items/retag", async (RetagItemsDto dto, AppDbContext db) =>
{
    if (dto == null || dto.ItemIds == null || dto.ItemIds.Count == 0)
        return Results.BadRequest();

    var items = await db.MetaEntities.Where(m => dto.ItemIds.Contains(m.Id)).ToListAsync();
    foreach (var item in items)
    {
        item.PackageType = dto.PackageType ?? "Other";
        item.CASCategories = dto.PackageType == "CAS" ? (dto.CasCategories ?? "") : "";
        item.IsUserTagged = true;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = $"Retagged {items.Count} items." });
});

// Update User Tags
app.MapPut("/api/items/user-tags", async (UpdateUserTagsDto dto, AppDbContext db) =>
{
    if (dto == null || dto.ItemIds == null || dto.ItemIds.Count == 0)
        return Results.BadRequest();

    var items = await db.MetaEntities.Where(m => dto.ItemIds.Contains(m.Id)).ToListAsync();
    foreach (var item in items)
    {
        if (dto.RemoveAll)
        {
            item.UserTags = "";
        }
        else if (dto.SetTags != null)
        {
            item.UserTags = string.Join(",", dto.SetTags.Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase));
        }
        else if (dto.AddTags != null && dto.AddTags.Length > 0)
        {
            var currentTags = (item.UserTags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            foreach (var newTag in dto.AddTags)
            {
                string cleanTag = newTag.Trim();
                if (!string.IsNullOrWhiteSpace(cleanTag) && !currentTags.Contains(cleanTag, StringComparer.OrdinalIgnoreCase))
                {
                    currentTags.Add(cleanTag);
                }
            }
            item.UserTags = string.Join(",", currentTags);
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = $"Updated user tags for {items.Count} items." });
});

// Rename Item
app.MapPut("/api/items/{id}/rename", async (long id, AppDbContext db, HttpContext context) =>
{
    var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, string>>();
    if (payload == null || !payload.ContainsKey("newName")) return Results.BadRequest();

    var newName = payload["newName"];
    if (string.IsNullOrWhiteSpace(newName)) return Results.BadRequest("Name cannot be empty.");

    var item = await db.MetaEntities.FindAsync(id);
    if (item == null) return Results.NotFound();

    var directory = Path.GetDirectoryName(item.CompleteFileName);
    var extension = Path.GetExtension(item.CompleteFileName);

    var safeName = string.Join("_", newName.Split(Path.GetInvalidFileNameChars()));
    if (!safeName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
    {
        safeName += extension;
    }

    if (directory != null)
    {
        var newCompleteFileName = Path.Combine(directory, safeName);
        if (item.CompleteFileName != newCompleteFileName)
        {
            if (File.Exists(newCompleteFileName)) return Results.Conflict(new { message = "A file with this name already exists." });
            if (File.Exists(item.CompleteFileName))
            {
                File.Move(item.CompleteFileName, newCompleteFileName);
            }
            item.FileName = safeName;
            item.CompleteFileName = newCompleteFileName;
        }
    }

    if (item.SetsEntityId.HasValue)
    {
        var set = await db.SetsEntities.FindAsync(item.SetsEntityId.Value);
        if (set != null) set.Dirty = true;
    }

    await db.SaveChangesAsync();
    return Results.Ok(item);
});

// Delete Items
app.MapDelete("/api/items", async (AppDbContext db, HttpContext context) =>
{
    var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
    if (payload == null || !payload.ContainsKey("itemIds")) return Results.BadRequest();

    bool permanent = false;
    if (payload.TryGetValue("permanent", out var permObj) && permObj is System.Text.Json.JsonElement permElement)
    {
        if (permElement.ValueKind == System.Text.Json.JsonValueKind.True) permanent = true;
    }

    var itemIdsObj = payload["itemIds"] as System.Text.Json.JsonElement?;
    if (!itemIdsObj.HasValue || itemIdsObj.Value.ValueKind != System.Text.Json.JsonValueKind.Array) return Results.BadRequest();

    var itemIds = itemIdsObj.Value.EnumerateArray().Select(x => x.GetInt64()).ToList();

    var itemsToDelete = await db.MetaEntities.Where(m => itemIds.Contains(m.Id)).ToListAsync();
    int count = 0;

    // Mark sets as dirty
    var setIdsToMark = itemsToDelete.Where(m => m.SetsEntityId.HasValue).Select(m => m.SetsEntityId!.Value).Distinct().ToList();
    var setsToMark = await db.SetsEntities.Where(s => setIdsToMark.Contains(s.Id)).ToListAsync();
    foreach (var s in setsToMark) s.Dirty = true;

    // Default set always dirty on deletion
    var defaultSet = await db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
    if (defaultSet != null) defaultSet.Dirty = true;

    foreach (var item in itemsToDelete)
    {
        if (System.IO.File.Exists(item.CompleteFileName))
        {
            if (permanent)
            {
                try { System.IO.File.Delete(item.CompleteFileName); } catch { /* Ignore */ }
            }
            else
            {
                PlumbobForge.Backend.Services.RecycleBinHelper.SendToRecycleBin(item.CompleteFileName);
            }
        }
        count++;
    }

    db.MetaEntities.RemoveRange(itemsToDelete);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = $"Deleted {count} items." });
});

app.MapPost("/api/shutdown", (IHostApplicationLifetime lifetime) =>
{
    lifetime.StopApplication();
    return Results.Ok(new { message = "Shutting down..." });
});

app.Run();

public class ConfigSetsUpdateDto { public List<long> SetIds { get; set; } = new(); }

public class ValidateGameFilesRequest { public string Path { get; set; } = ""; }

public record SaveSettingsRequest(PlumbobForgeOptions Options, bool MoveFolder);

public record RetagItemsDto(List<long> ItemIds, string PackageType, string CasCategories);

public record UpdateUserTagsDto(List<long> ItemIds, string[]? SetTags, string[]? AddTags, bool RemoveAll);

public record CheckDuplicatesRequest(string[] FileNames);

public record ImportFilesRequest(string[] FilePaths, string? DuplicateAction, long? TargetSetId);
