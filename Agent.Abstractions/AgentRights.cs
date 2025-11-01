using System;

namespace Agent.Abstractions;

/// <summary>
/// Agent yetki sabitleri
/// </summary>
[Flags]
public enum AgentRights : ulong
{
    None = 0,

    // Temel yetkiler
    EditAgent = 1,
    ManageUsers = 2,
    ManageComputers = 4,
    RemoteControl = 8,
    AgentConsole = 16,
    ServerFiles = 32,
    WakeDevice = 64,
    SetNotes = 128,

    // Uzak işlem yetkileri
    RemoteViewOnly = 256,
    NoTerminal = 512,
    NoFiles = 1024,
    NoAMT = 2048,
    DesktopLimitedInput = 4096,

    // İleri düzey yetkiler
    LimitedDesktop = 8192,
    LimitEvents = 16384,
    ChatNotify = 32768,
    Uninstall = 65536,
    NoRemoteDesktop = 131072,
    NoRemoteTerminal = 262144,
    NoRemoteFiles = 524288,

    // Güvenlik ve izleme
    RemoteCommands = 1048576,
    ResetOff = 2097152,
    GuestSharing = 4194304,

    // Tam yetki
    FullAdministrator = ulong.MaxValue
}

public static class AgentRightsExtensions
{
    public static bool HasRight(this AgentRights rights, AgentRights required)
    {
        if (rights == AgentRights.FullAdministrator) return true;
        return (rights & required) == required;
    }

    public static bool CanExecuteCommand(this AgentRights rights, string action)
    {
        return action.ToLowerInvariant() switch
        {
            // Konsol komutları
            "console" => rights.HasRight(AgentRights.AgentConsole) && !rights.HasRight(AgentRights.NoRemoteTerminal),

            // Dosya işlemleri
            "ls" or "download" or "upload" or "mkdir" or "rm" or "zip" or "unzip"
                => rights.HasRight(AgentRights.ServerFiles) && !rights.HasRight(AgentRights.NoRemoteFiles),

            // Uzak masaüstü
            "kvmmode" or "desktopstream" or "desktopmousemove" or "desktopkeyboard"
                => rights.HasRight(AgentRights.RemoteControl) && !rights.HasRight(AgentRights.NoRemoteDesktop),

            // Güç yönetimi
            "power" or "wakeonlan" => rights.HasRight(AgentRights.WakeDevice),

            // Servis yönetimi
            "service" => rights.HasRight(AgentRights.ManageComputers),

            // Agent güncelleme/kaldırma
            "agentupdate" or "agentupdateex" or "reinstall" => rights.HasRight(AgentRights.Uninstall),

            // Bilgi toplama (herkes erişebilir)
            "ping" or "status" or "agentinfo" or "versions" or "sysinfo" or "cpuinfo" or "netinfo"
            or "getfullinventory" or "getinstalledsoftware" or "getinstalledpatches" or "getpendingupdates"
                => true,

            // Varsayılan: RemoteCommands yetkisi
            _ => rights.HasRight(AgentRights.RemoteCommands)
        };
    }
}
