using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class CoreDiagnosticsModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "ping",
        "status",
        "agentinfo",
        "versions",
        "connectiondetails"
    };

    public CoreDiagnosticsModule(ILogger<CoreDiagnosticsModule> logger) : base(logger)
    {
    }

    public override string Name => "CoreDiagnosticsModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        var action = command.Action.ToLowerInvariant();
        
        var payload = action switch
        {
            "ping" => HandlePing(),
            "status" => HandleStatus(context),
            "agentinfo" => HandleAgentInfo(context),
            "versions" => HandleVersions(),
            "connectiondetails" => HandleConnectionDetails(context),
            _ => new JsonObject
            {
                ["action"] = command.Action,
                ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["module"] = Name
            }
        };

        var result = new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload);

        await context.ResponseWriter.SendAsync(result, command.CancellationToken).ConfigureAwait(false);
        Logger.LogInformation("CoreDiagnostics handled {Action}", command.Action);
        return true;
    }
    
    private JsonObject HandlePing()
    {
        return new JsonObject
        {
            ["action"] = "ping",
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["message"] = "pong"
        };
    }
    
    private JsonObject HandleStatus(AgentContext context)
    {
        return new JsonObject
        {
            ["action"] = "status",
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["connectionStatus"] = context.ConnectionStatus.ToString(),
            ["uptime"] = context.Uptime.ToString(@"dd\.hh\:mm\:ss"),
            ["startTime"] = context.StartTime.ToString("O")
        };
    }
    
    private JsonObject HandleAgentInfo(AgentContext context)
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version?.ToString() ?? "2.0.0";
        
        return new JsonObject
        {
            ["action"] = "agentinfo",
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["agentVersion"] = version,
            ["agentName"] = "olmez Agent",
            ["platform"] = Environment.OSVersion.Platform.ToString(),
            ["osVersion"] = Environment.OSVersion.VersionString,
            ["machineName"] = Environment.MachineName,
            ["userName"] = Environment.UserName,
            ["processorCount"] = Environment.ProcessorCount,
            ["is64Bit"] = Environment.Is64BitOperatingSystem
        };
    }
    
    private JsonObject HandleVersions()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        
        return new JsonObject
        {
            ["action"] = "versions",
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["agentVersion"] = version?.ToString() ?? "2.0.0",
            ["frameworkVersion"] = Environment.Version.ToString(),
            ["clrVersion"] = Environment.Version.ToString(),
            ["buildDate"] = GetBuildDate(assembly).ToString("O")
        };
    }
    
    private JsonObject HandleConnectionDetails(AgentContext context)
    {
        var details = context.GetConnectionDetails();
        
        return new JsonObject
        {
            ["action"] = "connectiondetails",
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["status"] = details.Status,
            ["agentVersion"] = details.NewVersion,
            ["serverUrl"] = details.ServerUrl,
            ["serverId"] = details.ServerId,
            ["groupName"] = details.GroupName,
            ["groupId"] = details.GroupId,
            ["osName"] = details.OSName,
            ["autoProxy"] = details.AutoProxy,
            ["startTime"] = details.StartTime.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            ["uptime"] = details.Uptime.ToString(@"dd\.hh\:mm\:ss")
        };
    }
    
    private static DateTime GetBuildDate(Assembly? assembly)
    {
        if (assembly == null) return DateTime.MinValue;
        
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute != null && DateTime.TryParse(attribute.InformationalVersion, out var buildDate))
        {
            return buildDate;
        }
        
        // Fallback: PE header build timestamp
        return File.GetLastWriteTimeUtc(assembly.Location);
    }
}
