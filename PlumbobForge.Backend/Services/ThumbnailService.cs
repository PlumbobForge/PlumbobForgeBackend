using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PlumbobForge.Backend.Configuration;
using PlumbobForge.Backend.Database;
using S3ForgeTools.GameFiles.Package;
using S3ForgeTools.GameFiles.TS3Pack;

namespace PlumbobForge.Backend.Services;

public class ThumbnailService
{
    private readonly AppDbContext _db;
    private readonly PlumbobForgeOptions _options;

    public ThumbnailService(AppDbContext db, IOptionsSnapshot<PlumbobForgeOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<string?> GetThumbnailPathAsync(long itemId)
    {
        string thumbDir = Path.Combine(_options.DocumentBaseDir, "Thumbnails");
        Directory.CreateDirectory(thumbDir);
        string thumbPath = Path.Combine(thumbDir, $"{itemId}.thumb");

        if (File.Exists(thumbPath))
        {
            return thumbPath;
        }

        var item = await _db.MetaEntities.FindAsync(itemId);
        if (item == null || !File.Exists(item.CompleteFileName)) return null;

        try
        {
            if (item.FileName.ToLower().EndsWith(".sims3pack"))
            {
                using var sims3Pack = new Sims3Pack(item.CompleteFileName);
                if (sims3Pack.Thumbnails != null && sims3Pack.Thumbnails.Count > 0)
                {
                    using var firstThumb = sims3Pack.Thumbnails[0];
                    using var fs = new FileStream(thumbPath, FileMode.Create, FileAccess.Write);
                    await firstThumb.CopyToAsync(fs);
                    return thumbPath;
                }
                else if (sims3Pack.Thumbnail != null)
                {
                    using var thumb = sims3Pack.Thumbnail;
                    using var fs = new FileStream(thumbPath, FileMode.Create, FileAccess.Write);
                    await thumb.CopyToAsync(fs);
                    return thumbPath;
                }
            }
            else
            {
                using var package = new DBPFPackage(item.CompleteFileName);
                var validThumbTypes = new uint[] {
                    0x626F60CC, 0x626F60CD, 0x626F60CE, // Custom thumbnails (highest priority)
                    0x2E75C765, 0x2E75C764, 0x2E75C766, // Auto-generated CAS / Object thumbnails
                    0x0B202AD9, // THUM
                    0x0580A2B4, 0x0580A2B5, 0x0580A2B6 // Other UI thumbnails
                };

                var res = validThumbTypes
                    .Select(typeId => package.Resources.FirstOrDefault(r => r.Key.Type == typeId))
                    .FirstOrDefault(r => r != null);

                if (res != null)
                {
                    var bytes = res.Read();
                    await File.WriteAllBytesAsync(thumbPath, bytes);
                    return thumbPath;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Thumbnails] Error extracting from {item.CompleteFileName}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        return null;
    }
}
