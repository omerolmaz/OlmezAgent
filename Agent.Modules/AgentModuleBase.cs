using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;

namespace Agent.Modules;

public abstract class AgentModuleBase : IAgentModule
{
    protected AgentModuleBase(ILogger logger)
    {
        Logger = logger;
    }

    protected ILogger Logger { get; }

    public abstract string Name { get; }
    public abstract IReadOnlyCollection<string> SupportedActions { get; }

    public virtual Task<bool> CanHandleAsync(AgentCommand command, AgentContext context)
    {
        return Task.FromResult(SupportedActions.Count == 0 ||
                               SupportedActions.Any(a => string.Equals(a, command.Action, StringComparison.OrdinalIgnoreCase)));
    }

    public abstract Task<bool> HandleAsync(AgentCommand command, AgentContext context);

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected Task SendNotImplementedAsync(AgentCommand command, AgentContext context, string detail)
    {
        Logger.LogWarning("{Module} has not implemented action {Action}: {Detail}", Name, command.Action, detail);
        var payload = new JsonObject
        {
            ["message"] = detail,
            ["action"] = command.Action,
            ["module"] = Name
        };

        var result = new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload,
            Success: false,
            Error: "NotImplemented");

        return context.ResponseWriter.SendAsync(result, command.CancellationToken);
    }
}
