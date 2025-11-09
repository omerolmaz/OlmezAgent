using Server.Domain.Enums;

namespace Server.Application.DTOs.Device;

public class DeviceDto
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? Domain { get; set; }
    public string? OsVersion { get; set; }
    public string? Architecture { get; set; }
    public string? IpAddress { get; set; }
    public string? AgentVersion { get; set; }
    public ConnectionStatus Status { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime RegisteredAt { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
}

public class RegisterDeviceRequest
{
    public string Hostname { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? Domain { get; set; }
    public string? OsVersion { get; set; }
    public string? Architecture { get; set; }
    public string? IpAddress { get; set; }
    public string AgentVersion { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
}

public class UpdateDeviceStatusRequest
{
    public Guid DeviceId { get; set; }
    public ConnectionStatus Status { get; set; }
    public string? IpAddress { get; set; }
}
