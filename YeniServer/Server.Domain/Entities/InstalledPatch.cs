namespace Server.Domain.Entities;

public class InstalledPatch
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime CollectedAt { get; set; }
    
    public string HotFixId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? InstalledOn { get; set; }
    public string? InstalledBy { get; set; }
    
    // Navigation
    public Device Device { get; set; } = null!;
}
