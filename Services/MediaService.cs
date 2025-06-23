using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
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
        if (CurrentUserId == null)
        {
            _logger.LogWarning("UploadFileAsync called without authenticated user");
            return null;
        }
        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId);
        
        if (!await _accessService.HasPermissionAsync(CurrentUserId, TargetType.Folder, folderId, PermissionEnum.Append))
        {
            _logger.LogWarning($"User {CurrentUserId} does not have permission to append to folder {folderId}");
            return null;
        }

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
        var mediaItems = await _db.MediaItems
            .Where(m => m.FolderId == folderId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var result = new List<MediaItem>();
        foreach (var m in mediaItems)
        {
            if (await _accessService.HasPermissionAsync(CurrentUserId!, TargetType.MediaItem, m.Id, PermissionEnum.View))
            {
                result.Add(m);
            }
        }
        return result;
    }

    // Get media by ID with access check
    public async Task<MediaItem?> GetMediaAsync(int mediaId)
    {
        return await _db.MediaItems
            .Include(m => m.Folder)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.Folder.OwnerId == CurrentUserId);
    }

    // Delete media if I own the folder
    public async Task DeleteMediaAsync(int mediaId)
    {
        _logger.LogInformation($"Deleting media {mediaId}...");
        var media = await _db.MediaItems
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null)
        {
            _logger.LogWarning($"Media {mediaId} not found");
            throw new FileNotFoundException($"Media {mediaId} not found");
        }
        
        if (!await _accessService.HasPermissionAsync(CurrentUserId!, TargetType.MediaItem, mediaId, PermissionEnum.Modify))
        {
            _logger.LogWarning($"User {CurrentUserId} does not have permission to delete media {mediaId}");
            throw new UnauthorizedAccessException($"You do not have permission to delete media {mediaId}");
        }

        if (File.Exists(media.DiskPath))
            File.Delete(media.DiskPath);

        _db.MediaItems.Remove(media);
        await _db.SaveChangesAsync();
        _logger.LogInformation($"Media {mediaId} deleted");
    }
    
    public async Task<bool> ShareMediaAsync(int mediaId, string subjectUserId, PermissionEnum permission)
    {
        var media = await _db.MediaItems
            .Include(m => m.Folder)
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null || _accessService.HasPermissionAsync(CurrentUserId, TargetType.MediaItem, mediaId, PermissionEnum.Modify).Result == false)
            return false;

        try
        {
            await _accessService.SetAclAsync(CurrentUserId, subjectUserId, TargetType.MediaItem, mediaId, permission);
            _logger.LogInformation($"Shared media item {mediaId} with user {subjectUserId} with permission {permission}");
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to share media item {FolderId} with user {UserId}", mediaId, subjectUserId);
            return false;
        }
    }
    
    public async Task<IActionResult> DownloadMediaAsync(int mediaId)
    {
        var media = await _db.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media == null)
        {
            _logger.LogWarning($"Media {mediaId} not found");
            return new NotFoundResult();
        }

        if (!await _accessService.HasPermissionAsync(CurrentUserId!, TargetType.MediaItem, mediaId, PermissionEnum.View))
        {
            _logger.LogWarning($"User {CurrentUserId} does not have permission to view media {mediaId}");
            return new ForbidResult();
        }

        var filePath = Path.Combine(BasePath, media.DiskPath);
        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogError($"File {filePath} does not exist on disk.");
            return new NotFoundResult();
        }

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return new FileStreamResult(fileStream, media.MimeType)
        {
            FileDownloadName = media.FileName
        };
    }

}
