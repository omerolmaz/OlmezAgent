namespace Server.Domain.Enums;

/// <summary>
/// Agent connection status
/// </summary>
public enum ConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Reconnecting = 3,
    Error = 4
}
