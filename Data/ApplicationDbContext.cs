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
    public DbSet<AclEntry>   AclEntries  { get; set; }

protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.Entity<AclEntry>()
        .Property(e => e.PermissionFlags)
        .HasConversion<int>();

    builder.Entity<AclEntry>()
        .HasIndex(e => new { e.TargetType, e.TargetId });
}

}