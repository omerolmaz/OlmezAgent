# License Enforcement - olmez Agent

Bu dosya, lisans kısıtlamalarının nasıl uygulandığını açıklar.

## Community Edition Kısıtlamaları

### 1. Device Limit (50 Cihaz)

**Kod:**
```csharp
// Server.Application/Services/LicenseService.cs
public class LicenseService
{
    private const int COMMUNITY_DEVICE_LIMIT = 50;
    
    public async Task<bool> CanAddDeviceAsync()
    {
        var license = await GetCurrentLicenseAsync();
        
        if (license.Edition == LicenseEdition.Community)
        {
            var deviceCount = await _deviceRepository.GetActiveCountAsync();
            return deviceCount < COMMUNITY_DEVICE_LIMIT;
        }
        
        return true; // Enterprise - unlimited
    }
}
```

### 2. Feature Flags

**Kod:**
```csharp
// Server.Domain/Enums/LicenseEdition.cs
public enum LicenseEdition
{
    Community = 0,
    Enterprise = 1
}

// Server.Domain/Enums/EnterpriseFeature.cs
[Flags]
public enum EnterpriseFeature : ulong
{
    None = 0,
    MultiUser = 1 << 0,              // 0x1
    RoleBasedAccess = 1 << 1,        // 0x2
    ActiveDirectory = 1 << 2,        // 0x4
    HighAvailability = 1 << 3,       // 0x8
    LoadBalancing = 1 << 4,          // 0x10
    CustomBranding = 1 << 5,         // 0x20
    FullApiAccess = 1 << 6,          // 0x40
    PrioritySupport = 1 << 7,        // 0x80
    UnlimitedAuditLog = 1 << 8,      // 0x100
    AdvancedSecurity = 1 << 9,       // 0x200
    CommercialUse = 1 << 10,         // 0x400
    WhiteLabel = 1 << 11,            // 0x800
    All = ulong.MaxValue
}
```

### 3. License Validation

**Kod:**
```csharp
// Server.Domain/Entities/License.cs
public class License
{
    public Guid LicenseId { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public LicenseEdition Edition { get; set; }
    public EnterpriseFeature EnabledFeatures { get; set; }
    public int MaxDevices { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    
    public bool IsValid()
    {
        return IsActive && ExpiresAt > DateTime.UtcNow;
    }
    
    public bool HasFeature(EnterpriseFeature feature)
    {
        if (Edition == LicenseEdition.Community)
            return false;
            
        return EnabledFeatures.HasFlag(feature);
    }
}
```

### 4. Middleware (Feature Check)

**Kod:**
```csharp
// Server.Api/Middleware/LicenseCheckMiddleware.cs
public class LicenseCheckMiddleware
{
    public async Task InvokeAsync(HttpContext context, LicenseService licenseService)
    {
        var endpoint = context.GetEndpoint();
        var requiredFeature = endpoint?.Metadata.GetMetadata<RequireEnterpriseAttribute>();
        
        if (requiredFeature != null)
        {
            var license = await licenseService.GetCurrentLicenseAsync();
            
            if (!license.HasFeature(requiredFeature.Feature))
            {
                context.Response.StatusCode = 402; // Payment Required
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Enterprise feature required",
                    feature = requiredFeature.Feature.ToString(),
                    upgradeUrl = "https://olmezagent.com/pricing"
                });
                return;
            }
        }
        
        await _next(context);
    }
}

// Kullanım:
[RequireEnterprise(EnterpriseFeature.MultiUser)]
[HttpPost("api/users")]
public async Task<IActionResult> CreateUser(CreateUserRequest request)
{
    // Sadece Enterprise lisansı ile erişilebilir
}
```

### 5. UI'da Feature Gizleme

**Kod:**
```csharp
// Blazor Components
@inject LicenseService LicenseService

@if (await LicenseService.HasFeatureAsync(EnterpriseFeature.CustomBranding))
{
    <button>Customize Logo</button>
}
else
{
    <div class="upgrade-banner">
        <span>🔒 Enterprise Feature</span>
        <a href="/pricing">Upgrade to Enterprise</a>
    </div>
}
```

## License Key Format

**Format:**
```
OLMEZ-{EDITION}-{RANDOM}-{CHECKSUM}

Örnek:
OLMEZ-ENT-A3F9D2B1-8C4E7F2A  (Enterprise)
OLMEZ-COM-00000000-00000000  (Community - default)
```

**Generation:**
```csharp
public class LicenseKeyGenerator
{
    public string Generate(LicenseEdition edition, string customerEmail)
    {
        var prefix = edition == LicenseEdition.Enterprise ? "ENT" : "COM";
        var random = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        var checksum = ComputeChecksum(customerEmail, random);
        
        return $"OLMEZ-{prefix}-{random}-{checksum}";
    }
    
    private string ComputeChecksum(string email, string random)
    {
        using var sha256 = SHA256.Create();
        var input = $"{email}:{random}:olmez-secret-salt";
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8);
    }
}
```

## Telemetry (Usage Monitoring)

**Opt-out edilebilir:**

```json
// appsettings.json
{
  "Telemetry": {
    "Enabled": true,
    "OptOut": false,
    "ReportingInterval": "24:00:00"
  }
}
```

**Veri:**
- Device count
- License edition
- Feature usage statistics
- Uptime metrics

**Privacy:** Kişisel veri toplanmaz, sadece lisans uyumluluğu için istatistikler.

## Enforcement Timeline

1. **Soft Warning (Grace Period):** +5 devices → UI'da uyarı
2. **Hard Limit:** +10 devices → Yeni bağlantı reddedilir
3. **Audit:** 90 günde bir telemetry raporu

## Enterprise License Activation

**Server başlatma:**
```bash
# License key ile aktivasyon
YeniServer.exe --activate OLMEZ-ENT-A3F9D2B1-8C4E7F2A

# Email verification
YeniServer.exe --verify-license omer.olmez@sitetelekom.com.tr
```

**Otomatik aktivasyon:**
```json
// appsettings.json
{
  "License": {
    "Key": "OLMEZ-ENT-A3F9D2B1-8C4E7F2A",
    "Email": "omer.olmez@sitetelekom.com.tr"
  }
}
```
