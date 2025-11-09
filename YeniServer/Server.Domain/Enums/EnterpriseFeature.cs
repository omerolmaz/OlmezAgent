namespace Server.Domain.Enums;

/// <summary>
/// Enterprise features (bitwise flags)
/// </summary>
[Flags]
public enum EnterpriseFeature : ulong
{
    None = 0,
    MultiUser = 1UL << 0,              // 0x1 - Multiple user accounts
    RoleBasedAccess = 1UL << 1,        // 0x2 - RBAC permissions
    ActiveDirectory = 1UL << 2,        // 0x4 - AD integration
    HighAvailability = 1UL << 3,       // 0x8 - HA setup
    LoadBalancing = 1UL << 4,          // 0x10 - Load balancer support
    CustomBranding = 1UL << 5,         // 0x20 - White-label
    FullApiAccess = 1UL << 6,          // 0x40 - Unrestricted API
    PrioritySupport = 1UL << 7,        // 0x80 - 24/7 support
    UnlimitedAuditLog = 1UL << 8,      // 0x100 - No audit retention limit
    AdvancedSecurity = 1UL << 9,       // 0x200 - Extended security features
    CommercialUse = 1UL << 10,         // 0x400 - Commercial deployment
    WhiteLabel = 1UL << 11,            // 0x800 - Rebrand rights
    All = ulong.MaxValue
}
