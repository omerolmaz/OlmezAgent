using Agent.Transport;

namespace AgentHost;

public class AgentWorker : BackgroundService
{
    private readonly AgentWebSocketClient _client;
    private readonly ILogger<AgentWorker> _logger;

    public AgentWorker(AgentWebSocketClient client, ILogger<AgentWorker> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentWorker başlatıldı.");
        await _client.RunAsync(stoppingToken);
        _logger.LogInformation("AgentWorker durduruldu.");
    }
}
