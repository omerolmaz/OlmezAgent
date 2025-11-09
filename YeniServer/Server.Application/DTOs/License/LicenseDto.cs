using Server.Domain.Enums;

namespace Server.Application.DTOs.License;

public class LicenseDto
{
    public Guid Id { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public LicenseEdition Edition { get; set; }
    public EnterpriseFeature Features { get; set; }
    public int MaxDevices { get; set; }
    public int CurrentDeviceCount { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? LicensedTo { get; set; }
    public string? CompanyName { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool HasCapacity => CurrentDeviceCount < MaxDevices;
}

public class ValidateLicenseRequest
{
    public string LicenseKey { get; set; } = string.Empty;
}

public class GenerateLicenseRequest
{
    public LicenseEdition Edition { get; set; }
    public int MaxDevices { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? LicensedTo { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
}
