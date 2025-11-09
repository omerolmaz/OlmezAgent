using Microsoft.EntityFrameworkCore;
using Server.Domain.Entities;
using Server.Domain.Enums;

namespace Server.Infrastructure.Data;

/// <summary>
/// Application database context
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Domain.Entities.File> Files => Set<Domain.Entities.File>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();
    public DbSet<DeviceInventory> DeviceInventories => Set<DeviceInventory>();
    public DbSet<InstalledSoftware> InstalledSoftware => Set<InstalledSoftware>();
    public DbSet<InstalledPatch> InstalledPatches => Set<InstalledPatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Rights).HasConversion<string>();
        });

        // Device configuration
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Hostname);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.GroupId);
            entity.Property(e => e.Status).HasConversion<string>();
            
            entity.HasOne(d => d.Group)
                .WithMany(g => g.Devices)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Group configuration
        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // License configuration
        modelBuilder.Entity<License>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LicenseKey).IsUnique();
            entity.Property(e => e.Edition).HasConversion<string>();
            entity.Property(e => e.Features).HasConversion<string>();
        });

        // Session configuration
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ConnectionId);
            
            entity.HasOne(s => s.Device)
                .WithMany(d => d.Sessions)
                .HasForeignKey(s => s.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Command configuration
        modelBuilder.Entity<Command>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.HasOne(c => c.Device)
                .WithMany(d => d.Commands)
                .HasForeignKey(c => c.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Event configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Timestamp);
            
            entity.HasOne(e => e.Device)
                .WithMany(d => d.Events)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.Timestamp);
            
            entity.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // File configuration
        modelBuilder.Entity<Domain.Entities.File>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.FileHash);
            
            entity.HasOne(f => f.Device)
                .WithMany(d => d.Files)
                .HasForeignKey(f => f.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AgentSession configuration
        modelBuilder.Entity<AgentSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.SessionType);
            entity.HasIndex(e => e.IsActive);
            
            entity.HasOne(s => s.Device)
                .WithMany()
                .HasForeignKey(s => s.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // DeviceInventory configuration
        modelBuilder.Entity<DeviceInventory>(entity =>
        {
            entity.HasKey(e => e.DeviceId); // PRIMARY KEY = DeviceId (tek kayıt per device)
            entity.HasIndex(e => e.CollectedAt);
            entity.HasIndex(e => e.SerialNumber);
            
            entity.HasOne(i => i.Device)
                .WithMany()
                .HasForeignKey(i => i.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InstalledSoftware configuration
        modelBuilder.Entity<InstalledSoftware>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DeviceId, e.RegistryPath }).IsUnique(); // Unique per device+registry
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CollectedAt);
            
            entity.HasOne(s => s.Device)
                .WithMany()
                .HasForeignKey(s => s.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InstalledPatch configuration
        modelBuilder.Entity<InstalledPatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DeviceId, e.HotFixId }).IsUnique(); // Unique per device+hotfix
            entity.HasIndex(e => e.CollectedAt);
            
            entity.HasOne(p => p.Device)
                .WithMany()
                .HasForeignKey(p => p.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Default admin user (password: Admin123!)
        // SHA256 hash of "Admin123!"
        var adminPasswordHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("Admin123!")));
        
        var adminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = adminId,
            Username = "admin",
            PasswordHash = adminPasswordHash,
            Email = "admin@olmezserver.com",
            FullName = "System Administrator",
            Rights = UserRights.All,
            IsActive = true,
            CreatedAt = new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Default license (Community edition)
        var licenseId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        modelBuilder.Entity<License>().HasData(new License
        {
            Id = licenseId,
            LicenseKey = "OLMEZ-COMMUNITY-FREE-EDITION",
            Edition = LicenseEdition.Community,
            Features = EnterpriseFeature.None,
            MaxDevices = 50,
            IssuedAt = new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            LicensedTo = "Community User",
            CompanyName = "Free License",
            IsActive = true,
            CurrentDeviceCount = 0
        });
    }
}
