using Microsoft.EntityFrameworkCore;
using ResidentPortal.Models;

namespace ResidentPortal.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Resident> Residents => Set<Resident>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Resident>(entity =>
        {
            entity.ToTable("Residents", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.ResidentName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Mobile).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("AdminUsers", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.RoleName).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.UserType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(300).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
