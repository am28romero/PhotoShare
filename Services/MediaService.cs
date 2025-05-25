using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Security.Cryptography;
using PhotoShare.Helpers;
using PhotoShare.Data;
using PhotoShare.Options;
using PhotoShare.Models;


namespace PhotoShare.Services;

public class MediaService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _http;
    private readonly string _basePath;
    private readonly ILogger<MediaService> _logger;
    private readonly AccessService _accessService;

    public MediaService(
        ApplicationDbContext db, IWebHostEnvironment env,
        IHttpContextAccessor http, IOptions<MediaStorageOptions> opts,
        ILogger<MediaService> logger, AccessService accessService)
    {
        _db = db;
        _env = env;
        _http = http;
        _basePath = opts.Value.BasePath;
        _logger = logger;
        _accessService = accessService;
    }

    private static void SetDefaultFilePermissions(string path)
    {
        FilePermissionHelper.SetFileOwnership(path, user:"photoshare", group:"photoshare");
        FilePermissionHelper.SetFilePermissions(path, mode:"600");
    }
    private string? CurrentUserId =>
        _http.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    private string BasePath => Path.Combine(_basePath);

    // Upload file into a folder (not root!)
    public async Task<MediaItem?> UploadFileAsync(IBrowserFile file, int folderId)
    {
        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == CurrentUserId);

        if (folder == null)
            return null;

        // Create physical folder path
        var folderPath = await _db.Folders
            .Where(f => f.Id == folderId)
            .Select(f => f.DiskPath)
            .FirstOrDefaultAsync();
        if (folderPath == null)
            throw new DirectoryNotFoundException($"Folder {folderId} not found");

        var ext = Path.GetExtension(file.Name);
        var hashInput = $"{folderId}{file.Name}";
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(hashInput));
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        var storedFileName = $"{DateTime.UtcNow:yyyyMMdd}_{hash}{ext}";
        var relPath = Path.Combine(folderPath, storedFileName);
        var absPath = Path.Combine(BasePath, relPath);

        
        try
        {
            await using var fs = new FileStream(absPath, FileMode.Create);
            await using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB default
            await stream.CopyToAsync(fs);
            // SetDefaultFilePermissions(absPath);
        }
        catch
        {
            if (File.Exists(absPath))
            {
                File.Delete(absPath);
            }
            throw;
        }


        var item = new MediaItem
        {
            Hash = hash,
            FolderId = folder.Id,
            FileName = file.Name,
            FileSize = file.Size,
            MimeType = file.ContentType,
            DiskPath = relPath,
            OwnerId = CurrentUserId!,
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
