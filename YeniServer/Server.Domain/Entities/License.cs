using System.ComponentModel.DataAnnotations;
using Server.Domain.Enums;

namespace Server.Domain.Entities;

/// <summary>
/// License entity
/// </summary>
public class License
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string LicenseKey { get; set; } = string.Empty;

    public LicenseEdition Edition { get; set; } = LicenseEdition.Community;

    public EnterpriseFeature Features { get; set; } = EnterpriseFeature.None;

    public int MaxDevices { get; set; } = 50;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    [MaxLength(200)]
    public string? LicensedTo { get; set; }

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public int CurrentDeviceCount { get; set; } = 0;
}
