using Agent.Abstractions;
using Agent.Scripting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Agent.Modules;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all agent modules. Desktop module is ALWAYS registered.
    /// It will detect service mode internally and spawn helper process when needed.
    /// </summary>
    public static IServiceCollection AddAgentModules(this IServiceCollection services)
    {
        var msg = "═══ AddAgentModules: DesktopModule ALWAYS registered (hybrid mode) ═══";
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
        
        // Protocol module (must be first to handle serverhello)
        services.AddSingleton<IAgentModule, ProtocolModule>();
        
        // Core modules
        services.AddSingleton<IAgentModule, CoreDiagnosticsModule>();
        services.AddSingleton<IAgentModule, HealthCheckModule>();

        // Inventory & system modules
        services.AddSingleton<IAgentModule, InventoryModule>();
        services.AddSingleton<IAgentModule, SoftwareModule>();
        services.AddSingleton<IAgentModule, SoftwareDistributionModule>();

        // Remote operations
        services.AddSingleton<IAgentModule, RemoteOperationsModule>();
        
        // Desktop module - Always registered
        // Will spawn user-session helper process when needed (MeshCentral style)
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
