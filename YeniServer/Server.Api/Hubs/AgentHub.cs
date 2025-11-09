using Microsoft.AspNetCore.SignalR;
using Server.Application.Interfaces;
using Server.Application.DTOs.Device;
using Server.Domain.Enums;
using System.Collections.Concurrent;

namespace Server.Api.Hubs;

/// <summary>
/// SignalR hub for agent connections
/// </summary>
public class AgentHub : Hub
{
    private readonly IDeviceService _deviceService;
    private readonly ICommandService _commandService;
    private static readonly ConcurrentDictionary<string, Guid> _connections = new();

    public AgentHub(IDeviceService deviceService, ICommandService commandService)
    {
        _deviceService = deviceService;
        _commandService = commandService;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Console.WriteLine($"Agent connected: {Context.ConnectionId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var deviceId))
        {
            await _deviceService.UpdateDeviceStatusAsync(new UpdateDeviceStatusRequest
            {
                DeviceId = deviceId,
                Status = ConnectionStatus.Disconnected
            });

            Console.WriteLine($"Agent disconnected: {Context.ConnectionId}, Device: {deviceId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Agent registers itself to the server
    /// </summary>
    public async Task<object> RegisterDevice(RegisterDeviceRequest request)
    {
        var result = await _deviceService.RegisterDeviceAsync(request);
        
        if (!result.Success)
        {
            return new { success = false, error = result.ErrorMessage };
        }

        var deviceId = result.Data!.Id;
        _connections[Context.ConnectionId] = deviceId;

        // Add to device group for targeted messaging
        await Groups.AddToGroupAsync(Context.ConnectionId, deviceId.ToString());

        return new { success = true, deviceId };
    }

    /// <summary>
    /// Agent sends heartbeat
    /// </summary>
    public async Task Heartbeat(Guid deviceId)
    {
        await _deviceService.UpdateDeviceLastSeenAsync(deviceId);
    }

    /// <summary>
    /// Agent sends command result
    /// </summary>
    public async Task<object> CommandResult(Guid commandId, string status, string? result)
    {
        var updateResult = await _commandService.UpdateCommandResultAsync(new Application.DTOs.Command.CommandResultRequest
        {
            CommandId = commandId,
            Status = status,
            Result = result
        });

        return new { success = updateResult.Success };
    }

    /// <summary>
    /// Server sends command to specific device (called from controllers)
    /// </summary>
    public async Task SendCommandToDevice(Guid deviceId, Guid commandId, string commandType, string? parameters)
    {
        await Clients.Group(deviceId.ToString()).SendAsync("ExecuteCommand", new
        {
            commandId,
            commandType,
            parameters
        });
    }
}
