using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhotoShare.Data;
using PhotoShare.Options;

namespace PhotoShare.Services;

public class MediaService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _http;
    private readonly string _basePath;
    private readonly ILogger<MediaService> _logger;

    public MediaService(
        ApplicationDbContext db,
        IWebHostEnvironment env,
        IHttpContextAccessor http,
        IOptions<MediaStorageOptions> opts,
        ILogger<MediaService> logger)
    {
        _db = db;
        _env = env;
        _http = http;
        _basePath = opts.Value.BasePath;
        _logger = logger;
    }

    private string? CurrentUserId =>
        _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    private string BasePath => Path.Combine(_basePath);

    // Upload file into a folder (not root!)
    public async Task<MediaItem?> UploadFileAsync(IFormFile file, int folderId)
    {
        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == CurrentUserId);

        if (folder == null)
            return null;

        // Create physical folder path
        var folderPath = Path.Combine(BasePath, $"u_{folder.OwnerId}", $"f_{folderId}");
        Directory.CreateDirectory(folderPath);

        var ext = Path.GetExtension(file.FileName);
        var storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folderPath, storedFileName);

        using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fs);
        }

        var item = new MediaItem
        {
            FolderId = folder.Id,
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

    // List media in a folder (enforcing ownership)
    public async Task<List<MediaItem>> ListMediaAsync(int folderId)
    {
        return await _db.MediaItems
            .Where(m => m.FolderId == folderId &&
                        m.Folder.OwnerId == CurrentUserId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    // Get media by ID with access check
    public async Task<MediaItem?> GetMediaAsync(int mediaId)
    {
        return await _db.MediaItems
            .Include(m => m.Folder)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.Folder.OwnerId == CurrentUserId);
    }

    // Delete media if I own the folder
    public async Task<bool> DeleteMediaAsync(int mediaId)
    {
        _logger.LogInformation($"Deleting media {mediaId}...");
        var media = await _db.MediaItems
            .Include(m => m.Folder)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.Folder.OwnerId == CurrentUserId);

        if (media == null)
        {
            _logger.LogWarning($"Media {mediaId} was not found");
            return false;
        }

        if (File.Exists(media.DiskPath))
            File.Delete(media.DiskPath);

        _db.MediaItems.Remove(media);
        await _db.SaveChangesAsync();
        _logger.LogInformation($"Media {mediaId} deleted");
        return true;
    }
}
