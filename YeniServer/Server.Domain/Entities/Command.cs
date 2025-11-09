using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Server.Domain.Enums;

namespace Server.Domain.Entities;

/// <summary>
/// Command execution history entity
/// </summary>
public class Command
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid DeviceId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CommandType { get; set; } = string.Empty;

    public CommandCategory Category { get; set; }

    [MaxLength(2000)]
    public string? Parameters { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public string? Result { get; set; }

    /// <summary>
    /// Session ID for session-based commands (desktop, console, etc.)
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Command priority (0=Normal, 1=High, 2=Critical)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Retry count if command fails
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Error message if failed
    /// </summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long? ExecutionDurationMs { get; set; }

    // Navigation properties
    [ForeignKey(nameof(DeviceId))]
    public virtual Device Device { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
