using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Domain.Entities;

/// <summary>
/// Audit log for all agent and system operations
/// </summary>
public class AuditLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public Guid? DeviceId { get; set; }

    public Guid? CommandId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EventType { get; set; } // CommandExecuted, SessionStarted, FileTransfer, etc.

    [MaxLength(100)]
    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [MaxLength(200)]
    public string? UserAgent { get; set; }

    public string? Details { get; set; }

    public bool Success { get; set; } = true;

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(DeviceId))]
    public virtual Device? Device { get; set; }

    [ForeignKey(nameof(CommandId))]
    public virtual Command? Command { get; set; }
}

