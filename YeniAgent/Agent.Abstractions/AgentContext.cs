using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Abstractions;

public sealed class AgentContext
{
    private DateTime _startTime = DateTime.UtcNow;
    
    public AgentContext(
        IServiceProvider services,
        IAgentEventBus eventBus,
        IAgentResponseWriter responseWriter,
        AgentRuntimeOptions options,
        AgentRights userRights = AgentRights.FullAdministrator)
    {
        Services = services;
        EventBus = eventBus;
        ResponseWriter = responseWriter;
        Options = options;
        UserRights = userRights;
    }

    public IServiceProvider Services { get; }
    public ILogger? Logger => Services.GetService<ILoggerFactory>()?.CreateLogger("Agent");
    public IAgentEventBus EventBus { get; }
    public IAgentResponseWriter ResponseWriter { get; }
    public AgentRuntimeOptions Options { get; }
    public AgentRights UserRights { get; set; }
    
    /// <summary>
    /// Agent'ın başlatılma zamanı (UTC)
    /// </summary>
    public DateTime StartTime => _startTime;
    
    /// <summary>
    /// Agent'ın çalışma süresi (uptime)
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - _startTime;
    
    /// <summary>
    /// Sunucu ile bağlantı durumu
    /// </summary>
    public ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.Disconnected;
    
    /// <summary>
    /// Son bağlantı zamanı (UTC)
    /// </summary>
    public DateTime? LastConnectedTime { get; set; }
    
    /// <summary>
    /// Bağlantı detaylarını içeren yapı
    /// </summary>
    public ConnectionDetails GetConnectionDetails()
    {
        return new ConnectionDetails
        {
            Status = ConnectionStatus.ToString(),
            NewVersion = Options.AgentVersion ?? "2.0.0",
            ServerUrl = Options.ServerEndpoint?.ToString() ?? "local",
            ServerId = ExtractServerIdFromUrl(Options.ServerEndpoint),
            GroupName = Options.GroupName ?? "Sitetelekom",
            GroupId = Options.GroupId ?? string.Empty,
            OSName = Options.OSName ?? Environment.OSVersion.ToString(),
            AutoProxy = Options.AutoProxy,
            StartTime = _startTime,
            Uptime = Uptime
        };
    }
    
    private string ExtractServerIdFromUrl(Uri? url)
    {
        if (url == null) return string.Empty;
        
        // URL'den server ID'yi çıkar (örn: 1402BEF6CD8A16D1E7B9DA3CA9D0D15245EF54B...)
        var path = url.AbsolutePath;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Son segment genellikle server/mesh ID'dir
        return segments.Length > 0 ? segments[^1] : string.Empty;
    }
}

/// <summary>
/// Bağlantı durumu
/// </summary>
public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

/// <summary>
/// Bağlantı detayları (MeshCentral "Connection Details" dialog'una benzer)
/// </summary>
public sealed class ConnectionDetails
{
    public string Status { get; init; } = string.Empty;
    public string NewVersion { get; init; } = string.Empty;
    public string ServerUrl { get; init; } = string.Empty;
    public string ServerId { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string OSName { get; init; } = string.Empty;
    public bool AutoProxy { get; init; }
    public DateTime StartTime { get; init; }
    public TimeSpan Uptime { get; init; }
}
