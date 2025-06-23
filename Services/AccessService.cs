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
    
    public async Task<PermissionEnum> GetPermissionsAsync(string userId, TargetType targetType, int targetId)
    {
        // If Folder → recursively check parent ownership or ACL
        if (targetType == TargetType.Folder)
        {
            var folder = await _db.Folders
                .Include(f => f.ParentFolder)
                .FirstOrDefaultAsync(f => f.Id == targetId);

            if (folder == null)
                throw new KeyNotFoundException();

            if (folder.OwnerId == userId)
                return PermissionEnum.FullControl;

            // Direct ACL
            var acl = await _db.AclEntries
                .FirstOrDefaultAsync(a =>
                    a.TargetType == "Folder" &&
                    a.TargetId == folder.Id
                    && a.SubjectId == userId);

            if (acl != null)
                return acl.PermissionFlags;

            // Recurse up
            if (folder.ParentFolderId != null)
                return await GetPermissionsAsync(userId, TargetType.Folder, folder.ParentFolderId.Value);
        }

        // If MediaItem → check owner or parent folder ACLs
        if (targetType == TargetType.MediaItem)
        {
            var media = await _db.MediaItems
                .Include(m => m.Folder)
                .FirstOrDefaultAsync(m => m.Id == targetId);

            if (media == null) return 0;

            if (media.OwnerId == userId || media.Folder.OwnerId == userId)
                return PermissionEnum.FullControl;

            // Direct ACL on media
            var acl = await _db.AclEntries
                .FirstOrDefaultAsync(a => 
                    a.TargetType == "MediaItem" &&
                    a.TargetId == media.Id &&
                    a.SubjectId == userId);
            
            if (acl != null)
                return acl.PermissionFlags;

            // Inherit from folder
            return await GetPermissionsAsync(userId, TargetType.Folder, media.FolderId);
        }

        return 0;
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
                .FirstOrDefaultAsync(a =>
                    a.TargetType == "Folder" &&
                    a.TargetId == folder.Id &&
                    a.SubjectId == userId);
            
            if (acl != null)
            {
                var effective = acl.PermissionFlags;
                if ((effective & required) == required)
                    return true;
            }

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

            if (media.OwnerId == userId || media.Folder.OwnerId == userId)
                return true;

            // Direct ACL on media
            var acl = await _db.AclEntries
                .FirstOrDefaultAsync(a => a.TargetType == "MediaItem" && a.TargetId == media.Id && a.SubjectId == userId);

            if (acl != null)
            {
                var effective = acl.PermissionFlags;
                if ((effective & required) == required)
                    return true;
            }
            
            // Inherit from folder
            return await HasPermissionAsync(userId, TargetType.Folder, media.FolderId, required);
        }

        return false;
    }
    
    public async Task SetAclAsync(
        string grantedById, string subjectId, TargetType targetType,
        int targetId, PermissionEnum permissionFlags)
    {
        // Validate inputs
        if (grantedById == subjectId) 
            throw new ArgumentException("Cannot set ACL for self");
        
        // Delete existing ACL for this user + target
        var existing = await _db.AclEntries
            .FirstOrDefaultAsync(a =>
                a.SubjectId == subjectId &&
                a.TargetType == targetType.ToString() &&
                a.TargetId == targetId);

        if (existing != null)
        {
            _db.AclEntries.Remove(existing);
        }

        // Add new ACL
        var entry = new AclEntry
        {
            TargetType = targetType.ToString(),
            TargetId = targetId,
            SubjectId = subjectId,
            PermissionFlags = permissionFlags,
            GrantedById = grantedById,
            GrantedAt = DateTime.UtcNow
        };

        _db.AclEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

}
