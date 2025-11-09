namespace Server.Domain.Enums;

/// <summary>
/// User permission flags (bitwise)
/// </summary>
[Flags]
public enum UserRights : ulong
{
    None = 0,
    ViewDevices = 1UL << 0,            // 0x1 - View device list
    ManageDevices = 1UL << 1,          // 0x2 - Add/remove devices
    ExecuteCommands = 1UL << 2,        // 0x4 - Run agent commands
    ViewFiles = 1UL << 3,              // 0x8 - Browse device files
    ManageFiles = 1UL << 4,            // 0x10 - Upload/download files
    ViewUsers = 1UL << 5,              // 0x20 - View user list
    ManageUsers = 1UL << 6,            // 0x40 - Create/edit users
    ViewGroups = 1UL << 7,             // 0x80 - View groups
    ManageGroups = 1UL << 8,           // 0x100 - Create/edit groups
    ViewLogs = 1UL << 9,               // 0x200 - Access audit logs
    ManageLicense = 1UL << 10,         // 0x400 - Manage licensing
    SystemAdmin = 1UL << 11,           // 0x800 - Full admin rights
    All = ulong.MaxValue
}
