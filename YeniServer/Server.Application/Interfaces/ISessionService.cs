using Server.Application.Common;
using Server.Application.DTOs.Session;

namespace Server.Application.Interfaces;

/// <summary>
/// Agent session yönetimi için servis interface
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Yeni session başlatır (desktop, console, filemonitor, eventmonitor)
    /// </summary>
    Task<Result<SessionDto>> StartSessionAsync(StartSessionRequest request, Guid userId);

    /// <summary>
    /// Session'ı sonlandırır
    /// </summary>
    Task<Result<bool>> EndSessionAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Session bilgisini getirir
    /// </summary>
    Task<Result<SessionDto>> GetSessionAsync(Guid sessionId);

    /// <summary>
    /// Belirli bir device'ın aktif session'larını getirir
    /// </summary>
    Task<Result<List<SessionDto>>> GetActiveSessionsAsync(Guid deviceId);

    /// <summary>
    /// Session'ın son aktivite zamanını günceller
    /// </summary>
    Task<Result<bool>> UpdateSessionActivityAsync(Guid sessionId);

    /// <summary>
    /// Session'a veri ekler/günceller
    /// </summary>
    Task<Result<bool>> UpdateSessionDataAsync(Guid sessionId, string data);

    /// <summary>
    /// Timeout olan session'ları temizler
    /// </summary>
    Task<Result<int>> CleanupInactiveSessionsAsync(TimeSpan inactivityThreshold);
}
