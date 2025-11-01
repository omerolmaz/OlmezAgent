using Agent.Abstractions;
using Agent.Transport;
using Agent.Modules;
using AgentHost;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentRuntimeOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddSingleton<IAgentEventBus, InMemoryAgentEventBus>();
builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddSingleton<IAgentResponseWriter, NullResponseWriter>();
builder.Services.AddAgentModules();
builder.Services.AddSingleton<AgentContext>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AgentRuntimeOptions>>().Value
                 ?? throw new InvalidOperationException("Agent configuration section is missing.");
    if (options.ServerEndpoint is null)
    {
        throw new InvalidOperationException("Agent.ServerEndpoint configuration is required.");
    }

    return new AgentContext(
        sp,
        sp.GetRequiredService<IAgentEventBus>(),
        sp.GetRequiredService<IAgentResponseWriter>(),
        options);
});

// Modules will be registered via DI; empty collection is supported during bootstrap.
builder.Services.AddSingleton<MeshWebSocketClient>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
