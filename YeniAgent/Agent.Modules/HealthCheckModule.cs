using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class HealthCheckModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "health",
        "metrics",
        "uptime"
    };

    private static readonly DateTime StartTime = DateTime.UtcNow;
    private static readonly Stopwatch Uptime = Stopwatch.StartNew();
    private readonly Process _currentProcess = Process.GetCurrentProcess();

    public HealthCheckModule(ILogger<HealthCheckModule> logger) : base(logger)
    {
    }

    public override string Name => "HealthCheckModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        switch (command.Action.ToLowerInvariant())
        {
            case "health":
                await HandleHealthAsync(command, context).ConfigureAwait(false);
                return true;
            case "metrics":
                await HandleMetricsAsync(command, context).ConfigureAwait(false);
                return true;
            case "uptime":
                await HandleUptimeAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleHealthAsync(AgentCommand command, AgentContext context)
    {
        var health = GetHealthStatus();
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            health)).ConfigureAwait(false);
    }

    private JsonObject GetHealthStatus()
    {
        _currentProcess.Refresh();

        var uptime = Uptime.Elapsed;
        var memoryMB = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);
        var cpuTime = _currentProcess.TotalProcessorTime;

        var health = new JsonObject
        {
            ["status"] = "healthy",
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["startTime"] = StartTime.ToString("O"),
            ["uptime"] = FormatTimeSpan(uptime),
            ["uptimeSeconds"] = uptime.TotalSeconds,
            ["processId"] = _currentProcess.Id,
            ["processName"] = _currentProcess.ProcessName,
            ["machineName"] = Environment.MachineName,
            ["osVersion"] = Environment.OSVersion.VersionString,
            ["framework"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["memory"] = new JsonObject
            {
                ["workingSetMB"] = Math.Round(memoryMB, 2),
                ["privateMemoryMB"] = Math.Round(_currentProcess.PrivateMemorySize64 / (1024.0 * 1024.0), 2),
                ["virtualMemoryMB"] = Math.Round(_currentProcess.VirtualMemorySize64 / (1024.0 * 1024.0), 2),
                ["gcTotalMemoryMB"] = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2)
            },
            ["cpu"] = new JsonObject
            {
                ["totalProcessorTimeSeconds"] = cpuTime.TotalSeconds,
                ["userProcessorTimeSeconds"] = _currentProcess.UserProcessorTime.TotalSeconds,
                ["privilegedProcessorTimeSeconds"] = _currentProcess.PrivilegedProcessorTime.TotalSeconds
            },
            ["threads"] = _currentProcess.Threads.Count,
            ["handles"] = _currentProcess.HandleCount
        };

        // Health check - mark as unhealthy if issues detected
        if (memoryMB > 500) // More than 500 MB
        {
            health["status"] = "degraded";
            health["warning"] = "High memory usage";
        }

        if (_currentProcess.Threads.Count > 100)
        {
            health["status"] = "degraded";
            health["warning"] = "High thread count";
        }

        return health;
    }

    private async Task HandleMetricsAsync(AgentCommand command, AgentContext context)
    {
        var metrics = GetSystemMetrics();
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            metrics)).ConfigureAwait(false);
    }

    private JsonObject GetSystemMetrics()
    {
        _currentProcess.Refresh();

        // Get system-wide metrics
        double cpuUsage = 0;
        double memoryUsage = 0;
        double diskUsage = 0;

        try
        {
            // CPU usage (system-wide)
            using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
            {
                cpuCounter.NextValue(); // First call always returns 0
                System.Threading.Thread.Sleep(100); // Wait a bit
                cpuUsage = cpuCounter.NextValue();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to get CPU metrics: {Error}", ex.Message);
        }

        try
        {
            // Memory usage (system-wide) - using PerformanceCounter
            using (var memCounter = new PerformanceCounter("Memory", "Available MBytes"))
            {
                var availableMB = memCounter.NextValue();
                // Get total physical memory from WMI or estimate
                var totalMemoryMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0);
                if (totalMemoryMB > 0)
                {
                    memoryUsage = ((totalMemoryMB - availableMB) / totalMemoryMB) * 100.0;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to get memory metrics: {Error}", ex.Message);
        }

        try
        {
            // Disk usage for C: drive
            var driveInfo = new System.IO.DriveInfo("C");
            if (driveInfo.IsReady)
            {
                var usedSpace = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                diskUsage = (usedSpace / (double)driveInfo.TotalSize) * 100.0;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to get disk metrics: {Error}", ex.Message);
        }

        var metrics = new JsonObject
        {
            ["cpuUsage"] = Math.Round(cpuUsage, 2),
            ["memoryUsage"] = Math.Round(memoryUsage, 2),
            ["diskUsage"] = Math.Round(diskUsage, 2),
            ["uptimeSeconds"] = Uptime.Elapsed.TotalSeconds,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        return metrics;
    }

    private static JsonObject CreateMetric(string name, double value, string type, string help)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["value"] = value,
            ["type"] = type,
            ["help"] = help
        };
    }

    private async Task HandleUptimeAsync(AgentCommand command, AgentContext context)
    {
        var uptime = Uptime.Elapsed;

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["uptime"] = FormatTimeSpan(uptime),
                ["uptimeSeconds"] = uptime.TotalSeconds,
                ["startTime"] = StartTime.ToString("O")
            })).ConfigureAwait(false);
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    public override async ValueTask DisposeAsync()
    {
        _currentProcess?.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
