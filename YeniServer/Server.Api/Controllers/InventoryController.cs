using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Api.Services;
using Server.Domain.Entities;

namespace Server.Api.Controllers;

/// <summary>
/// Inventory Controller - Reads from DATABASE (not agent)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ICommandService _commandService;

    public InventoryController(
        IInventoryService inventoryService, 
        ILogger<InventoryController> logger,
        AgentConnectionManager connectionManager,
        ICommandService commandService)
    {
        _inventoryService = inventoryService;
        _logger = logger;
        _connectionManager = connectionManager;
        _commandService = commandService;
    }

    /// <summary>
    /// Get latest inventory for device (FROM DATABASE)
    /// </summary>
    [HttpGet("devices/{deviceId}")]
    public async Task<IActionResult> GetInventory(Guid deviceId)
    {
        var inventory = await _inventoryService.GetInventoryAsync(deviceId);
        
        if (inventory == null)
        {
            return NotFound(new { message = "Inventory not found. Click 'Refresh' to collect from agent." });
        }

        var software = await _inventoryService.GetInstalledSoftwareAsync(deviceId);
        var patches = await _inventoryService.GetInstalledPatchesAsync(deviceId);

        var response = MapInventory(inventory, software, patches);
        return Ok(new InventoryEnvelope(true, response));
    }

    /// <summary>
    /// Get installed software for device (FROM DATABASE)
    /// </summary>
    [HttpGet("devices/{deviceId}/software")]
    public async Task<IActionResult> GetInstalledSoftware(Guid deviceId)
    {
        var software = await _inventoryService.GetInstalledSoftwareAsync(deviceId);
        return Ok(software);
    }

    /// <summary>
    /// Get installed patches for device (FROM DATABASE)
    /// </summary>
    [HttpGet("devices/{deviceId}/patches")]
    public async Task<IActionResult> GetInstalledPatches(Guid deviceId)
    {
        var patches = await _inventoryService.GetInstalledPatchesAsync(deviceId);
        return Ok(patches);
    }

    /// <summary>
    /// Request fresh inventory from agent (SENDS COMMAND TO AGENT)
    /// </summary>
    [HttpPost("devices/{deviceId}/refresh")]
    public async Task<IActionResult> RefreshInventory(Guid deviceId, [FromBody] RefreshRequest request)
    {
        try
        {
            var commandId = await _inventoryService.RequestInventoryRefreshAsync(deviceId, request.UserId);

            // Prepare command envelope
            var commandEnvelope = new
            {
                action = "getfullinventory",
                commandId,
                timestamp = DateTime.UtcNow,
                parameters = new { }
            };

            bool sent = false;
            if (_connectionManager.IsConnected(deviceId))
            {
                sent = await _connectionManager.SendCommandToAgentAsync(deviceId, commandEnvelope);
                if (sent)
                {
                    await _commandService.MarkCommandAsSentAsync(commandId);
                }
            }
            
            return Ok(new 
            { 
                commandId,
                message = sent
                    ? "Inventory refresh command sent to agent. Data will be updated shortly."
                    : "Inventory refresh command queued. Device is offline or command could not be delivered immediately."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request inventory refresh for device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Failed to request inventory refresh", error = ex.Message });
        }
    }

    public class RefreshRequest
    {
        public Guid UserId { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static InventorySummaryDto MapInventory(
        DeviceInventory inventory,
        IEnumerable<InstalledSoftware> software,
        IEnumerable<InstalledPatch> patches)
    {
        var networkAdapters = ParseNetworkAdapters(inventory.NetworkAdapters);
        var diskDrives = ParseDiskDrives(inventory.DiskDrives);
        var monitors = ParseMonitors(inventory.MonitorInfo);

        long? totalDiskSpace = inventory.TotalDiskSpace;
        if (totalDiskSpace == null && diskDrives.Count > 0)
        {
            totalDiskSpace = diskDrives
                .Select(d => d.SizeBytes)
                .Where(v => v.HasValue)
                .Sum(v => v!.Value);
        }

        int? diskCount = inventory.DiskCount;
        if (diskCount == null && diskDrives.Count > 0)
        {
            diskCount = diskDrives.Count;
        }

        return new InventorySummaryDto
        {
            DeviceId = inventory.DeviceId,
            CollectedAt = inventory.CollectedAt,
            UpdatedAt = inventory.UpdatedAt,
            Manufacturer = inventory.Manufacturer,
            Model = inventory.Model,
            SerialNumber = inventory.SerialNumber,
            BiosVersion = inventory.BiosVersion,
            BiosManufacturer = inventory.BiosManufacturer,
            BiosReleaseDate = inventory.BiosReleaseDate,
            SystemSku = inventory.SystemSKU,
            SystemFamily = inventory.SystemFamily,
            ChassisType = inventory.ChassisType,
            ProcessorName = inventory.ProcessorName,
            ProcessorManufacturer = inventory.ProcessorManufacturer,
            ProcessorCores = inventory.ProcessorCores,
            ProcessorLogicalProcessors = inventory.ProcessorLogicalProcessors,
            ProcessorArchitecture = inventory.ProcessorArchitecture,
            ProcessorMaxClockSpeed = inventory.ProcessorMaxClockSpeed,
            TotalPhysicalMemoryBytes = inventory.TotalPhysicalMemory,
            MemorySlots = inventory.MemorySlots,
            MemoryType = inventory.MemoryType,
            MemorySpeed = inventory.MemorySpeed,
            TotalDiskSpaceBytes = totalDiskSpace,
            DiskCount = diskCount,
            OsName = inventory.OsName,
            OsVersion = inventory.OsVersion,
            OsBuild = inventory.OsBuild,
            OsArchitecture = inventory.OsArchitecture,
            OsInstallDate = inventory.OsInstallDate,
            OsSerialNumber = inventory.OsSerialNumber,
            OsProductKey = inventory.OsProductKey,
            PrimaryMacAddress = inventory.PrimaryMacAddress,
            PrimaryIpAddress = inventory.PrimaryIpAddress,
            HostName = inventory.HostName,
            DomainName = inventory.DomainName,
            GraphicsCard = inventory.GraphicsCard,
            GraphicsCardMemory = inventory.GraphicsCardMemory,
            CurrentResolution = inventory.CurrentResolution,
            NetworkAdapters = networkAdapters,
            DiskDrives = diskDrives,
            Monitors = monitors,
            Software = software
                .Select(item => new InventorySoftwareDto(
                    item.Id,
                    item.Name,
                    item.Version,
                    item.Publisher,
                    item.InstallDate,
                    item.InstallLocation,
                    item.SizeInBytes))
                .OrderBy(s => s.Name)
                .ToList(),
            Patches = patches
                .Select(patch => new InventoryPatchDto(
                    patch.Id,
                    patch.HotFixId,
                    patch.Description,
                    patch.InstalledOn,
                    patch.InstalledBy))
                .OrderByDescending(p => p.InstalledOn)
                .ToList()
        };
    }

    private static IReadOnlyList<NetworkAdapterDto> ParseNetworkAdapters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<NetworkAdapterDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<NetworkAdapterDto>();
            }

            var list = new List<NetworkAdapterDto>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var description = element.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
                var status = element.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
                var speed = element.TryGetProperty("speed", out var speedEl) ? ReadLong(speedEl) : null;
                var mac = element.TryGetProperty("macAddress", out var macEl) ? macEl.GetString() : null;
                var ipAddresses = new List<string>();
                if (element.TryGetProperty("ipAddresses", out var ipsEl) && ipsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ipElement in ipsEl.EnumerateArray())
                    {
                        var ip = ipElement.GetString();
                        if (!string.IsNullOrWhiteSpace(ip))
                        {
                            ipAddresses.Add(ip);
                        }
                    }
                }

                list.Add(new NetworkAdapterDto(name, description, status, speed, mac, ipAddresses));
            }

            return list;
        }
        catch
        {
            return Array.Empty<NetworkAdapterDto>();
        }
    }

    private static IReadOnlyList<DiskDriveDto> ParseDiskDrives(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<DiskDriveDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<DiskDriveDto>();
            }

            var list = new List<DiskDriveDto>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var deviceId = element.TryGetProperty("deviceId", out var idEl) ? idEl.GetString() : null;
                var size = element.TryGetProperty("size", out var sizeEl) ? ReadLong(sizeEl) : null;
                var free = element.TryGetProperty("freeSpace", out var freeEl) ? ReadLong(freeEl) : null;
                var fileSystem = element.TryGetProperty("fileSystem", out var fsEl) ? fsEl.GetString() : null;

                list.Add(new DiskDriveDto(deviceId, size, free, fileSystem));
            }

            return list;
        }
        catch
        {
            return Array.Empty<DiskDriveDto>();
        }
    }

    private static IReadOnlyList<MonitorInfoDto> ParseMonitors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<MonitorInfoDto>();
        }

        try
        {
            var result = JsonSerializer.Deserialize<List<MonitorInfoDto>>(json, JsonOptions);
            return result != null ? result : Array.Empty<MonitorInfoDto>();
        }
        catch
        {
            return Array.Empty<MonitorInfoDto>();
        }
    }

    private static long? ReadLong(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var value) => value,
            JsonValueKind.String when long.TryParse(element.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private sealed record InventoryEnvelope(bool Success, InventorySummaryDto? Data, string? Error = null);

    private sealed record InventorySummaryDto
    {
        public Guid DeviceId { get; init; }
        public DateTime CollectedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public string? Manufacturer { get; init; }
        public string? Model { get; init; }
        public string? SerialNumber { get; init; }
        public string? BiosVersion { get; init; }
        public string? BiosManufacturer { get; init; }
        public DateTime? BiosReleaseDate { get; init; }
        public string? SystemSku { get; init; }
        public string? SystemFamily { get; init; }
        public string? ChassisType { get; init; }
        public string? ProcessorName { get; init; }
        public string? ProcessorManufacturer { get; init; }
        public int? ProcessorCores { get; init; }
        public int? ProcessorLogicalProcessors { get; init; }
        public string? ProcessorArchitecture { get; init; }
        public int? ProcessorMaxClockSpeed { get; init; }
        public long? TotalPhysicalMemoryBytes { get; init; }
        public int? MemorySlots { get; init; }
        public string? MemoryType { get; init; }
        public int? MemorySpeed { get; init; }
        public long? TotalDiskSpaceBytes { get; init; }
        public int? DiskCount { get; init; }
        public string? OsName { get; init; }
        public string? OsVersion { get; init; }
        public string? OsBuild { get; init; }
        public string? OsArchitecture { get; init; }
        public DateTime? OsInstallDate { get; init; }
        public string? OsSerialNumber { get; init; }
        public string? OsProductKey { get; init; }
        public string? PrimaryMacAddress { get; init; }
        public string? PrimaryIpAddress { get; init; }
        public string? HostName { get; init; }
        public string? DomainName { get; init; }
        public string? GraphicsCard { get; init; }
        public string? GraphicsCardMemory { get; init; }
        public string? CurrentResolution { get; init; }
        public IReadOnlyList<NetworkAdapterDto> NetworkAdapters { get; init; } = Array.Empty<NetworkAdapterDto>();
        public IReadOnlyList<DiskDriveDto> DiskDrives { get; init; } = Array.Empty<DiskDriveDto>();
        public IReadOnlyList<MonitorInfoDto> Monitors { get; init; } = Array.Empty<MonitorInfoDto>();
        public IReadOnlyList<InventorySoftwareDto> Software { get; init; } = Array.Empty<InventorySoftwareDto>();
        public IReadOnlyList<InventoryPatchDto> Patches { get; init; } = Array.Empty<InventoryPatchDto>();
    }

    private sealed record NetworkAdapterDto(
        string? Name,
        string? Description,
        string? Status,
        long? SpeedBitsPerSecond,
        string? MacAddress,
        IReadOnlyList<string> IpAddresses);

    private sealed record DiskDriveDto(
        string? DeviceId,
        long? SizeBytes,
        long? FreeBytes,
        string? FileSystem);

    private sealed record MonitorInfoDto(
        string? Manufacturer,
        string? Model,
        string? SerialNumber,
        string? Resolution,
        string? Size);

    private sealed record InventorySoftwareDto(
        Guid Id,
        string Name,
        string? Version,
        string? Publisher,
        DateTime? InstallDate,
        string? InstallLocation,
        long? SizeInBytes);

    private sealed record InventoryPatchDto(
        Guid Id,
        string HotFixId,
        string? Description,
        DateTime? InstalledOn,
        string? InstalledBy);
}
