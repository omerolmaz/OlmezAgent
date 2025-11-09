namespace Server.Application.DTOs.Command;

public class CommandDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ExecuteCommandRequest
{
    public Guid DeviceId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? Parameters { get; set; }
}

public class CommandResultRequest
{
    public Guid CommandId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
}
