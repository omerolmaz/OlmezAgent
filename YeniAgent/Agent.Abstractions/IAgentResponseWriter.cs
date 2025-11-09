using System.Threading;
using System.Threading.Tasks;

namespace Agent.Abstractions;

public interface IAgentResponseWriter
{
    Task SendAsync(CommandResult result, CancellationToken cancellationToken = default);
}
