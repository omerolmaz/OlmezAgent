using Agent.Abstractions;
using Agent.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Modules;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentModules(this IServiceCollection services)
    {
        // Protocol module (must be first to handle serverhello)
        services.AddSingleton<IAgentModule, ProtocolModule>();
        
        // Core modules
        services.AddSingleton<IAgentModule, CoreDiagnosticsModule>();
        services.AddSingleton<IAgentModule, HealthCheckModule>();

        // Inventory & system modules
        services.AddSingleton<IAgentModule, InventoryModule>();
        services.AddSingleton<IAgentModule, SoftwareDistributionModule>();

        // Remote operations
        services.AddSingleton<IAgentModule, RemoteOperationsModule>();
        services.AddSingleton<IAgentModule, DesktopModule>();

        // Communication
        services.AddSingleton<IAgentModule, MessagingModule>();
        services.AddSingleton<IAgentModule, PrivacyModule>();

        // Maintenance
        services.AddSingleton<IAgentModule, MaintenanceModule>();

        // Security & monitoring
        services.AddSingleton<IAgentModule, SecurityMonitoringModule>();
        services.AddSingleton<IAgentModule, EventLogModule>();
        services.AddSingleton<IAgentModule, FileMonitoringModule>();
        services.AddSingleton<IAgentModule, AuditModule>();

        // Scripting
        services.AddSingleton<JavaScriptRuntime>();
        services.AddSingleton<IAgentModule, JavaScriptBridgeModule>();

        return services;
    }
}
