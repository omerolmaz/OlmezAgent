using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Agent.Abstractions;

/// <summary>
/// Temel agent komut yapısını temsil eder.
/// </summary>
public sealed record AgentCommand(
    string Action,
    string? NodeId,
    string? SessionId,
    JsonElement Payload,
    CancellationToken CancellationToken)
{
    public static AgentCommand FromEnvelope(CommandEnvelope envelope, CancellationToken token) =>
        new(envelope.Action, envelope.NodeId, envelope.SessionId, envelope.Payload, token);
}

/// <summary>
/// Gelen JSON mesajının ham halini tutar.
/// </summary>
public sealed record CommandEnvelope(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("nodeid")] string? NodeId,
    [property: JsonPropertyName("sessionid")] string? SessionId,
    [property: JsonPropertyName("data")] JsonElement Payload);
