namespace Server.Domain.Constants;

/// <summary>
/// Agent komut tipleri - YeniAgent'in desteklediği tüm komutlar
/// </summary>
public static class AgentCommands
{
    /// <summary>
    /// Komut tiplerini kategori bazında organize eder
    /// </summary>
    public static class Categories
    {
        // Protocol Commands (3)
        public const string ServerHello = "serverhello";
        public const string Registered = "registered";
        public const string Error = "error";

        // Diagnostics Commands (5)
        public const string Ping = "ping";
        public const string Status = "status";
        public const string AgentInfo = "agentinfo";
        public const string Versions = "versions";
        public const string ConnectionDetails = "connectiondetails";

        // Inventory Commands (11)
        public const string GetFullInventory = "getfullinventory";
        public const string GetInstalledSoftware = "getinstalledsoftware";
        public const string GetInstalledPatches = "getinstalledpatches";
        public const string GetPendingUpdates = "getpendingupdates";
        public const string SysInfo = "sysinfo";
        public const string CpuInfo = "cpuinfo";
        public const string NetInfo = "netinfo";
        public const string SmbiosInfo = "smbios";
        public const string VmDetect = "vm";
        public const string WifiScan = "wifiscan";
        public const string PerfCounters = "perfcounters";

        // Remote Operations Commands (16)
        public const string Console = "console";
        public const string Power = "power";
        public const string Service = "service";
        public const string ListFiles = "ls";
        public const string Download = "download";
        public const string Upload = "upload";
        public const string MakeDir = "mkdir";
        public const string Remove = "rm";
        public const string Zip = "zip";
        public const string Unzip = "unzip";
        public const string OpenUrl = "openurl";
        public const string Wallpaper = "wallpaper";
        public const string KvmMode = "kvmmode";
        public const string WakeOnLan = "wakeonlan";
        public const string ClipboardGet = "clipboardget";
        public const string ClipboardSet = "clipboardset";

        // Desktop Commands (11)
        public const string DesktopStart = "desktopstart";
        public const string DesktopStop = "desktopstop";
        public const string DesktopFrame = "desktopframe";
        public const string DesktopMouseMove = "desktopmousemove";
        public const string DesktopMouseClick = "desktopmouseclick";
        public const string DesktopMouseDown = "desktopmousedown";
        public const string DesktopMouseUp = "desktopmouseup";
        public const string DesktopKeyDown = "desktopkeydown";
        public const string DesktopKeyUp = "desktopkeyup";
        public const string DesktopKeyPress = "desktopkeypress";

        // File Monitoring Commands (4)
        public const string StartFileMonitor = "startfilemonitor";
        public const string StopFileMonitor = "stopfilemonitor";
        public const string GetFileChanges = "getfilechanges";
        public const string ListMonitors = "listmonitors";

        // Security Commands (6)
        public const string GetSecurityStatus = "getsecuritystatus";
        public const string GetAntivirusStatus = "getantivirusstatus";
        public const string GetFirewallStatus = "getfirewallstatus";
        public const string GetDefenderStatus = "getdefenderstatus";
        public const string GetUacStatus = "getuacstatus";
        public const string GetEncryptionStatus = "getencryptionstatus";

        // Event Log Commands (7)
        public const string GetEventLogs = "geteventlogs";
        public const string GetSecurityEvents = "getsecurityevents";
        public const string GetApplicationEvents = "getapplicationevents";
        public const string GetSystemEvents = "getsystemevents";
        public const string StartEventMonitor = "starteventmonitor";
        public const string StopEventMonitor = "stopeventmonitor";
        public const string ClearEventLog = "cleareventlog";

        // Software Distribution Commands (6)
        public const string InstallSoftware = "installsoftware";
        public const string UninstallSoftware = "uninstallsoftware";
        public const string InstallUpdates = "installupdates";
        public const string SchedulePatch = "schedulepatch";
        public const string InstallChoco = "installchoco";
        public const string InstallWithChoco = "installwithchoco";

