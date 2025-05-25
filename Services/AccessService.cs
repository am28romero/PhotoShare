using Microsoft.EntityFrameworkCore;
using PhotoShare.Data;

namespace PhotoShare.Services;


public enum TargetType
{
    Folder = 1,
    MediaItem = 2
}

public class AccessService
{
    private readonly ApplicationDbContext _db;

    public AccessService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(string userId, TargetType targetType, int targetId, PermissionEnum required)
    {
        // If Folder → recursively check parent ownership or ACL
        if (targetType == TargetType.Folder)
        {
            var folder = await _db.Folders
                .Include(f => f.ParentFolder)
                .FirstOrDefaultAsync(f => f.Id == targetId);

            if (folder == null) return false;

            if (folder.OwnerId == userId)
                return true;

            // Direct ACL
            var acl = await _db.AclEntries
                .Where(a => a.TargetType == "Folder" && a.TargetId == folder.Id && a.SubjectId == userId)
                .ToListAsync();

            var effective = acl.Aggregate(PermissionEnum.View, (acc, a) => acc | a.PermissionEnum);
            if ((effective & required) == required)
                return true;

            // Recurse up
            if (folder.ParentFolderId != null)
                return await HasPermissionAsync(userId, TargetType.Folder, folder.ParentFolderId.Value, required);
        }

        // If MediaItem → check owner or parent folder ACLs
        if (targetType == TargetType.MediaItem)
        {
            var media = await _db.MediaItems
                .Include(m => m.Folder)
                .FirstOrDefaultAsync(m => m.Id == targetId);

            if (media == null) return false;

            if (media.Folder.OwnerId == userId)
                return true;

            // Direct ACL on media
            var acl = await _db.AclEntries
                .Where(a => a.TargetType == "MediaItem" && a.TargetId == media.Id && a.SubjectId == userId)
                .ToListAsync();

            var effective = acl.Aggregate(PermissionEnum.View, (acc, a) => acc | a.PermissionEnum);
            if ((effective & required) == required)
                return true;

            // Inherit from folder
            return await HasPermissionAsync(userId, TargetType.Folder, media.FolderId, required);
        }

        return false;
    }
}
