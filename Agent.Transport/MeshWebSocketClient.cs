using System;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent.Abstractions;
using Microsoft.Extensions.Logging;

namespace Agent.Transport;

public sealed class MeshWebSocketClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private ClientWebSocket? _socket;
    private readonly ICommandDispatcher _dispatcher;
    private readonly AgentContext _context;
    private readonly ILogger<MeshWebSocketClient> _logger;

    public MeshWebSocketClient(
        ICommandDispatcher dispatcher,
        AgentContext context,
        ILogger<MeshWebSocketClient> logger)
    {
        _dispatcher = dispatcher;
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested == false)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
                await ReceiveLoopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (AuthenticationException ex)
            {
                _logger.LogCritical(ex, "TLS doğrulaması başarısız oldu. Yeniden denemeyeceğim.");
                break;
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.NotAWebSocket)
            {
                _logger.LogCritical(ex, "Sunucu bir WebSocket uç noktası değil veya el sıkışma reddedildi.");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket bağlantı hatası, yeniden denenecek.");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_socket is { State: WebSocketState.Open })
        {
            return;
        }

        if (_socket is not null)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None)
                .ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            _socket.Dispose();
            _socket = null;
        }

        var socket = CreateSocket();
        await socket.ConnectAsync(_context.Options.ServerEndpoint, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sunucuya bağlandı: {Endpoint}", _context.Options.ServerEndpoint);

        await PerformHandshakeAsync(socket, cancellationToken).ConfigureAwait(false);
        _socket = socket;
    }

    private ClientWebSocket CreateSocket()
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        socket.Options.RemoteCertificateValidationCallback = RemoteCertificateValidation;
        if (!string.IsNullOrEmpty(_context.Options.EnrollmentKey))
        {
            socket.Options.SetRequestHeader("x-agent-key", _context.Options.EnrollmentKey);
        }

        return socket;
    }

    private bool RemoteCertificateValidation(object? sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors != SslPolicyErrors.None)
        {
            _logger.LogWarning("TLS sertifika hatası: {Errors}", errors);
        }

        try
        {
            return CertificatePinValidator.ValidateCertificate(certificate, _context.Options, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sertifika doğrulaması sırasında hata oluştu.");
            return false;
        }
    }

    private async Task PerformHandshakeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var hello = BuildHelloPayload();
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(hello, SerializerOptions));
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Handshake paketi gönderildi.");
    }

    private JsonObject BuildHelloPayload()
    {
        var deviceId = _context.Options.DeviceId ?? Environment.MachineName;
        var payload = new JsonObject
        {
            ["action"] = "agenthello",
            ["deviceId"] = deviceId,
            ["os"] = Environment.OSVersion.VersionString,
            ["architecture"] = Environment.Is64BitOperatingSystem ? "x64" : "x86",
            ["processArch"] = Environment.Is64BitProcess ? "x64" : "x86",
            ["username"] = Environment.UserName,
            ["machineName"] = Environment.MachineName,
            ["domain"] = Environment.UserDomainName,
            ["agentVersion"] = typeof(MeshWebSocketClient).Assembly.GetName().Version?.ToString() ?? "0.0.0"
        };

        if (!string.IsNullOrWhiteSpace(_context.Options.EnrollmentKey))
        {
            payload["enrollmentKey"] = _context.Options.EnrollmentKey;
        }

        return payload;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        while (_socket is { State: WebSocketState.Open } socket && cancellationToken.IsCancellationRequested == false)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogWarning("Sunucu bağlantıyı kapattı: {Status} - {Description}",
                    result.CloseStatus, result.CloseStatusDescription);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", cancellationToken).ConfigureAwait(false);
                break;
            }

            if (result.Count == 0)
            {
                continue;
            }

            var payload = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            CommandEnvelope? envelope = null;
            try
            {
                envelope = JsonSerializer.Deserialize<CommandEnvelope>(payload, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Geçersiz komut paketi alındı: {Payload}", payload);
            }

            if (envelope is null)
            {
                continue;
            }

            var command = AgentCommand.FromEnvelope(envelope, cancellationToken);
            await _dispatcher.DispatchAsync(command, _context).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket is { State: WebSocketState.Open } socket)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None)
                .ConfigureAwait(false);
        }

        _socket?.Dispose();
        _socket = null;
    }
}