        // Maintenance Commands (6)
        public const string AgentUpdate = "agentupdate";
        public const string AgentUpdateEx = "agentupdateex";
        public const string DownloadFile = "downloadfile";
        public const string Reinstall = "reinstall";
        public const string Log = "log";

        // Script Commands (4)
        public const string ScriptDeploy = "scriptdeploy";
        public const string ScriptReload = "scriptreload";
        public const string ScriptList = "scriptlist";
        public const string ScriptRemove = "scriptremove";

        // Messaging Commands (7)
        public const string AgentMsg = "agentmsg";
        public const string MessageBox = "messagebox";
        public const string Notify = "notify";
        public const string Toast = "toast";
        public const string Chat = "chat";
        public const string WebRtcSdp = "webrtcsdp";
        public const string WebRtcIce = "webrtcice";

        // Health Commands (3)
        public const string Health = "health";
        public const string Metrics = "metrics";
        public const string Uptime = "uptime";

        // Privacy Commands (2)
        public const string PrivacyBarShow = "privacybarshow";
        public const string PrivacyBarHide = "privacybarhide";

        // Audit Commands (2)
        public const string GetAuditLogs = "getauditlogs";
        public const string ClearAuditLogs = "clearauditlogs";
    }

    /// <summary>
    /// Tüm geçerli komutların listesi
    /// </summary>
    public static readonly HashSet<string> AllCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Protocol (3)
        Categories.ServerHello, Categories.Registered, Categories.Error,
        
        // Diagnostics (5)
        Categories.Ping, Categories.Status, Categories.AgentInfo, 
        Categories.Versions, Categories.ConnectionDetails,
        
        // Inventory (11)
        Categories.GetFullInventory, Categories.GetInstalledSoftware, 
        Categories.GetInstalledPatches, Categories.GetPendingUpdates,
        Categories.SysInfo, Categories.CpuInfo, Categories.NetInfo, 
        Categories.SmbiosInfo, Categories.VmDetect, Categories.WifiScan, 
        Categories.PerfCounters,
        
        // Remote Operations (16)
        Categories.Console, Categories.Power, Categories.Service, 
        Categories.ListFiles, Categories.Download, Categories.Upload,
        Categories.MakeDir, Categories.Remove, Categories.Zip, 
        Categories.Unzip, Categories.OpenUrl, Categories.Wallpaper,
        Categories.KvmMode, Categories.WakeOnLan, Categories.ClipboardGet, 
        Categories.ClipboardSet,
        
        // Desktop (11)
        Categories.DesktopStart, Categories.DesktopStop, Categories.DesktopFrame,
        Categories.DesktopMouseMove, Categories.DesktopMouseClick, 
        Categories.DesktopMouseDown, Categories.DesktopMouseUp,
        Categories.DesktopKeyDown, Categories.DesktopKeyUp, Categories.DesktopKeyPress,
        
        // File Monitoring (4)
        Categories.StartFileMonitor, Categories.StopFileMonitor, 
        Categories.GetFileChanges, Categories.ListMonitors,
        
        // Security (6)
        Categories.GetSecurityStatus, Categories.GetAntivirusStatus, 
        Categories.GetFirewallStatus, Categories.GetDefenderStatus,
        Categories.GetUacStatus, Categories.GetEncryptionStatus,
        
        // Event Log (7)
        Categories.GetEventLogs, Categories.GetSecurityEvents, 
        Categories.GetApplicationEvents, Categories.GetSystemEvents,
        Categories.StartEventMonitor, Categories.StopEventMonitor, 
        Categories.ClearEventLog,
        
        // Software (6)
        Categories.InstallSoftware, Categories.UninstallSoftware, 
        Categories.InstallUpdates, Categories.SchedulePatch,
        Categories.InstallChoco, Categories.InstallWithChoco,
        
        // Maintenance (6)
        Categories.AgentUpdate, Categories.AgentUpdateEx, 
        Categories.DownloadFile, Categories.Reinstall, Categories.Log,

        // Script (4)
        Categories.ScriptDeploy, Categories.ScriptReload, 
        Categories.ScriptList, Categories.ScriptRemove,

