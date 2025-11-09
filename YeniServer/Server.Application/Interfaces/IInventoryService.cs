using Server.Domain.Entities;

namespace Server.Application.Interfaces;

public interface IInventoryService
{
    /// <summary>
    /// Get latest inventory for a device
    /// </summary>
    Task<DeviceInventory?> GetInventoryAsync(Guid deviceId);
    
    /// <summary>
    /// Get installed software for a device
    /// </summary>
    Task<IEnumerable<InstalledSoftware>> GetInstalledSoftwareAsync(Guid deviceId);
    
    /// <summary>
    /// Get installed patches for a device
    /// </summary>
    Task<IEnumerable<InstalledPatch>> GetInstalledPatchesAsync(Guid deviceId);
    
    /// <summary>
    /// Save or update inventory (called when agent sends inventory data)
    /// </summary>
    Task SaveInventoryAsync(DeviceInventory inventory);
    
    /// <summary>
    /// Save installed software (replaces existing data for device)
    /// </summary>
    Task SaveInstalledSoftwareAsync(Guid deviceId, IEnumerable<InstalledSoftware> software);
    
    /// <summary>
    /// Save installed patches (replaces existing data for device)
    /// </summary>
    Task SaveInstalledPatchesAsync(Guid deviceId, IEnumerable<InstalledPatch> patches);
    
    /// <summary>
    /// Request fresh inventory from agent (sends command to agent)
    /// </summary>
    Task<Guid> RequestInventoryRefreshAsync(Guid deviceId, Guid userId);
}
