using Server.Application.Common;
using Server.Application.DTOs.Device;

namespace Server.Application.Interfaces;

public interface IDeviceService
{
    Task<Result<DeviceDto>> GetDeviceByIdAsync(Guid id);
    Task<Result<List<DeviceDto>>> GetAllDevicesAsync();
    Task<Result<List<DeviceDto>>> GetDevicesByGroupAsync(Guid groupId);
    Task<Result<DeviceDto>> RegisterDeviceAsync(RegisterDeviceRequest request);
    Task<Result> UpdateDeviceStatusAsync(UpdateDeviceStatusRequest request);
    Task<Result> UpdateDeviceLastSeenAsync(Guid deviceId);
    Task<Result> DeleteDeviceAsync(Guid id);
}
