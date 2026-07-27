using PlumbobForge.Backend.Database;
using Microsoft.EntityFrameworkCore;
using PlumbobForge.Backend.Configuration;

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
builder.Services.AddScoped<PlumbobForge.Backend.Services.PKGManager>();

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors("AllowFrontend");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    bool hasChanges = false;
    if (!db.SetsEntities.Any(s => s.Name == "Default"))
    {
        db.SetsEntities.Add(new PlumbobForge.Backend.Database.SetsEntity { Name = "Default", FolderName = "Default", IsDefault = true });
        hasChanges = true;
    }
    
    if (!db.ConfigEntities.Any(c => c.Name == "Default"))
    {
        db.ConfigEntities.Add(new PlumbobForge.Backend.Database.ConfigEntity { Name = "Default", Active = true, Default = true });
        hasChanges = true;
    }

    if (hasChanges)
    {
        db.SaveChanges();
    }

    var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsSnapshot<PlumbobForgeOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.DocumentBaseDir))
    {
        var appSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plumbobforge-app", "appsettings.json");
        if (System.IO.File.Exists(appSettingsFile))
        {
            var jsonText = System.IO.File.ReadAllText(appSettingsFile);
            var jObject = System.Text.Json.Nodes.JsonNode.Parse(jsonText) as System.Text.Json.Nodes.JsonObject;
            if (jObject != null)
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var basePath = Path.Combine(docs, "PlumbobForge");
                
                var ccMagicNode = jObject["PlumbobForge"] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
                ccMagicNode["DocumentBaseDir"] = basePath;
                ccMagicNode["ManagedPackageFolderName"] = "Library";
                ccMagicNode["SetCacheFolderName"] = "Builds";
                
                jObject["PlumbobForge"] = ccMagicNode;
                var serializerOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(appSettingsFile, jObject.ToJsonString(serializerOptions));

                options.DocumentBaseDir = basePath;
                options.ManagedPackageFolderName = "Library";
                options.SetCacheFolderName = "Builds";
            }
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
        ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
        ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
    };

    await manager.RunAsync(true, onProgress);
    await ctx.Response.WriteAsync("data: DONE\n\n");
    await ctx.Response.Body.FlushAsync();
});

app.MapPost("/api/import-files", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    using var reader = new System.IO.StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var filePaths = System.Text.Json.JsonSerializer.Deserialize<string[]>(body);

    ctx.Response.Headers.Append("Content-Type", "text/event-stream");

    Action<string> onProgress = (msg) =>
    {
        ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
        ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
    };

    if (filePaths != null && filePaths.Length > 0)
    {
        await manager.ImportFilesAsync(filePaths, onProgress);
    }
    await ctx.Response.WriteAsync("data: DONE\n\n");
    await ctx.Response.Body.FlushAsync();
});

app.MapPost("/api/upload-files", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");

    Action<string> onProgress = (msg) =>
    {
        ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
        ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
    };

    try
    {
        var form = await ctx.Request.ReadFormAsync();
        if (form.Files.Count > 0)
        {
            await manager.UploadFilesAsync(form.Files, onProgress);
        }
    }
    catch (Exception ex)
    {
        onProgress($"CRASH: {ex.Message}");
    }

    await ctx.Response.WriteAsync("data: DONE\n\n");
    await ctx.Response.Body.FlushAsync();
}).DisableAntiforgery();

app.MapPost("/api/import-downloads", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");

    Action<string> onProgress = (msg) =>
    {
        ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
        ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
    };

    await manager.ImportFromDownloadsAsync(onProgress);
    await ctx.Response.WriteAsync("data: DONE\n\n");
    await ctx.Response.Body.FlushAsync();
});

app.MapPost("/api/fix", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");

    Action<string> onProgress = (msg) =>
    {
        ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
        ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
    };

    await manager.AutoFixAsync(onProgress);
    await ctx.Response.WriteAsync("data: DONE\n\n");
    await ctx.Response.Body.FlushAsync();
});

app.MapPost("/api/settings/recheck-types", async (HttpContext ctx, PlumbobForge.Backend.Services.PKGManager manager) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");

    Action<string> onProgress = (msg) =>
    {
        ctx.Response.WriteAsync($"data: {msg}\n\n").GetAwaiter().GetResult();
        ctx.Response.Body.FlushAsync().GetAwaiter().GetResult();
    };

    await manager.RecheckPackageTypesAsync(onProgress);
    await ctx.Response.WriteAsync("data: DONE\n\n");
    await ctx.Response.Body.FlushAsync();
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
    return Results.Ok(config);
});

app.MapDelete("/api/configurations/{id}", async (long id, AppDbContext db) =>
{
    var config = await db.ConfigEntities.FindAsync(id);
    if (config == null || config.Default || config.Active) return Results.BadRequest();
    db.ConfigEntities.Remove(config);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPut("/api/configurations/{id}", async (long id, string name, AppDbContext db) =>
{
    var config = await db.ConfigEntities.FindAsync(id);
    if (config == null) return Results.NotFound();
    config.Name = name;
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

    // If it's active, regenerate resource.cfg instantly!
    if (config.Active)
    {
        var manager = ctx.RequestServices.GetRequiredService<PlumbobForge.Backend.Services.PKGManager>();
        await manager.SyncToSims3Async();
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
    await db.SaveChangesAsync();

    var manager = ctx.RequestServices.GetRequiredService<PlumbobForge.Backend.Services.PKGManager>();
    await manager.SyncToSims3Async();

    return Results.Ok();
});

app.MapGet("/api/settings", (Microsoft.Extensions.Options.IOptionsSnapshot<PlumbobForgeOptions> options) =>
{
    return Results.Ok(options.Value);
});

app.MapPost("/api/settings", async (SaveSettingsRequest req, IWebHostEnvironment env) =>
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
                ["HasSeenWalkthrough"] = newOptions.HasSeenWalkthrough
            };

            jObject["PlumbobForge"] = ccMagicNode;

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            await System.IO.File.WriteAllTextAsync(appSettingsFile, jObject.ToJsonString(options));

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
    string[] defaultPaths = {
        @"C:\Program Files\EA Games\The Sims 3",
        @"C:\Program Files (x86)\Steam\steamapps\common\The Sims 3",
        @"C:\Games\The Sims 3"
    };

    foreach (var path in defaultPaths)
    {
        if (System.IO.Directory.Exists(path) && (System.IO.File.Exists(System.IO.Path.Combine(path, "Game", "Bin", "TS3W.exe")) || System.IO.File.Exists(System.IO.Path.Combine(path, "Game", "Bin", "TS3.exe"))))
        {
            return Results.Ok(new { path = path });
        }
    }
    return Results.Ok(new { path = "" });
});

app.MapPost("/api/settings/validate-gamefiles", (ValidateGameFilesRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Path)) return Results.Ok(new { valid = false });
    bool exists = System.IO.Directory.Exists(req.Path) && (System.IO.File.Exists(System.IO.Path.Combine(req.Path, "Game", "Bin", "TS3W.exe")) || System.IO.File.Exists(System.IO.Path.Combine(req.Path, "Game", "Bin", "TS3.exe")));
    return Results.Ok(new { valid = exists });
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

    set.Name = payload["name"].Trim();
    await db.SaveChangesAsync();
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

    await db.SaveChangesAsync();

    return Results.Ok(new { message = $"Toggled {itemsToToggle.Count} items." });
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
