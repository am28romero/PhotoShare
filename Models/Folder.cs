using PhotoShare.Data;

namespace PhotoShare.Models;

public class Folder
{
    public int     Id             { get; set; }
    public string  OwnerId        { get; set; }
    public string  Name           { get; set; }
    public int?    ParentFolderId { get; set; }
    public string  DiskPath           { get; set; }
    public DateTime CreatedAt     { get; set; }

    public ApplicationUser Owner       { get; set; }
    public Folder          ParentFolder{ get; set; }
    public ICollection<Folder> ChildFolders { get; set; }
    public ICollection<MediaItem> MediaItems   { get; set; }
}