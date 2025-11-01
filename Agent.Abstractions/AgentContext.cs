using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Abstractions;

public sealed class AgentContext
{
    public AgentContext(
        IServiceProvider services,
        IAgentEventBus eventBus,
        IAgentResponseWriter responseWriter,
        AgentRuntimeOptions options)
    {
        Services = services;
        EventBus = eventBus;
        ResponseWriter = responseWriter;
        Options = options;
    }

    public IServiceProvider Services { get; }
    public ILogger? Logger => Services.GetService<ILoggerFactory>()?.CreateLogger("Agent");
    public IAgentEventBus EventBus { get; }
    public IAgentResponseWriter ResponseWriter { get; }
    public AgentRuntimeOptions Options { get; }
}
