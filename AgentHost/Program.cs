using Agent.Abstractions;
using Agent.Transport;
using Agent.Modules;
using AgentHost;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;

// Komut satırı argümanlarını kontrol et
if (args.Length > 0)
{
    var command = args[0].ToLowerInvariant();
    switch (command)
    {
        case "--install-service":
        case "-install":
            return await ServiceInstaller.InstallServiceAsync();
        
        case "--uninstall-service":
        case "-uninstall":
            return await ServiceInstaller.UninstallServiceAsync();
        
        case "--help":
        case "-h":
        case "/?":
            ServiceInstaller.ShowHelp();
            return 0;
    }
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(AppContext.BaseDirectory, "logs", "agent-.json"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "agent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

// Windows Service desteği ekle
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "olmezAgent";
});

// Use Serilog
builder.Services.AddSerilog();

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
builder.Services.AddSingleton<AgentWebSocketClient>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();

try
{
    Log.Information("olmez Agent başlatılıyor... (Mode: {Mode})", 
        Environment.UserInteractive ? "Console" : "Windows Service");
    host.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent beklenmedik bir hata ile sonlandı");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
