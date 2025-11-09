using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Domain.Entities;

/// <summary>
/// Active connection session entity
/// </summary>
public class Session
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid DeviceId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(100)]
    public string? ConnectionId { get; set; }

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DisconnectedAt { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    // Navigation properties
    [ForeignKey(nameof(DeviceId))]
    public virtual Device Device { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
