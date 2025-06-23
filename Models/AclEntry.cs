using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotoShare.Data;

[Flags]
public enum PermissionEnum
{
    None   = 0,
    View   = 1,
    Append = 2,
    Modify = 4,
    Share  = 8,
    FullControl = View | Append | Modify | Share
}

public class AclEntry
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string TargetType { get; set; } = null!; // "Folder" or "MediaItem"

    [Required]
    public int TargetId { get; set; }

    [Required]
    public string SubjectId { get; set; } = null!; // FK to AspNetUsers
    [Required]
    public PermissionEnum PermissionFlags { get; set; }

    [Required]
    public string GrantedById { get; set; } = null!; // FK to AspNetUsers

    [Required]
    public DateTime GrantedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(SubjectId))]
    public ApplicationUser Subject { get; set; } = null!;

    [ForeignKey(nameof(GrantedById))]
    public ApplicationUser GrantedBy { get; set; } = null!;
}