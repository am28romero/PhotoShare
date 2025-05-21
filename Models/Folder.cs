using System.ComponentModel.DataAnnotations;
using PhotoShare.Data;

namespace PhotoShare.Models;

public class Folder
{
    public Folder()
    {
        ChildFolders = new HashSet<Folder>();
        MediaItems   = new HashSet<MediaItem>();
    }
    public int     Id             { get; set; }
    [StringLength(32)]
    public string  OwnerId        { get; set; } = default!;
    [StringLength(255)]
    public string  Name           { get; set; }= default!;
    public int?    ParentFolderId { get; set; }
    [StringLength(1024)]
    public string  DiskPath           { get; set; } = default!;
    public DateTime CreatedAt     { get; set; }

    public ApplicationUser Owner       { get; set; }
    public Folder          ParentFolder{ get; set; }
    public ICollection<Folder> ChildFolders { get; set; }
    public ICollection<MediaItem> MediaItems   { get; set; }
}