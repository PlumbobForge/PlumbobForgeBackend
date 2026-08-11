using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlumbobForge.Backend.Database;
using PlumbobForge.Backend.Services;

namespace PlumbobForge.Backend.Endpoints;

public class ConfigSetsUpdateDto { public List<long> SetIds { get; set; } = new(); }

public static class ConfigurationEndpoints
{
    public static void MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        // Get configurations
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

        // Create configuration
        app.MapPost("/api/configurations", async (AppDbContext db, HttpContext ctx) =>
        {
            var config = new ConfigEntity { Name = "New Configuration", Active = false, Default = false };
            db.ConfigEntities.Add(config);
            await db.SaveChangesAsync();

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

        // Duplicate configuration
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

        // Delete configuration
        app.MapDelete("/api/configurations/{id}", async (long id, AppDbContext db) =>
        {
            var config = await db.ConfigEntities.FindAsync(id);
            if (config == null || config.Default || config.Active) return Results.BadRequest();
            db.ConfigEntities.Remove(config);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Update configuration name/description
        app.MapPut("/api/configurations/{id}", async (long id, string? name, string? description, AppDbContext db) =>
        {
            var config = await db.ConfigEntities.FindAsync(id);
            if (config == null) return Results.NotFound();
            if (name != null) config.Name = name;
            if (description != null) config.Description = description;
            await db.SaveChangesAsync();
            return Results.Ok(config);
        });

        // Update configuration sets
        app.MapPut("/api/configurations/{id}/sets", async (long id, ConfigSetsUpdateDto dto, AppDbContext db, HttpContext ctx) =>
        {
            var config = await db.ConfigEntities
                .Include(c => c.ConfigSetsEntities)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (config == null) return Results.NotFound();

            config.ConfigSetsEntities.Clear();
            foreach (var setId in dto.SetIds)
            {
                config.ConfigSetsEntities.Add(new ConfigSetsEntity { ConfigEntityId = id, SetsEntityId = setId });
            }

            await db.SaveChangesAsync();

            if (config.Active)
            {
                var allSets = await db.SetsEntities.ToListAsync();
                foreach (var s in allSets)
                {
                    s.Dirty = true;
                }
                await db.SaveChangesAsync();

                var manager = ctx.RequestServices.GetRequiredService<PKGManager>();
                await manager.SyncToSims3Async(null, forceRebuildStatic: true);
            }

            return Results.Ok();
        });

        // Activate configuration
        app.MapPut("/api/configurations/{id}/active", async (long id, AppDbContext db, HttpContext ctx) =>
        {
            var allConfigs = await db.ConfigEntities.ToListAsync();
            foreach (var c in allConfigs)
            {
                c.Active = c.Id == id;
            }

            var allSets = await db.SetsEntities.ToListAsync();
            foreach (var s in allSets)
            {
                s.Dirty = true;
            }

            await db.SaveChangesAsync();

            var manager = ctx.RequestServices.GetRequiredService<PKGManager>();
            await manager.SyncToSims3Async(null, forceRebuildStatic: true);

            return Results.Ok();
        });
    }
}
