namespace Server.Domain.Enums;

/// <summary>
/// License edition types
/// </summary>
public enum LicenseEdition
{
    /// <summary>
    /// Free version with limited features (50 devices)
    /// </summary>
    Community = 0,
    
    /// <summary>
    /// Paid version with all features (unlimited devices)
    /// </summary>
    Enterprise = 1
}
