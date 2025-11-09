using Agent.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Transport;

public sealed class NullResponseWriter : IAgentResponseWriter
{
    public Task SendAsync(CommandResult result, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