        // Messaging (7)
        Categories.AgentMsg, Categories.MessageBox, Categories.Notify, 
        Categories.Toast, Categories.Chat, Categories.WebRtcSdp, 
        Categories.WebRtcIce,
        
        // Health (3)
        Categories.Health, Categories.Metrics, Categories.Uptime,
        
        // Privacy (2)
        Categories.PrivacyBarShow, Categories.PrivacyBarHide,
        
        // Audit (2)
        Categories.GetAuditLogs, Categories.ClearAuditLogs
    };

    /// <summary>
    /// Komut validasyonu yapar
    /// </summary>
    public static bool IsValidCommand(string command)
    {
        return AllCommands.Contains(command);
    }

    /// <summary>
    /// Komutun kategorisini döner
    /// </summary>
    public static Enums.CommandCategory GetCategory(string command)
    {
        var cmd = command.ToLowerInvariant();
        
        // Protocol
        if (new[] { "serverhello", "registered", "error" }.Contains(cmd))
            return Enums.CommandCategory.Protocol;
            
        // Diagnostics
        if (new[] { "ping", "status", "agentinfo", "versions", "connectiondetails" }.Contains(cmd))
            return Enums.CommandCategory.Diagnostics;
            
        // Inventory
        if (new[] { "getfullinventory", "getinstalledsoftware", "getinstalledpatches", 
                   "getpendingupdates", "sysinfo", "cpuinfo", "netinfo", "smbios", 
                   "vm", "wifiscan", "perfcounters" }.Contains(cmd))
            return Enums.CommandCategory.Inventory;
            
        // Remote Operations
        if (new[] { "console", "power", "service", "ls", "download", "upload", 
                   "mkdir", "rm", "zip", "unzip", "openurl", "wallpaper", 
                   "kvmmode", "wakeonlan", "clipboardget", "clipboardset" }.Contains(cmd))
            return Enums.CommandCategory.RemoteOperations;
            
        // Desktop
        if (cmd.StartsWith("desktop"))
            return Enums.CommandCategory.Desktop;
            
        // File Monitoring
        if (cmd.Contains("filemonitor") || cmd.Contains("getfilechanges") || cmd == "listmonitors")
            return Enums.CommandCategory.FileMonitoring;
            
        // Security
        if (cmd.Contains("security") || cmd.Contains("antivirus") || cmd.Contains("firewall") || 
            cmd.Contains("defender") || cmd.Contains("uac") || cmd.Contains("encryption"))
            return Enums.CommandCategory.Security;
            
        // Event Log
        if (cmd.Contains("event") || cmd.Contains("log"))
            return Enums.CommandCategory.EventLog;
            
        // Software
        if (cmd.Contains("install") || cmd.Contains("uninstall") || cmd.Contains("update") || cmd.Contains("patch"))
            return Enums.CommandCategory.Software;
            
        // Maintenance
        if (cmd.Contains("agentupdate") || cmd == "downloadfile" || cmd == "reinstall" || cmd == "versions" || cmd == "log")
            return Enums.CommandCategory.Maintenance;
            
        // Scripts
        if (cmd.Contains("scriptdeploy") || cmd.Contains("scriptreload") || cmd.Contains("scriptlist") || cmd.Contains("scriptremove"))
            return Enums.CommandCategory.Scripts;
            
        // Messaging
        if (cmd.Contains("msg") || cmd.Contains("message") || cmd.Contains("notify") || 
            cmd.Contains("toast") || cmd.Contains("chat") || cmd.Contains("webrtc"))
            return Enums.CommandCategory.Messaging;
            
        // Health
        if (cmd == "health" || cmd == "metrics" || cmd == "uptime")
            return Enums.CommandCategory.Health;
            
        // Privacy
        if (cmd.Contains("privacy"))
            return Enums.CommandCategory.Privacy;
            
        // Audit
        if (cmd.Contains("audit"))
            return Enums.CommandCategory.Audit;
            
        return Enums.CommandCategory.Protocol; // Default
    }
}
