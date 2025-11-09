using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Server.Domain.Enums;

namespace Server.Domain.Entities;

/// <summary>
/// User account entity
/// </summary>
public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? FullName { get; set; }

    public UserRights Rights { get; set; } = UserRights.ViewDevices;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
