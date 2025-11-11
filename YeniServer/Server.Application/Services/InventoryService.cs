using Microsoft.EntityFrameworkCore;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Domain.Entities;
using Server.Infrastructure.Data;

namespace Server.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ICommandService _commandService;

    public InventoryService(ApplicationDbContext context, ICommandService commandService)
    {
        _context = context;
        _commandService = commandService;
    }

    public async Task<DeviceInventory?> GetInventoryAsync(Guid deviceId)
    {
        return await _context.DeviceInventories
            .FirstOrDefaultAsync(i => i.DeviceId == deviceId);
    }

    public async Task<IEnumerable<InstalledSoftware>> GetInstalledSoftwareAsync(Guid deviceId)
    {
        return await _context.InstalledSoftware
            .Where(s => s.DeviceId == deviceId)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<InstalledPatch>> GetInstalledPatchesAsync(Guid deviceId)
    {
        return await _context.InstalledPatches
            .Where(p => p.DeviceId == deviceId)
            .OrderByDescending(p => p.InstalledOn)
            .ToListAsync();
    }

    public async Task SaveInventoryAsync(DeviceInventory inventory)
    {
        var existing = await _context.DeviceInventories
            .FirstOrDefaultAsync(i => i.DeviceId == inventory.DeviceId);

        if (existing != null)
        {
            // UPDATE existing
            _context.Entry(existing).CurrentValues.SetValues(inventory);
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // INSERT new
            inventory.UpdatedAt = DateTime.UtcNow;
            await _context.DeviceInventories.AddAsync(inventory);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SaveInstalledSoftwareAsync(Guid deviceId, IEnumerable<InstalledSoftware> software)
    {
        // SİL eski kayıtları
        var existingSoftware = await _context.InstalledSoftware
            .Where(s => s.DeviceId == deviceId)
            .ToListAsync();
        
        _context.InstalledSoftware.RemoveRange(existingSoftware);

        // EKLE yeni kayıtları
        await _context.InstalledSoftware.AddRangeAsync(software);
        await _context.SaveChangesAsync();
    }

    public async Task SaveInstalledPatchesAsync(Guid deviceId, IEnumerable<InstalledPatch> patches)
    {
        // SİL eski kayıtları
        var existingPatches = await _context.InstalledPatches
            .Where(p => p.DeviceId == deviceId)
            .ToListAsync();
        
        _context.InstalledPatches.RemoveRange(existingPatches);

        // EKLE yeni kayıtları
        await _context.InstalledPatches.AddRangeAsync(patches);
        await _context.SaveChangesAsync();
    }

    public async Task<Guid> RequestInventoryRefreshAsync(Guid deviceId, Guid userId)
    {
        // Convert Guid.Empty to null for nullable UserId
        Guid? nullableUserId = userId == Guid.Empty ? null : userId;
        
        var command = await _commandService.ExecuteCommandAsync(
            new ExecuteCommandRequest
            {
                DeviceId = deviceId,
                CommandType = "getfullinventory",
                Parameters = "{}"
            },
            nullableUserId);

        if (!command.Success || command.Data == null)
        {
            throw new Exception("Failed to create inventory refresh command");
        }

        return command.Data.Id;
    }
}
