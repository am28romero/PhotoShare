using System.ComponentModel.DataAnnotations;
using PhotoShare.Data;

namespace PhotoShare.Models;

public class Folder
{
    public int     Id             { get; set; }
    [Required] [StringLength(32)]
    public string  OwnerId        { get; set; } = null!;
    [Required] [StringLength(255)]
    public string  Name           { get; set; } = null!;
    public int?    ParentFolderId { get; set; }
    [Required] [StringLength(1024)]
    public string  DiskPath           { get; set; } = null!;
    [Required] 
    public DateTime CreatedAt     { get; set; }
    
#pragma warning disable CS8618
    public ApplicationUser Owner       { get; set; }
    public Folder          ParentFolder{ get; set; }
    public ICollection<Folder> ChildFolders { get; set; }
    public ICollection<MediaItem> MediaItems   { get; set; }
}