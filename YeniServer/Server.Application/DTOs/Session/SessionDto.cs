namespace Server.Application.DTOs.Session;

public class SessionDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }
    public string SessionType { get; set; } = string.Empty; // desktop, console, filemonitor, eventmonitor
    public string SessionId { get; set; } = string.Empty;
    public string? SessionData { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class StartSessionRequest
{
    public Guid DeviceId { get; set; }
    public string SessionType { get; set; } = string.Empty; // desktop, console, filemonitor, eventmonitor
    public string? InitialData { get; set; } // Session'a özgü başlangıç parametreleri (JSON)
}

public class UpdateSessionDataRequest
{
    public Guid SessionId { get; set; }
    public string Data { get; set; } = string.Empty;
}

public class EndSessionRequest
{
    public Guid SessionId { get; set; }
}
