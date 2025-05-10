public class MediaItem
{
    public int      Id           { get; set; }
    public int      FolderId     { get; set; }
    public string   FileName     { get; set; }
    public long     FileSize     { get; set; }
    public string   MimeType     { get; set; }
    public string   DiskPath     { get; set; }
    public DateTime CreatedAt    { get; set; }
    public DateTime LastModified { get; set; }

    public Folder   Folder       { get; set; }
}