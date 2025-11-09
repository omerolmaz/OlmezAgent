namespace Server.Domain.Entities;

public class DeviceInventory
{
    // DeviceId = PRIMARY KEY (tek kayıt per device)
    public Guid DeviceId { get; set; }
    public DateTime CollectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Hardware
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? BiosVersion { get; set; }
    public string? BiosManufacturer { get; set; }
    public DateTime? BiosReleaseDate { get; set; }
    
    // System
    public string? SystemSKU { get; set; }
    public string? SystemFamily { get; set; }
    public string? ChassisType { get; set; }
    
    // Processor
    public string? ProcessorName { get; set; }
    public string? ProcessorManufacturer { get; set; }
    public int? ProcessorCores { get; set; }
    public int? ProcessorLogicalProcessors { get; set; }
    public string? ProcessorArchitecture { get; set; }
    public int? ProcessorMaxClockSpeed { get; set; }
    
    // Memory
    public long? TotalPhysicalMemory { get; set; }
    public int? MemorySlots { get; set; }
    public string? MemoryType { get; set; }
    public int? MemorySpeed { get; set; }
    
    // Storage
    public long? TotalDiskSpace { get; set; }
    public int? DiskCount { get; set; }
    
    // Operating System
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? OsBuild { get; set; }
    public string? OsArchitecture { get; set; }
    public DateTime? OsInstallDate { get; set; }
    public string? OsSerialNumber { get; set; }
    public string? OsProductKey { get; set; }
    
    // Network
    public string? PrimaryMacAddress { get; set; }
    public string? PrimaryIpAddress { get; set; }
    public string? HostName { get; set; }
    public string? DomainName { get; set; }
    
    // Graphics
    public string? GraphicsCard { get; set; }
    public string? GraphicsCardMemory { get; set; }
    public string? CurrentResolution { get; set; }
    
    // Additional Info (JSON)
    public string? NetworkAdapters { get; set; } // JSON array
    public string? DiskDrives { get; set; } // JSON array
    public string? MonitorInfo { get; set; } // JSON array
    
    // Navigation
    public Device Device { get; set; } = null!;
}
