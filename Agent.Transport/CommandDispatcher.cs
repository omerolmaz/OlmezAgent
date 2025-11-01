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
        foreach (var module in _modules)
        {
            if (module.SupportedActions.Count > 0 &&
                module.SupportedActions.Any(a => string.Equals(a, command.Action, StringComparison.OrdinalIgnoreCase)) == false)
            {
                continue;
            }

            if (await module.CanHandleAsync(command, context).ConfigureAwait(false) == false)
            {
                continue;
            }

            var handled = await module.HandleAsync(command, context).ConfigureAwait(false);
            if (handled)
            {
                return;
            }
        }

        context.Logger?.LogWarning("Komut herhangi bir modül tarafından işlenmedi: {Action}", command.Action);
    }
}
