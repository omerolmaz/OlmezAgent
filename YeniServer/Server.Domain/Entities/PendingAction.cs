using System;

namespace Server.Domain.Entities;

/// <summary>
/// Represents a pending action that will be executed on an agent
/// Used for tracking async operations like software installation
/// </summary>
public class PendingAction
{
    public Guid Id { get; set; }
    
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    
    public PendingActionType ActionType { get; set; }
    public PendingActionStatus Status { get; set; }
    
    /// <summary>
    /// JSON details about the action (package name, parameters, etc.)
    /// </summary>
    public string Details { get; set; } = "{}";
    
    /// <summary>
    /// Output from the action execution
    /// </summary>
    public string? Output { get; set; }
    
    /// <summary>
    /// Error message if action failed
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Exit code from the command execution
    /// </summary>
    public int? ExitCode { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// User who initiated the action
    /// </summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Types of pending actions
/// </summary>
public enum PendingActionType
{
    ChocoInstall = 1,
    SoftwareInstall = 2,
    SoftwareUninstall = 3,
    ScriptExecution = 4,
    CommandExecution = 5
}

/// <summary>
/// Status of a pending action
/// </summary>
public enum PendingActionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Timeout = 4,
    Cancelled = 5
}
