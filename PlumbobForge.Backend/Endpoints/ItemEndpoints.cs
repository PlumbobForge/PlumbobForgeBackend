using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlumbobForge.Backend.Database;
using PlumbobForge.Backend.Services;

namespace PlumbobForge.Backend.Endpoints;

public record RetagItemsDto(List<long> ItemIds, string PackageType, string CasCategories);
public record UpdateUserTagsDto(List<long> ItemIds, string[]? SetTags, string[]? AddTags, bool RemoveAll);

public static class ItemEndpoints
{
    public static void MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        // Get items (with optional server-side filtering, search, sorting & pagination)
        app.MapGet("/api/items", async (
            AppDbContext db,
            long? setId,
            string? search,
            string? packageType,
            bool? enabled,
            string? sortBy,
            int? page,
            int? pageSize) =>
        {
            var query = db.MetaEntities.AsQueryable();

            if (setId.HasValue)
            {
                if (setId.Value == -1)
                {
                    query = query.Where(m => m.SetsEntityId == null);
                }
                else
                {
                    query = query.Where(m => m.SetsEntityId == setId.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(packageType))
            {
                query = query.Where(m => m.PackageType == packageType);
            }

            if (enabled.HasValue)
            {
                query = query.Where(m => m.Enabled == enabled.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(m =>
                    EF.Functions.Like(m.FileName, pattern) ||
                    (m.Description != null && EF.Functions.Like(m.Description, pattern)) ||
                    (m.UserTags != null && EF.Functions.Like(m.UserTags, pattern)));
            }

            // Sorting
            query = sortBy switch
            {
                "date_asc" => query.OrderBy(m => m.Id),
                "alpha_asc" => query.OrderBy(m => m.FileName),
                "alpha_desc" => query.OrderByDescending(m => m.FileName),
                _ => query.OrderByDescending(m => m.Id) // default: date_desc
            };

            if (page.HasValue && pageSize.HasValue && pageSize.Value > 0)
            {
                int totalCount = await query.CountAsync();
                double totalSize = await query.SumAsync(m => m.FileSize);
                int p = Math.Max(1, page.Value);
                int ps = Math.Clamp(pageSize.Value, 1, 10000);

                var items = await query.Skip((p - 1) * ps).Take(ps).ToListAsync();

                return Results.Ok(new
                {
                    items,
                    totalCount,
                    totalSize,
                    page = p,
                    pageSize = ps,
                    totalPages = (int)Math.Ceiling((double)totalCount / ps)
                });
            }

            return Results.Ok(await query.ToListAsync());
        });

        // Get thumbnail for item
        app.MapGet("/api/items/{id}/thumbnail", async (long id, PKGManager manager) =>
        {
            var thumbPath = await manager.GetThumbnailPathAsync(id);
            if (thumbPath == null) return Results.NoContent();
            return Results.File(thumbPath, "image/png");
        });

        // Open item file folder in Explorer
        app.MapPost("/api/items/{id}/open-folder", async (long id, AppDbContext db) =>
        {
            var item = await db.MetaEntities.FindAsync(id);
            if (item == null || string.IsNullOrWhiteSpace(item.CompleteFileName) || !System.IO.File.Exists(item.CompleteFileName))
                return Results.NotFound(new { message = "File not found on disk" });

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{item.CompleteFileName}\"",
                    UseShellExecute = true
                });
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        // Move items
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

        // Enable/Disable items
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

        // Retag items
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

        // Update user tags
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

        // Rename item
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

        // Delete items
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

            var setIdsToMark = itemsToDelete.Where(m => m.SetsEntityId.HasValue).Select(m => m.SetsEntityId!.Value).Distinct().ToList();
            var setsToMark = await db.SetsEntities.Where(s => setIdsToMark.Contains(s.Id)).ToListAsync();
            foreach (var s in setsToMark) s.Dirty = true;

            var defaultSet = await db.SetsEntities.FirstOrDefaultAsync(s => s.Name == "Default");
            if (defaultSet != null) defaultSet.Dirty = true;

            foreach (var item in itemsToDelete)
            {
                var existingTomb = await db.Tombstones.FirstOrDefaultAsync(t => t.FileName == item.FileName);
                if (existingTomb != null)
                {
                    existingTomb.PackageType = item.PackageType;
                    existingTomb.CASCategories = item.CASCategories;
                    existingTomb.CASAge = item.CASAge;
                    existingTomb.CASGender = item.CASGender;
                    existingTomb.CASOutfitCategory = item.CASOutfitCategory;
                    existingTomb.IsUserTagged = item.IsUserTagged;
                    existingTomb.UserTags = item.UserTags;
                    existingTomb.SetsEntityId = item.SetsEntityId;
                    existingTomb.DeletedAt = DateTime.UtcNow;
                }
                else
                {
                    db.Tombstones.Add(new TombstoneEntity
                    {
                        FileName = item.FileName,
                        PackageType = item.PackageType,
                        CASCategories = item.CASCategories,
                        CASAge = item.CASAge,
                        CASGender = item.CASGender,
                        CASOutfitCategory = item.CASOutfitCategory,
                        IsUserTagged = item.IsUserTagged,
                        UserTags = item.UserTags,
                        SetsEntityId = item.SetsEntityId,
                        DeletedAt = DateTime.UtcNow
                    });
                }

                if (System.IO.File.Exists(item.CompleteFileName))
                {
                    if (permanent)
                    {
                        try { System.IO.File.Delete(item.CompleteFileName); } catch { /* Ignore */ }
                    }
                    else
                    {
                        RecycleBinHelper.SendToRecycleBin(item.CompleteFileName);
                    }
                }
                count++;
            }

            db.MetaEntities.RemoveRange(itemsToDelete);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = $"Deleted {count} items." });
        });
    }
}
