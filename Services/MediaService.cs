using Microsoft.EntityFrameworkCore;
using PhotoShare.Data;

namespace PhotoShare.Services;

public class MediaService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _http;

    public MediaService(ApplicationDbContext db, IWebHostEnvironment env, IHttpContextAccessor http)
    {
        _db = db;
        _env = env;
        _http = http;
    }

    private string CurrentUserId => _http.HttpContext?.User?.FindFirst("sub")?.Value!;
    private string BasePath => Path.Combine(_env.ContentRootPath, "MediaStore");

    public async Task<MediaItem?> UploadFileAsync(IFormFile file, int folderId)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == CurrentUserId);
        if (folder == null) return null;

        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")}{fileExt}";
        var folderPath = Path.Combine(BasePath,
            $"u_{folder.OwnerId}",
            $"f_{folderId}");
        Directory.CreateDirectory(folderPath);

        var fullPath = Path.Combine(folderPath, fileName);
        using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fs);
        }

        var item = new MediaItem
        {
            FolderId = folderId,
            FileName = file.FileName,
            FileSize = file.Length,
            MimeType = file.ContentType,
            DiskPath = fullPath,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _db.MediaItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<List<MediaItem>> ListFilesAsync(int folderId)
    {
        return await _db.MediaItems
            .Where(m => m.FolderId == folderId &&
                        m.Folder.OwnerId == CurrentUserId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteFileAsync(int mediaId)
    {
        var item = await _db.MediaItems
            .Include(m => m.Folder)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.Folder.OwnerId == CurrentUserId);

        if (item == null) return false;

        if (File.Exists(item.DiskPath))
            File.Delete(item.DiskPath);

        _db.MediaItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }
}
