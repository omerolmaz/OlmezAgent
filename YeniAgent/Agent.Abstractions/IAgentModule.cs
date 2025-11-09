using System.Collections.Generic;

namespace Agent.Abstractions;

/// <summary>
/// Ajan içindeki tüm işlevsel modüllerin uygulaması gereken temel sözleşme.
/// </summary>
public interface IAgentModule : IAsyncDisposable
{
    string Name { get; }
    IReadOnlyCollection<string> SupportedActions { get; }

    /// <summary>
    /// Komutun modül tarafından çalıştırılıp çalıştırılamayacağını belirler.
    /// </summary>
    Task<bool> CanHandleAsync(AgentCommand command, AgentContext context);

    /// <summary>
    /// Komutu işler. İşlem sonunda <c>true</c> dönerse sonraki modüller devreye girmez.
    /// </summary>
    Task<bool> HandleAsync(AgentCommand command, AgentContext context);
}
