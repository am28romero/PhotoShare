using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhotoShare.Data;
using PhotoShare.Options;
using PhotoShare.Models;
using PhotoShare.Helpers;

namespace PhotoShare.Services;

public class FolderService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<FolderService> _logger;
    private readonly string _mediaBasePath;
    private readonly AccessService _accessService;

    public FolderService(
        ApplicationDbContext db, IHttpContextAccessor http,
        ILogger<FolderService> logger, IOptions<MediaStorageOptions> mediaStorageOptions,
        AccessService accessService)
    {
        _logger = logger;
        _db = db;
        _http = http;
        var mediaStorageOpts = mediaStorageOptions.Value;
        if (mediaStorageOpts.BasePath == null)
        {
            throw new InvalidOperationException("Media storage base path is not configured.");
        }
        _mediaBasePath = Path.Combine(mediaStorageOpts.BasePath);
        _accessService = accessService;
    }

    private string? CurrentUserId =>
        _http.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private void SetDefaultDirectoryPermissions(string path)
    {
        FilePermissionHelper.SetDirectoryOwnership(path, user:"photoshare", group:"photoshare");
        FilePermissionHelper.SetDirectoryPermissions(path, mode:"700");
    }


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
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == folderId);
        if (folder == null || !await _accessService.HasPermissionAsync(CurrentUserId!, TargetType.Folder, folder.Id, PermissionEnum.View))
            return null;
        return folder;
    }

    // Create folder (optionally nested)
    public async Task<Folder?> CreateFolderAsync(string name, int? parentFolderId = null)
    {
        // Verify parent exists and access is allowed
        List<int> folderChain = [];
        if (parentFolderId.HasValue)
        {
            var currentId = parentFolderId;
            while (currentId.HasValue)
            {
                var parent = await _db.Folders.FirstOrDefaultAsync(f => f.Id == currentId);
                if (parent == null)
                    return null;

                if (!await _accessService.HasPermissionAsync(CurrentUserId!, TargetType.Folder, parent.Id, PermissionEnum.Modify))
                    return null;

                folderChain.Insert(0, parent.Id); // prepend for root-to-leaf
                currentId = parent.ParentFolderId;
            }
        }

        // Step 1: Create DB row without DiskPath
        var folder = new Folder
        {
            Name = name,
            OwnerId = CurrentUserId!,
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow,
            DiskPath = "" // will be set later
        };

        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(); // generates folder.Id

        // Step 2: Build relative path with full folder chain
        folder = await _db.Folders
            .Include(f => f.ParentFolder)
            .FirstOrDefaultAsync(f => f.Id == folder.Id);
        
        folderChain.Add(folder.Id); // include this folder
        var relPath = Path.Combine(
            $"u_{folder.OwnerId}",
            Path.Combine(folderChain.Select(id => $"f_{id}").ToArray())
        );

        folder.DiskPath = relPath;

        // Step 3: Create directory on disk
        var absPath = Path.Combine(_mediaBasePath, relPath);
        if (!Directory.Exists(absPath))
        {
            try
            {
                Directory.CreateDirectory(absPath);
                // Optionally set permissions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create folder path `{FolderPath}`", absPath);
                // Cleanup: remove DB entry if directory creation fails
                _db.Folders.Remove(folder);
                await _db.SaveChangesAsync();
                return null;
            }
        }

        // Step 4: Save updated DiskPath
        await _db.SaveChangesAsync();
        return folder;
    }


    public async Task<bool> RenameFolderAsync(int folderId, string newName)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == folderId);
        if (folder == null || !await _accessService.HasPermissionAsync(CurrentUserId!, TargetType.Folder, folder.Id, PermissionEnum.Modify))
            return false;

        folder.Name = newName;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteFolderAsync(int folderId, bool skipPermissionCheck = false)
    {
        var folder = await _db.Folders
            .Include(f => f.ChildFolders)
            .Include(f => f.MediaItems)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null)
            return false;

        if (!skipPermissionCheck &&
            !await _accessService.HasPermissionAsync(CurrentUserId!, TargetType.Folder, folder.Id, PermissionEnum.Modify))
            return false;

        // Recursively delete subfolders
        foreach (var child in folder.ChildFolders)
        {
            await DeleteFolderAsync(child.Id, skipPermissionCheck: true); // already verified access
        }

        // Delete media items
        _db.MediaItems.RemoveRange(folder.MediaItems);

        // Delete this folder
        _db.Folders.Remove(folder);
        
        // Delete on-disk folder
        var folderPath = Path.Combine(_mediaBasePath, folder.DiskPath);
        if (Directory.Exists(folderPath))
        {
            try
            {
                Directory.Delete(folderPath, recursive: true);
                _logger.LogInformation("Deleted folder `{FolderPath}`", folderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete folder `{FolderPath}`", folderPath);
            }
        }
        
        await _db.SaveChangesAsync();
        return true;
    }
}
