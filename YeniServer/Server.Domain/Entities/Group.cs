using System.ComponentModel.DataAnnotations;

namespace Server.Domain.Entities;

/// <summary>
/// Device group entity
/// </summary>
public class Group
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
}
