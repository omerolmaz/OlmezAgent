using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Agent.Abstractions;

/// <summary>
/// Temel agent komut yapısını temsil eder.
/// </summary>
public sealed record AgentCommand(
    string Action,
    string CommandId,
    string? NodeId,
    string? SessionId,
    JsonElement Payload,
    CancellationToken CancellationToken)
{
    public static AgentCommand FromEnvelope(CommandEnvelope envelope, CancellationToken token) =>
        new(
            envelope.GetAction(),
            envelope.CommandId,
            envelope.NodeId,
            envelope.SessionId,
            NormalizePayload(envelope.Payload),
            token);

    private static JsonElement NormalizePayload(JsonElement payload)
    {
        try
        {
            if (payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("parameters", out var parametersElement))
            {
                if (parametersElement.ValueKind == JsonValueKind.Object)
                {
                    return parametersElement.Clone();
                }

                if (parametersElement.ValueKind == JsonValueKind.String)
                {
                    var raw = parametersElement.GetString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        using var doc = JsonDocument.Parse(raw);
                        return doc.RootElement.Clone();
                    }
                }
            }
            else if (payload.ValueKind == JsonValueKind.String)
            {
                var raw = payload.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    using var doc = JsonDocument.Parse(raw);
                    return doc.RootElement.Clone();
                }
            }
        }
        catch
        {
            // Ignore parse errors and fall back to original payload
        }

        if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
        {
            using var emptyDoc = JsonDocument.Parse("{}");
            return emptyDoc.RootElement.Clone();
        }

        return payload.Clone();
    }
}

/// <summary>
/// Gelen JSON mesajının ham halini tutar.
/// </summary>
public sealed record CommandEnvelope(
    [property: JsonPropertyName("commandType")] string? CommandType,
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("commandId")] string CommandId,
    [property: JsonPropertyName("nodeId")] string? NodeId,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("parameters")] JsonElement Payload)
{
    // commandType veya action'dan birini kullan (commandType öncelikli)
    public string GetAction() => CommandType ?? Action ?? string.Empty;
};
