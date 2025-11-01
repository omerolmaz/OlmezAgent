using Agent.Abstractions;
using Agent.Scripting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class JavaScriptBridgeModule : AgentModuleBase
{
    private static readonly HashSet<string> ControlActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "scriptdeploy",
        "scriptreload",
        "scriptlist",
        "scriptremove"
    };

    private readonly JavaScriptRuntime _runtime;

    public JavaScriptBridgeModule(JavaScriptRuntime runtime, ILogger<JavaScriptBridgeModule> logger)
        : base(logger)
    {
        _runtime = runtime;
    }

    public override string Name => "JavaScriptBridgeModule";

    public override IReadOnlyCollection<string> SupportedActions => Array.Empty<string>();

    public override Task<bool> CanHandleAsync(AgentCommand command, AgentContext context)
    {
        if (ControlActions.Contains(command.Action))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(_runtime.CanHandle(command.Action));
    }

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        switch (command.Action.ToLowerInvariant())
        {
            case "scriptdeploy":
                await HandleScriptDeployAsync(command, context).ConfigureAwait(false);
                return true;
            case "scriptreload":
                _runtime.ReloadDefaultScript();
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action,
                    command.NodeId,
                    command.SessionId,
                    BuildHandlerPayload(null, includeScripts: true))).ConfigureAwait(false);
                return true;
            case "scriptlist":
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action,
                    command.NodeId,
                    command.SessionId,
                    BuildHandlerPayload(null, includeScripts: true))).ConfigureAwait(false);
                return true;
            case "scriptremove":
                await HandleScriptRemoveAsync(command, context).ConfigureAwait(false);
                return true;
        }

        var result = _runtime.Execute(command);
        if (result == null)
        {
            return false;
        }

        await context.ResponseWriter.SendAsync(result, command.CancellationToken).ConfigureAwait(false);
        return true;
    }

    public override async ValueTask DisposeAsync()
    {
        await _runtime.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private async Task HandleScriptDeployAsync(AgentCommand command, AgentContext context)
    {
        var name = command.Payload.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()!
            : $"script_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        string? code = null;
        if (command.Payload.TryGetProperty("codeBase64", out var base64Element) && base64Element.ValueKind == JsonValueKind.String)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64Element.GetString()!);
                code = Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Script base64 decode failed.");
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action,
                    command.NodeId,
                    command.SessionId,
                    new JsonObject { ["error"] = "Base64 decode failed" },
                    Success: false,
                    Error: ex.Message)).ConfigureAwait(false);
                return;
            }
        }
        else if (command.Payload.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String)
        {
            code = codeElement.GetString();
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                new JsonObject { ["error"] = "Script content missing" },
                Success: false,
                Error: "Script content missing" )).ConfigureAwait(false);
            return;
        }

        try
        {
            _runtime.LoadScript(name, code);
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                BuildHandlerPayload(name, includeScripts: true))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Script deploy failed: {Name}", name);
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                new JsonObject { ["error"] = ex.Message },
                Success: false,
                Error: ex.Message)).ConfigureAwait(false);
        }
    }

    private async Task HandleScriptRemoveAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                new JsonObject { ["error"] = "scriptremove requires 'name'" },
                Success: false,
                Error: "Missing script name" )).ConfigureAwait(false);
            return;
        }

        var name = nameElement.GetString()!;
        var removed = _runtime.RemoveScript(name);
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            BuildHandlerPayload(removed ? name : null, includeScripts: true),
            Success: removed,
            Error: removed ? null : "Script not found" )).ConfigureAwait(false);
    }

    private JsonObject BuildHandlerPayload(string? latestScript = null, bool includeScripts = false)
    {
        var handlers = _runtime.ListHandlers();
        var handlerArray = new JsonArray();
        foreach (var handler in handlers)
        {
            handlerArray.Add(handler);
        }

        var payload = new JsonObject
        {
            ["handlers"] = handlerArray
        };

        if (includeScripts)
        {
            var scriptArray = new JsonArray();
            foreach (var script in _runtime.ListScripts())
            {
                scriptArray.Add(script);
            }
            payload["scripts"] = scriptArray;
        }

        if (!string.IsNullOrWhiteSpace(latestScript))
        {
            payload["name"] = latestScript;
            payload["ack"] = true;
        }

        return payload;
    }
}