using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Domain.Entities;

/// <summary>
/// File metadata entity
/// </summary>
public class File
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid DeviceId { get; set; }

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? FileName { get; set; }

    public long FileSize { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; }

    [MaxLength(64)]
    public string? FileHash { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Guid UploadedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(DeviceId))]
    public virtual Device Device { get; set; } = null!;
}
