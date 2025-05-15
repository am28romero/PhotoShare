using Microsoft.EntityFrameworkCore;
using PhotoShare.Data;

namespace PhotoShare.Services;

public class FolderService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<FolderService> _logger;

    public FolderService(ApplicationDbContext db, IHttpContextAccessor http, ILogger<FolderService> logger)
    {
        _logger = logger;
        _db = db;
        _http = http;
    }

    private string? CurrentUserId =>
        _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;


    // Get folders I own — later: ACL-aware
    public async Task<List<Folder>> GetAccessibleFoldersAsync()
    {
        _logger.LogInformation($"Getting accessible folders for user id: `{CurrentUserId}`");
        
        return await _db.Folders
            .Where(f => f.OwnerId == CurrentUserId)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    // Get single folder if I own it
    public async Task<Folder?> GetFolderAsync(int folderId)
    {
        return await _db.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == CurrentUserId);
    }

    // Create folder (optionally nested)
    public async Task<Folder?> CreateFolderAsync(string name, int? parentFolderId = null)
    {
        if (parentFolderId.HasValue)
        {
            var parent = await _db.Folders
                .FirstOrDefaultAsync(f => f.Id == parentFolderId.Value && f.OwnerId == CurrentUserId);
            if (parent == null)
                return null;
        }

        var folder = new Folder
        {
            Name = name,
            OwnerId = CurrentUserId,
            ParentFolderId = parentFolderId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();
        return folder;
    }

    // Rename folder (if owned)
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
