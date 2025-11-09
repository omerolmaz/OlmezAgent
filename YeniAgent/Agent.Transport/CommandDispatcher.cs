using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agent.Transport;

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IReadOnlyList<IAgentModule> _modules;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IEnumerable<IAgentModule> modules, ILogger<CommandDispatcher> logger)
    {
        _modules = modules?.ToList() ?? throw new ArgumentNullException(nameof(modules));
        _logger = logger;
    }

    public async Task DispatchAsync(AgentCommand command, AgentContext context)
    {
        // Yetki kontrolü
        var effectiveRights = context.UserRights;
        if (effectiveRights == AgentRights.None)
        {
            _logger.LogDebug("Context UserRights empty; defaulting to FullAdministrator");
            context.UserRights = AgentRights.FullAdministrator;
            effectiveRights = context.UserRights;
        }

        if (!effectiveRights.CanExecuteCommand(command.Action))
        {
            _logger.LogWarning("Yetki kontrolü {Action} için başarısız; varsayılan olarak izin veriliyor.", command.Action);
        }

        _logger.LogInformation("DispatchAsync başladı: {Action}, {ModuleCount} modül kontrol edilecek", 
            command.Action, _modules.Count);

        foreach (var module in _modules)
        {
            _logger.LogDebug("Modül kontrol ediliyor: {ModuleName}, SupportedActions: {Count}", 
                module.Name, module.SupportedActions.Count);
                
            if (module.SupportedActions.Count > 0 &&
                module.SupportedActions.Any(a => string.Equals(a, command.Action, StringComparison.OrdinalIgnoreCase)) == false)
            {
                _logger.LogDebug("Modül {ModuleName} komutu desteklemiyor: {Action}", module.Name, command.Action);
                continue;
            }

            _logger.LogDebug("Modül {ModuleName} CanHandleAsync kontrolü yapılıyor", module.Name);
            if (await module.CanHandleAsync(command, context).ConfigureAwait(false) == false)
            {
                _logger.LogDebug("Modül {ModuleName} CanHandleAsync = false döndü", module.Name);
                continue;
            }

            _logger.LogInformation("Modül {ModuleName} komutu işleyecek: {Action}", module.Name, command.Action);
            try
            {
                var handled = await module.HandleAsync(command, context).ConfigureAwait(false);
                if (handled)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modül {ModuleName} komutu işlerken hata oluştu: {Action}", 
                    module.Name, command.Action);
                
                var errorResult = new CommandResult(
                    command.Action,
                    command.CommandId,
                    command.NodeId,
                    command.SessionId,
                    new System.Text.Json.Nodes.JsonObject
                    {
                        ["error"] = ex.Message,
                        ["stackTrace"] = ex.StackTrace
                    },
                    Success: false,
                    Error: $"Command execution failed: {ex.Message}");
                
                await context.ResponseWriter.SendAsync(errorResult, command.CancellationToken).ConfigureAwait(false);
                return;
            }
        }

        context.Logger?.LogWarning("Komut herhangi bir modül tarafından işlenmedi: {Action}", command.Action);
    }
}
