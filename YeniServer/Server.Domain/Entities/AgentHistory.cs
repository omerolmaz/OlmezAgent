using System;

namespace Server.Domain.Entities;

/// <summary>
/// History of commands/actions executed on an agent
/// Used for troubleshooting and tracking
/// </summary>
public class AgentHistory
{
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    
    public AgentHistoryType Type { get; set; }
    
    /// <summary>
    /// Command or script that was executed
    /// </summary>
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Output from the command execution
    /// </summary>
    public string? Output { get; set; }
    
    /// <summary>
    /// Error output if command failed
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Exit code from the command
    /// </summary>
    public int? ExitCode { get; set; }
    
    /// <summary>
    /// User who executed the command
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Duration of command execution in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Types of agent history entries
/// </summary>
public enum AgentHistoryType
{
    CommandRun = 1,
    ScriptExecution = 2,
    SoftwareInstall = 3,
    SoftwareUninstall = 4,
    Update = 5,
    Restart = 6
}
