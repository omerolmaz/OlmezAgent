using System;

namespace Agent.Abstractions;

public sealed class AgentRuntimeOptions
{
    /// <summary>
    /// WebSocket sunucu adresi (wss:// veya ws://)
    /// </summary>
    public required Uri ServerEndpoint { get; init; }
    
    /// <summary>
    /// Agent'ın benzersiz cihaz kimliği (NodeId)
    /// </summary>
    public string? DeviceId { get; init; }
    
    /// <summary>
    /// Sunucu kayıt anahtarı (enrollment/registration)
    /// </summary>
    public string? EnrollmentKey { get; init; }
    
    /// <summary>
    /// JavaScript modül desteği (ClearScript V8)
    /// </summary>
    public bool EnableJavascriptModules { get; init; } = true;
    
    /// <summary>
    /// Komut çalıştırma timeout süresi
    /// </summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Sunucu sertifika parmak izi (SHA-256/SHA-384) - Certificate Pinning için
    /// </summary>
    public string? ServerCertificateHash { get; init; }
    
    /// <summary>
    /// Sertifika hash algoritması (sha256, sha384, sha512)
    /// </summary>
    public string ServerCertificateHashAlgorithm { get; init; } = "sha384";
    
    /// <summary>
    /// Grup adı (Group Name) - Agent'ın dahil olduğu grup
    /// </summary>
    public string? GroupName { get; init; }
    
    /// <summary>
    /// Grup kimliği (Group ID) - Mesh identifier
    /// </summary>
    public string? GroupId { get; init; }
    
    /// <summary>
    /// İşletim sistemi adı (OS Name) - Windows, Linux, macOS
    /// </summary>
    public string? OSName { get; init; }
    
    /// <summary>
    /// Agent'ın görünen adı (Display Name)
    /// </summary>
    public string? AgentDisplayName { get; init; }
    
    /// <summary>
    /// Agent versiyonu
    /// </summary>
    public string? AgentVersion { get; init; }
    
    /// <summary>
    /// Auto Proxy desteği (otomatik proxy tespiti)
    /// </summary>
    public bool AutoProxy { get; init; } = false;
    
    /// <summary>
    /// Manuel Proxy sunucu adresi
    /// </summary>
    public string? ProxyServer { get; init; }
    
    /// <summary>
    /// Otomatik yeniden bağlanma aralığı
    /// </summary>
    public TimeSpan ReconnectInterval { get; init; } = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Maksimum yeniden bağlanma aralığı (exponential backoff için)
    /// </summary>
    public TimeSpan MaxReconnectInterval { get; init; } = TimeSpan.FromMinutes(5);
}
