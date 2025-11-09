using Microsoft.EntityFrameworkCore;
using Server.Application.Common;
using Server.Application.DTOs.Device;
using Server.Application.Interfaces;
using Server.Domain.Entities;
using Server.Domain.Enums;
using Server.Infrastructure.Data;

namespace Server.Application.Services;

public class DeviceService : IDeviceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILicenseService _licenseService;

    public DeviceService(ApplicationDbContext context, ILicenseService licenseService)
    {
        _context = context;
        _licenseService = licenseService;
    }

    public async Task<Result<DeviceDto>> GetDeviceByIdAsync(Guid id)
    {
        var device = await _context.Devices
            .Include(d => d.Group)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
            return Result<DeviceDto>.Fail("Device not found", "DEVICE_NOT_FOUND");

        return Result<DeviceDto>.Ok(MapToDto(device));
    }

    public async Task<Result<List<DeviceDto>>> GetAllDevicesAsync()
    {
        var devices = await _context.Devices
            .Include(d => d.Group)
            .OrderBy(d => d.Hostname)
            .ToListAsync();

        var dtos = devices.Select(MapToDto).ToList();
        return Result<List<DeviceDto>>.Ok(dtos);
    }

    public async Task<Result<List<DeviceDto>>> GetDevicesByGroupAsync(Guid groupId)
    {
        var devices = await _context.Devices
            .Include(d => d.Group)
            .Where(d => d.GroupId == groupId)
            .OrderBy(d => d.Hostname)
            .ToListAsync();

        var dtos = devices.Select(MapToDto).ToList();
        return Result<List<DeviceDto>>.Ok(dtos);
    }

    public async Task<Result<DeviceDto>> RegisterDeviceAsync(RegisterDeviceRequest request)
    {
        // Check license capacity
        var capacityCheck = await _licenseService.CheckDeviceCapacityAsync();
        if (!capacityCheck.Success)
            return Result<DeviceDto>.Fail(capacityCheck.ErrorMessage!, capacityCheck.ErrorCode);

        if (!capacityCheck.Data)
            return Result<DeviceDto>.Fail("Device limit reached. Upgrade your license.", "DEVICE_LIMIT_EXCEEDED");

        // Check if device already exists by MAC address (preferred) or hostname
        Device? existingDevice = null;
        
        // Try to find by MAC address first (most reliable unique identifier)
        if (!string.IsNullOrWhiteSpace(request.MacAddress))
        {
            existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.MacAddress == request.MacAddress);
        }
        
        // If not found by MAC, try by hostname
        if (existingDevice == null)
        {
            existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.Hostname == request.Hostname);
        }

        if (existingDevice != null)
        {
            // Update existing device
            existingDevice.Hostname = request.Hostname; // Update hostname in case it changed
            existingDevice.MacAddress = request.MacAddress; // Update MAC if it was missing before
            existingDevice.Domain = request.Domain;
            existingDevice.OsVersion = request.OsVersion;
            existingDevice.Architecture = request.Architecture;
            existingDevice.IpAddress = request.IpAddress;
            existingDevice.AgentVersion = request.AgentVersion;
            existingDevice.Status = ConnectionStatus.Connected;
            existingDevice.LastSeenAt = DateTime.UtcNow;
            existingDevice.GroupId = request.GroupId;

            await _context.SaveChangesAsync();
            return Result<DeviceDto>.Ok(MapToDto(existingDevice));
        }

        // Create new device
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Hostname = request.Hostname,
            MacAddress = request.MacAddress,
            Domain = request.Domain,
            OsVersion = request.OsVersion,
            Architecture = request.Architecture,
            IpAddress = request.IpAddress,
            AgentVersion = request.AgentVersion,
            Status = ConnectionStatus.Connected,
            LastSeenAt = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow,
            GroupId = request.GroupId
        };

        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        // Increment device count in license
        await _licenseService.IncrementDeviceCountAsync();

        return Result<DeviceDto>.Ok(MapToDto(device));
    }

    public async Task<Result> UpdateDeviceStatusAsync(UpdateDeviceStatusRequest request)
    {
        var device = await _context.Devices.FindAsync(request.DeviceId);
        if (device == null)
            return Result.Fail("Device not found", "DEVICE_NOT_FOUND");

        device.Status = request.Status;
        device.LastSeenAt = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(request.IpAddress))
            device.IpAddress = request.IpAddress;

        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> UpdateDeviceLastSeenAsync(Guid deviceId)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
            return Result.Fail("Device not found", "DEVICE_NOT_FOUND");

        device.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteDeviceAsync(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
            return Result.Fail("Device not found", "DEVICE_NOT_FOUND");

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        // Decrement device count in license
        await _licenseService.DecrementDeviceCountAsync();

        return Result.Ok();
    }

    private DeviceDto MapToDto(Device device)
    {
        return new DeviceDto
        {
            Id = device.Id,
            Hostname = device.Hostname,
            MacAddress = device.MacAddress,
            Domain = device.Domain,
            OsVersion = device.OsVersion,
            Architecture = device.Architecture,
            IpAddress = device.IpAddress,
            AgentVersion = device.AgentVersion,
            Status = device.Status,
            LastSeenAt = device.LastSeenAt,
            RegisteredAt = device.RegisteredAt,
            GroupId = device.GroupId,
            GroupName = device.Group?.Name
        };
    }
}
