using Agent.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Transport;

public sealed class InMemoryAgentEventBus : IAgentEventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<object, CancellationToken, ValueTask>>> _handlers = new();

    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event is null)
        {
            return;
        }

        if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            foreach (var handler in handlers.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var task = handler(@event!, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                {
                    await task.ConfigureAwait(false);
                }
            }
        }
    }

    public void RegisterHandler<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler)
    {
        var wrapped = new Func<object, CancellationToken, ValueTask>((obj, token) =>
        {
            if (obj is TEvent typed)
            {
                return handler(typed, token);
            }

            return ValueTask.CompletedTask;
        });

        var list = _handlers.GetOrAdd(typeof(TEvent), _ => new List<Func<object, CancellationToken, ValueTask>>());
        lock (list)
        {
            list.Add(wrapped);
        }
    }
}
