using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agent.Abstractions;

public sealed record CommandResult(
    string Action,
    string CommandId,
    string? NodeId,
    string? SessionId,
    JsonObject Payload,
    bool Success = true,
    string? Error = null);
