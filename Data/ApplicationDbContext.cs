using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhotoShare.Models;

namespace PhotoShare.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions opts)
        : base(opts) { }

    public DbSet<Folder>     Folders     { get; set; }
    public DbSet<MediaItem>  MediaItems  { get; set; }

protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    // builder.Entity<MediaItem>(entity =>
    // {
    //     entity.ToTable("MediaItems"); // make sure casing matches table name
    //     entity.Property(m => m.Id).HasColumnName("id");
    //     entity.Property(m => m.FolderId).HasColumnName("folder_id");
    //     entity.Property(m => m.FileName).HasColumnName("file_name");
    //     entity.Property(m => m.FileSize).HasColumnName("file_size");
    //     entity.Property(m => m.MimeType).HasColumnName("mime_type");
    //     entity.Property(m => m.DiskPath).HasColumnName("disk_path");
    //     entity.Property(m => m.CreatedAt).HasColumnName("created_at");
    //     entity.Property(m => m.LastModified).HasColumnName("last_modified");
    // });
    //
    // builder.Entity<Folder>(entity =>
    // {
    //     entity.ToTable("Folders"); // again, match your table name
    //     entity.Property(f => f.Id).HasColumnName("id");
    //     entity.Property(f => f.Name).HasColumnName("name");
    //     entity.Property(f => f.OwnerId).HasColumnName("owner_id");
    //     entity.Property(f => f.ParentFolderId).HasColumnName("parent_folder_id");
    //     entity.Property(f => f.CreatedAt).HasColumnName("created_at");
    // });
}

}