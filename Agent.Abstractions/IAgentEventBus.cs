using System;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Abstractions;

public interface IAgentEventBus
{
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
    void RegisterHandler<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler);
}
