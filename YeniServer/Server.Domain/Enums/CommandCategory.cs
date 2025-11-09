namespace Server.Domain.Enums;

/// <summary>
/// Command kategorileri - Agent modüllerine göre
/// </summary>
public enum CommandCategory
{
    Protocol = 0,           // ProtocolModule
    Diagnostics = 1,        // CoreDiagnosticsModule
    Inventory = 2,          // InventoryModule
    RemoteOperations = 3,   // RemoteOperationsModule
    Desktop = 4,            // DesktopModule
    FileMonitoring = 5,     // FileMonitoringModule
    Security = 6,           // SecurityMonitoringModule
    EventLog = 7,           // EventLogModule
    Software = 8,           // SoftwareDistributionModule
    Maintenance = 9,        // MaintenanceModule
    Messaging = 10,         // MessagingModule
    Health = 11,            // HealthCheckModule
    Privacy = 12,           // PrivacyModule
    Audit = 13,            // AuditModule
    Scripts = 14           // JavaScriptBridgeModule
}
