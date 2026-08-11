using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlumbobForge.Backend.Database;
using PlumbobForge.Backend.Services;

namespace PlumbobForge.Backend.Endpoints;

public static class SetEndpoints
{
    public static void MapSetEndpoints(this IEndpointRouteBuilder app)
    {
        // Fetch sets hierarchy
        app.MapGet("/api/sets", async (AppDbContext db) =>
        {
            return await db.SetsEntities.Include(s => s.Children).ToListAsync();
        });

        // Create set
        app.MapPost("/api/sets", async (AppDbContext db, HttpContext context) =>
        {
            var dto = await context.Request.ReadFromJsonAsync<SetsEntity>();
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest("Invalid set data");

            dto.Id = 0; // Ensure EF treats as new

            if (string.IsNullOrWhiteSpace(dto.FolderName))
            {
                var invalidChars = Path.GetInvalidFileNameChars();
                dto.FolderName = new string(dto.Name.Where(c => !invalidChars.Contains(c)).ToArray());
                if (string.IsNullOrWhiteSpace(dto.FolderName)) dto.FolderName = $"Set_{Guid.NewGuid().ToString().Substring(0, 8)}";
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
                    var manager = context.RequestServices.GetRequiredService<PKGManager>();
                    await manager.SyncToSims3Async();
                }
            }

            return Results.Ok(dto);
        });

        // Move set (Reparenting with cycle detection)
        app.MapPut("/api/sets/{id}/move", async (long id, AppDbContext db, HttpContext context) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, long?>>();
            if (payload == null || !payload.ContainsKey("parentSetsEntityId")) return Results.BadRequest();

            var set = await db.SetsEntities.FindAsync(id);
            if (set == null) return Results.NotFound();

            var newParentId = payload["parentSetsEntityId"];
            if (newParentId.HasValue)
            {
                if (newParentId.Value == id)
                {
                    return Results.BadRequest(new { message = "A set cannot be a parent of itself." });
                }

                var allSets = await db.SetsEntities.ToListAsync();
                var visited = new HashSet<long> { id };
                var current = allSets.FirstOrDefault(s => s.Id == newParentId.Value);

                while (current != null)
                {
                    if (visited.Contains(current.Id))
                    {
                        return Results.BadRequest(new { message = "Cannot move a set inside one of its own subsets." });
                    }
                    visited.Add(current.Id);
                    current = current.ParentSetsEntityId.HasValue
                        ? allSets.FirstOrDefault(s => s.Id == current.ParentSetsEntityId.Value)
                        : null;
                }
            }

            set.ParentSetsEntityId = newParentId;
            await db.SaveChangesAsync();
            return Results.Ok(set);
        });

        // Rename set
        app.MapPut("/api/sets/{id}", async (long id, AppDbContext db, HttpContext context) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<Dictionary<string, string>>();
            if (payload == null || !payload.ContainsKey("name") || string.IsNullOrWhiteSpace(payload["name"])) return Results.BadRequest();

            var set = await db.SetsEntities.FindAsync(id);
            if (set == null) return Results.NotFound();

            var manager = context.RequestServices.GetRequiredService<PKGManager>();

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

        // Delete set
        app.MapDelete("/api/sets/{id}", async (long id, bool deleteItems, AppDbContext db, HttpContext context) =>
        {
            var set = await db.SetsEntities.FindAsync(id);
            if (set == null) return Results.NotFound();

            if (set.Name == "Default" || set.Name == "Legacy")
                return Results.BadRequest(new { message = "Cannot delete built-in sets." });

            var allSets = await db.SetsEntities.Include(s => s.Children).ToListAsync();
            var setsToDelete = new List<SetsEntity>();
            var visitedSetIds = new HashSet<long>();

            void CollectSets(SetsEntity current)
            {
                if (visitedSetIds.Contains(current.Id)) return;
                visitedSetIds.Add(current.Id);
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
                foreach (var item in itemsInSets)
                {
                    item.SetsEntityId = null;
                }
            }

            var defaultSet = allSets.FirstOrDefault(s => s.Name == "Default");
            if (defaultSet != null) defaultSet.Dirty = true;

            db.SetsEntities.RemoveRange(setsToDelete);
            await db.SaveChangesAsync();

            var manager = context.RequestServices.GetRequiredService<PKGManager>();
            await manager.SyncToSims3Async();

            return Results.Ok(new { message = $"Deleted {setsToDelete.Count} sets and handled {itemsInSets.Count} items." });
        });
    }
}
