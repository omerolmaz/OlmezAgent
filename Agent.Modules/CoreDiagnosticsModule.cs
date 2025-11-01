using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
        "versions"
    };

    public CoreDiagnosticsModule(ILogger<CoreDiagnosticsModule> logger) : base(logger)
    {
    }

    public override string Name => "CoreDiagnosticsModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        var payload = new JsonObject
        {
            ["action"] = command.Action,
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["module"] = Name
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
}
