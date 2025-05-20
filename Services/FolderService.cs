using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhotoShare.Data;
using PhotoShare.Options;
using PhotoShare.Models;

namespace PhotoShare.Services;

public class FolderService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<FolderService> _logger;
    private readonly MediaStorageOptions _mediaStorageOptions;
    private readonly string _mediaBasePath;

    public FolderService(
        ApplicationDbContext db, IHttpContextAccessor http,
        ILogger<FolderService> logger, IOptions<MediaStorageOptions> mediaStorageOptions)
    {
        _logger = logger;
        _db = db;
        _http = http;
        _mediaStorageOptions = mediaStorageOptions.Value;
        if (_mediaStorageOptions.BasePath == null)
        {
            throw new InvalidOperationException("Media storage base path is not configured.");
        }
        _mediaBasePath = Path.Combine(_mediaStorageOptions.BasePath, "media");
    }

    private string? CurrentUserId =>
        _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;


    public async Task<List<Folder>> GetAccessibleFoldersAsync()
    {
        _logger.LogInformation($"Getting accessible folders for user id: `{CurrentUserId}`");
        
        return await _db.Folders
            .Where(f => f.OwnerId == CurrentUserId)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<Folder?> GetFolderAsync(int folderId)
    {
        return await _db.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == CurrentUserId);
    }

    // Create folder (optionally nested)
    public async Task<Folder?> CreateFolderAsync(string name, int? parentFolderId = null)
    {
        string relPath;
        // Recursively add subfolders to ensure the full path exists
        if (parentFolderId.HasValue)
        {
            var parentFolders = new Stack<string>();
            var currentParentId = parentFolderId;
            while (currentParentId.HasValue)
            {
                var parentFolder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == currentParentId.Value);
                if (parentFolder == null) break;
                parentFolders.Push($"f_{parentFolder.Name}");
                currentParentId = parentFolder.ParentFolderId;
            }
            relPath = Path.Combine($"u_{CurrentUserId}" , Path.Combine(parentFolders.ToArray()), $"f_{name}");
        }
        else
        {
            relPath = Path.Combine($"u_{CurrentUserId}", $"f_{name}");
        }

        var folder = new Folder
        {
            Name = name,
            OwnerId = CurrentUserId,
            ParentFolderId = parentFolderId,
            DiskPath = relPath,
            CreatedAt = DateTime.UtcNow
        };

        _db.Folders.Add(folder);

        // Create folder on disk
        var absPath = Path.Combine(_mediaBasePath, relPath);
        if (!Directory.Exists(absPath))
        {
            try 
            {
                Directory.CreateDirectory(absPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create directory for folder `{FolderName}`", name);
                return null;
            }
        }

        await _db.SaveChangesAsync();
        return folder;
    }

    public async Task<bool> RenameFolderAsync(int folderId, string newName)
    {
        var folder = await GetFolderAsync(folderId);
        if (folder == null) return false;

        folder.Name = newName;
        await _db.SaveChangesAsync();
        return true;
    }

    // Delete folder and child media
    public async Task<bool> DeleteFolderAsync(int folderId)
    {
        var folder = await _db.Folders
            .Include(f => f.MediaItems)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == CurrentUserId);

        if (folder == null) return false;

        // Remove media items first
        _db.MediaItems.RemoveRange(folder.MediaItems);
        _db.Folders.Remove(folder);
        await _db.SaveChangesAsync();
        return true;
    }
}
