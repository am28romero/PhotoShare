using System.ComponentModel.DataAnnotations;

namespace PhotoShare.Models;

public class MediaItem
{
    public int   Id              { get; set; }
    [StringLength(32)]
    public string   Hash         { get; set; } = null!;
    public int      FolderId     { get; set; }
    [StringLength(255)]
    public string   FileName     { get; set; } = null!;
    public long     FileSize     { get; set; }
    [StringLength(255)]
    public string   MimeType     { get; set; } = null!;
    [StringLength(1024)]
    public string   DiskPath     { get; set; } = null!;
    [StringLength(32)]
    public string   OwnerId      { get; set; } = null!;
    public DateTime CreatedAt    { get; set; }
    public DateTime LastModified { get; set; }
    public Folder   Folder       { get; set; } = null!;
}