using System;

namespace Agent.Abstractions;

public sealed class AgentRuntimeOptions
{
    public required Uri ServerEndpoint { get; init; }
    public string? DeviceId { get; init; }
    public string? EnrollmentKey { get; init; }
    public bool EnableJavascriptModules { get; init; } = true;
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public string? ServerCertificateHash { get; init; }
    public string ServerCertificateHashAlgorithm { get; init; } = "sha384";
}
