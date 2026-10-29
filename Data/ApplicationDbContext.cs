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

    public DbSet<Receptionist> Receptionists => Set<Receptionist>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<VisitorRequest> VisitorRequests => Set<VisitorRequest>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

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
            entity.Property(e => e.Mobile2).HasMaxLength(30);
            entity.Property(e => e.UnitNo).HasMaxLength(50);
            entity.Property(e => e.Street).HasMaxLength(150);
            entity.Property(e => e.Tower).HasMaxLength(50);
            entity.Property(e => e.Floor).HasColumnName("Floor").HasMaxLength(20);
            entity.Property(e => e.Block).HasMaxLength(50);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(30).HasDefaultValue("Active");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasMany(e => e.VisitorRequests).WithOne(v => v.Resident).HasForeignKey(v => v.ResidentId);
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
            entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<Receptionist>(entity =>
        {
            entity.ToTable("Receptionists", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
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

        modelBuilder.Entity<VisitorRequest>(entity =>
        {
            entity.ToTable("VisitorRequests", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.VisitorName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Purpose).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Duration).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ArrivalDate).HasColumnType("date");
            entity.Property(e => e.NumberOfGuests).HasDefaultValue(1);
            entity.Property(e => e.CNIC).HasMaxLength(30);
            entity.Property(e => e.VehicleRegistrationNo).HasMaxLength(30);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(30).HasDefaultValue(VisitorStatus.Pending);
            entity.Property(e => e.IsWalkIn).HasDefaultValue(false);
            entity.Property(e => e.CheckedInAt).HasColumnType("datetime2");
            entity.Property(e => e.CheckedOutAt).HasColumnType("datetime2");
            entity.Property(e => e.CheckedInByUserType).HasMaxLength(20);
            entity.Property(e => e.ApprovedAt).HasColumnType("datetime2");
            entity.Property(e => e.DeclinedAt).HasColumnType("datetime2");
            entity.Property(e => e.DeclineReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.ResidentId);
            entity.HasIndex(e => e.ArrivalDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("AppSettings", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.SettingKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SettingValue).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.SettingKey).IsUnique();
        });
    }
}
