using System.ComponentModel.DataAnnotations;

namespace PhotoShare.Models;

public class MediaItem
{
    [StringLength(32)]
    public string   Id           { get; set; }
    public int      FolderId     { get; set; }
    [StringLength(255)]
    public string   FileName     { get; set; }
    public long     FileSize     { get; set; }
    [StringLength(255)]
    public string   MimeType     { get; set; }
    [StringLength(1024)]
    public string   DiskPath     { get; set; }
    [StringLength(32)]
    public string   OwnerId      { get; set; }
    public DateTime CreatedAt    { get; set; }
    public DateTime LastModified { get; set; }

    public Folder   Folder       { get; set; }
}