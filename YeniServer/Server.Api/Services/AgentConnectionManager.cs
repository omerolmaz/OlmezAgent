using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Server.Api.Services;

/// <summary>
/// Aktif agent WebSocket bağlantılarını yöneten servis
/// </summary>
public class AgentConnectionManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _connections = new();
    private readonly ILogger<AgentConnectionManager> _logger;

    public AgentConnectionManager(ILogger<AgentConnectionManager> logger)
    {
        _logger = logger;
    }

    public void AddConnection(Guid deviceId, WebSocket webSocket)
    {
        _connections[deviceId] = webSocket;
        _logger.LogInformation("Agent {DeviceId} connection added. Total active: {Count}", 
            deviceId, _connections.Count);
    }

    public void RemoveConnection(Guid deviceId)
    {
        if (_connections.TryRemove(deviceId, out _))
        {
            _logger.LogInformation("Agent {DeviceId} connection removed. Total active: {Count}", 
                deviceId, _connections.Count);
        }
    }

    public WebSocket? GetConnection(Guid deviceId)
    {
        return _connections.TryGetValue(deviceId, out var socket) ? socket : null;
    }

    public bool IsConnected(Guid deviceId)
    {
        return _connections.TryGetValue(deviceId, out var socket) && 
               socket.State == WebSocketState.Open;
    }

    public int GetActiveConnectionCount()
    {
        return _connections.Count(kvp => kvp.Value.State == WebSocketState.Open);
    }

    public IEnumerable<Guid> GetActiveDeviceIds()
    {
        return _connections
            .Where(kvp => kvp.Value.State == WebSocketState.Open)
            .Select(kvp => kvp.Key);
    }

    public async Task<bool> SendCommandToAgentAsync(Guid deviceId, object command, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(deviceId, out var socket) || socket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Cannot send command to {DeviceId}: Not connected", deviceId);
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(command, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            _logger.LogInformation("Command sent to agent {DeviceId}: {Command}", deviceId, json);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to agent {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<int> BroadcastCommandAsync(object command, CancellationToken cancellationToken = default)
    {
        var activeConnections = _connections
            .Where(kvp => kvp.Value.State == WebSocketState.Open)
            .ToList();

        _logger.LogInformation("Broadcasting command to {Count} agents", activeConnections.Count);

        int sentCount = 0;
        foreach (var (deviceId, _) in activeConnections)
        {
            var success = await SendCommandToAgentAsync(deviceId, command, cancellationToken);
            if (success) sentCount++;
        }

        return sentCount;
    }

    /// <summary>
    /// Bağlı device sayısını döner
    /// </summary>
    public int GetConnectedDevicesCount()
    {
        return _connections.Count;
    }

    /// <summary>
    /// Bağlı tüm device ID'lerini döner
    /// </summary>
    public List<Guid> GetConnectedDevices()
    {
        return _connections.Keys.ToList();
    }
}
