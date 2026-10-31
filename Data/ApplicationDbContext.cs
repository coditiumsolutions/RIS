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

    public DbSet<VisitorPhoto> VisitorPhotos => Set<VisitorPhoto>();

    public DbSet<ComplaintCategory> ComplaintCategories => Set<ComplaintCategory>();

    public DbSet<Complaint> Complaints => Set<Complaint>();

    public DbSet<ComplaintHistory> ComplaintHistory => Set<ComplaintHistory>();

    public DbSet<ComplaintPhoto> ComplaintPhotos => Set<ComplaintPhoto>();

    public DbSet<SmtpSettings> SmtpSettings => Set<SmtpSettings>();

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
            entity.Property(e => e.SecurityStamp).HasMaxLength(36);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(e => e.LockoutEndUtc).HasColumnType("datetime2");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasMany(e => e.VisitorRequests).WithOne(v => v.Resident).HasForeignKey(v => v.ResidentId);
            entity.HasMany<Complaint>().WithOne(c => c.Resident).HasForeignKey(c => c.ResidentId);
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
            entity.Property(e => e.SecurityStamp).HasMaxLength(36);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(e => e.LockoutEndUtc).HasColumnType("datetime2");
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
            entity.Property(e => e.SecurityStamp).HasMaxLength(36);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(e => e.LockoutEndUtc).HasColumnType("datetime2");
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
            entity.Property(e => e.Purpose).HasMaxLength(500).IsRequired();
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
            entity.HasMany<VisitorPhoto>().WithOne(p => p.VisitorRequest).HasForeignKey(p => p.VisitorRequestId);
        });

        modelBuilder.Entity<VisitorPhoto>(entity =>
        {
            entity.ToTable("VisitorPhotos", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.Kind).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(80).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CapturedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.VisitorRequestId);
            entity.HasIndex(e => e.CapturedAtUtc);
        });

        modelBuilder.Entity<ComplaintCategory>(entity =>
        {
            entity.ToTable("ComplaintCategories", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.ToTable("Complaints", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.Priority).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.PreferredVisitTime).HasMaxLength(200);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(30).HasDefaultValue(ComplaintStatus.Initiated);
            entity.Property(e => e.DeclineReason).HasMaxLength(500);
            entity.Property(e => e.WorkerName).HasMaxLength(150);
            entity.Property(e => e.WorkerPhone).HasMaxLength(30);
            entity.Property(e => e.AssignedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ResolvedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.ClosedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.SlaDueAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Category).WithMany(c => c.Complaints).HasForeignKey(e => e.CategoryId);
            entity.HasIndex(e => e.ResidentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        modelBuilder.Entity<ComplaintHistory>(entity =>
        {
            entity.ToTable("ComplaintHistory", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.ActorUserType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ActorDisplayName).HasMaxLength(150);
            entity.Property(e => e.FromStatus).HasMaxLength(30);
            entity.Property(e => e.ToStatus).HasMaxLength(30);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Complaint).WithMany(c => c.History).HasForeignKey(e => e.ComplaintId);
            entity.HasIndex(e => e.ComplaintId);
        });

        modelBuilder.Entity<ComplaintPhoto>(entity =>
        {
            entity.ToTable("ComplaintPhotos", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.FilePath).HasMaxLength(80).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UploadedByUserType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CapturedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Complaint).WithMany(c => c.Photos).HasForeignKey(e => e.ComplaintId);
            entity.HasIndex(e => e.ComplaintId);
        });

        modelBuilder.Entity<SmtpSettings>(entity =>
        {
            entity.ToTable("SmtpSettings", "dbo");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("uid").UseIdentityColumn();
            entity.Property(e => e.Host).HasMaxLength(256);
            entity.Property(e => e.Port).HasDefaultValue(587);
            entity.Property(e => e.UseSsl).HasDefaultValue(true);
            entity.Property(e => e.Username).HasMaxLength(256);
            entity.Property(e => e.PasswordProtected).HasMaxLength(2000);
            entity.Property(e => e.FromEmail).HasMaxLength(256);
            entity.Property(e => e.FromName).HasMaxLength(150);
            entity.Property(e => e.EmailNotificationsEnabled).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
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
