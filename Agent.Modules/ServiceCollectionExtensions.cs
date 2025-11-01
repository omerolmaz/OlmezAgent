using Agent.Abstractions;
using Agent.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Modules;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentModules(this IServiceCollection services)
    {
        services.AddSingleton<IAgentModule, CoreDiagnosticsModule>();
        services.AddSingleton<IAgentModule, InventoryModule>();
        services.AddSingleton<IAgentModule, SoftwareDistributionModule>();
        services.AddSingleton<IAgentModule, RemoteOperationsModule>();
        services.AddSingleton<IAgentModule, MessagingModule>();
        services.AddSingleton<IAgentModule, MaintenanceModule>();
        services.AddSingleton<JavaScriptRuntime>();
        services.AddSingleton<IAgentModule, JavaScriptBridgeModule>();
        return services;
    }
}
