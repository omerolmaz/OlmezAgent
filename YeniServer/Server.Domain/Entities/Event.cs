using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Domain.Entities;

/// <summary>
/// System event/log entity
/// </summary>
public class Event
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid DeviceId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Severity { get; set; } = "Info";

    [MaxLength(500)]
    public string? Message { get; set; }

    public string? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(DeviceId))]
    public virtual Device Device { get; set; } = null!;
}
