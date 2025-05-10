using Microsoft.EntityFrameworkCore;
using PhotoShare.Data;

namespace PhotoShare.Services;

public class FolderService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public FolderService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private string CurrentUserId => _http.HttpContext?.User?.FindFirst("sub")?.Value!;

    public async Task<Folder> CreateFolderAsync(string name, int? parentId = null)
    {
        var folder = new Folder
        {
            Name = name,
            OwnerId = CurrentUserId,
            ParentFolderId = parentId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();
        return folder;
    }

    public async Task<List<Folder>> ListFoldersAsync()
    {
        return await _db.Folders
            .Where(f => f.OwnerId == CurrentUserId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteFolderAsync(int folderId)
    {
        var folder = await _db.Folders
            .Include(f => f.MediaItems)
            .FirstOrDefaultAsync(f => f.Id == folderId);

        if (folder == null) return false;

        _db.MediaItems.RemoveRange(folder.MediaItems);
        _db.Folders.Remove(folder);
        await _db.SaveChangesAsync();
        return true;
    }
}