using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class MaintenanceModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "agentupdate",
        "agentupdateex",
        "download",
        "reinstall",
        "log",
        "versions"
    };

    public MaintenanceModule(ILogger<MaintenanceModule> logger)
        : base(logger)
    {
    }

    public override string Name => "MaintenanceModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        switch (command.Action.ToLowerInvariant())
        {
            case "log":
                await HandleLogAsync(command, context).ConfigureAwait(false);
                return true;
            case "versions":
                await HandleVersionsAsync(command, context).ConfigureAwait(false);
                return true;
            case "agentupdate":
            case "agentupdateex":
            case "download":
            case "reinstall":
                await SendNotImplementedAsync(command, context, $"{command.Action} pipeline is not configured yet.")
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleLogAsync(AgentCommand command, AgentContext context)
    {
        var lines = new List<string>();
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "agent.log");
            if (File.Exists(logPath))
            {
                var allLines = await File.ReadAllLinesAsync(logPath, command.CancellationToken).ConfigureAwait(false);
                var take = Math.Min(200, allLines.Length);
                lines.AddRange(allLines[^take..]);
            }
        }
        catch (Exception ex)
        {
            lines.Add($"Log read error: {ex.Message}");
        }

        var logArray = new JsonArray();
        foreach (var line in lines)
        {
            logArray.Add(line);
        }

        var payload = new JsonObject
        {
            ["entries"] = logArray
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private async Task HandleVersionsAsync(AgentCommand command, AgentContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var payload = new JsonObject
        {
            ["agentVersion"] = version,
            ["framework"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["os"] = Environment.OSVersion.VersionString,
            ["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            ["machineName"] = Environment.MachineName
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }
}
