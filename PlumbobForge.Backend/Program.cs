using PlumbobForge.Backend.Database;
using Microsoft.EntityFrameworkCore;
using PlumbobForge.Backend.Configuration;
using PlumbobForge.Backend.Endpoints;

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
builder.Services.AddScoped<PlumbobForge.Backend.Services.PackageTypeService>();
builder.Services.AddScoped<PlumbobForge.Backend.Services.ArchiveService>();
builder.Services.AddScoped<PlumbobForge.Backend.Services.ThumbnailService>();
builder.Services.AddScoped<PlumbobForge.Backend.Services.CacheBuilderService>();
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

// Register Modular Endpoint Groups
app.MapSetEndpoints();
app.MapItemEndpoints();
app.MapConfigurationEndpoints();
app.MapSettingsEndpoints(appSettingsPath);
app.MapMaintenanceEndpoints();

app.Run();
