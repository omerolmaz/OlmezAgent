namespace Server.Domain.Entities;

public class InstalledSoftware
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime CollectedAt { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
    public string? InstallLocation { get; set; }
    public long? SizeInBytes { get; set; }
    public string? UninstallString { get; set; }
    public string? QuietUninstallString { get; set; } // Silent uninstall için
    public string? RegistryPath { get; set; } // Benzersizlik için
    
    // Navigation
    public Device Device { get; set; } = null!;
}
