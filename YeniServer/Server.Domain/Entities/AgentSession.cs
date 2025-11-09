using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Domain.Entities;

/// <summary>
/// Session tracking for long-running agent operations (desktop, console, file monitor, etc.)
/// </summary>
public class AgentSession
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid DeviceId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SessionType { get; set; } = string.Empty; // desktop, console, filemonitor, eventmonitor

    [Required]
    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? SessionData { get; set; } // JSON data specific to session type

    public bool IsActive { get; set; } = true;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public DateTime? LastActivityAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(DeviceId))]
    public virtual Device Device { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
