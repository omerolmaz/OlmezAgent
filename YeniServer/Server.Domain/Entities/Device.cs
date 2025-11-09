using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Server.Domain.Enums;

namespace Server.Domain.Entities;

/// <summary>
/// Device (agent) entity
/// </summary>
public class Device
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Hostname { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? MacAddress { get; set; }

    [MaxLength(100)]
    public string? Domain { get; set; }

    [MaxLength(100)]
    public string? OsVersion { get; set; }

    [MaxLength(50)]
    public string? Architecture { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [MaxLength(20)]
    public string? AgentVersion { get; set; }

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;

    public DateTime? LastSeenAt { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public Guid? GroupId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(GroupId))]
    public virtual Group? Group { get; set; }

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
    public virtual ICollection<Command> Commands { get; set; } = new List<Command>();
    public virtual ICollection<Event> Events { get; set; } = new List<Event>();
    public virtual ICollection<File> Files { get; set; } = new List<File>();
}
