using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.Application.Common;
using Server.Application.DTOs.Session;
using Server.Application.Interfaces;
using Server.Domain.Entities;
using Server.Infrastructure.Data;

namespace Server.Application.Services;

public class SessionService : ISessionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SessionService> _logger;

    public SessionService(ApplicationDbContext context, ILogger<SessionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<SessionDto>> StartSessionAsync(StartSessionRequest request, Guid userId)
    {
        try
        {
            // Session ID oluştur (agent ile paylaşılacak)
            var sessionId = $"{request.SessionType}_{Guid.NewGuid():N}";

            var session = new AgentSession
            {
                Id = Guid.NewGuid(),
                DeviceId = request.DeviceId,
                UserId = userId,
                SessionType = request.SessionType,
                SessionId = sessionId,
                SessionData = request.InitialData,
                IsActive = true,
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            _context.AgentSessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Session started: {SessionId} for device {DeviceId}, type: {Type}", 
                sessionId, request.DeviceId, request.SessionType);

            return Result<SessionDto>.Ok(new SessionDto
            {
                Id = session.Id,
                DeviceId = session.DeviceId,
                UserId = session.UserId,
                SessionType = session.SessionType,
                SessionId = session.SessionId,
                SessionData = session.SessionData,
                IsActive = session.IsActive,
                StartedAt = session.StartedAt,
                LastActivityAt = session.LastActivityAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session for device {DeviceId}", request.DeviceId);
            return Result<SessionDto>.Fail("Failed to start session", "SESSION_START_ERROR");
        }
    }

    public async Task<Result<bool>> EndSessionAsync(Guid sessionId, Guid userId)
    {
        try
        {
            var session = await _context.AgentSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
                return Result<bool>.Fail("Session not found", "SESSION_NOT_FOUND");

            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Session ended: {SessionId}", session.SessionId);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId}", sessionId);
            return Result<bool>.Fail("Failed to end session", "SESSION_END_ERROR");
        }
    }

    public async Task<Result<SessionDto>> GetSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.AgentSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return Result<SessionDto>.Fail("Session not found", "SESSION_NOT_FOUND");

            return Result<SessionDto>.Ok(new SessionDto
            {
                Id = session.Id,
                DeviceId = session.DeviceId,
                UserId = session.UserId,
                SessionType = session.SessionType,
                SessionId = session.SessionId,
                SessionData = session.SessionData,
                IsActive = session.IsActive,
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
                LastActivityAt = session.LastActivityAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session {SessionId}", sessionId);
            return Result<SessionDto>.Fail("Failed to get session", "SESSION_GET_ERROR");
        }
    }

    public async Task<Result<List<SessionDto>>> GetActiveSessionsAsync(Guid deviceId)
    {
        try
        {
            var sessions = await _context.AgentSessions
                .Where(s => s.DeviceId == deviceId && s.IsActive)
                .OrderByDescending(s => s.StartedAt)
                .Select(s => new SessionDto
                {
                    Id = s.Id,
                    DeviceId = s.DeviceId,
                    UserId = s.UserId,
                    SessionType = s.SessionType,
                    SessionId = s.SessionId,
                    SessionData = s.SessionData,
                    IsActive = s.IsActive,
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    LastActivityAt = s.LastActivityAt
                })
                .ToListAsync();

            return Result<List<SessionDto>>.Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions for device {DeviceId}", deviceId);
            return Result<List<SessionDto>>.Fail("Failed to get sessions", "SESSION_LIST_ERROR");
        }
    }

    public async Task<Result<bool>> UpdateSessionActivityAsync(Guid sessionId)
    {
        try
        {
            var session = await _context.AgentSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return Result<bool>.Fail("Session not found", "SESSION_NOT_FOUND");

            session.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session activity {SessionId}", sessionId);
            return Result<bool>.Fail("Failed to update session", "SESSION_UPDATE_ERROR");
        }
    }

    public async Task<Result<bool>> UpdateSessionDataAsync(Guid sessionId, string data)
    {
        try
        {
            var session = await _context.AgentSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return Result<bool>.Fail("Session not found", "SESSION_NOT_FOUND");

            session.SessionData = data;
            session.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session data {SessionId}", sessionId);
            return Result<bool>.Fail("Failed to update session data", "SESSION_DATA_ERROR");
        }
    }

    public async Task<Result<int>> CleanupInactiveSessionsAsync(TimeSpan inactivityThreshold)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(inactivityThreshold);

            var inactiveSessions = await _context.AgentSessions
                .Where(s => s.IsActive && s.LastActivityAt < cutoffTime)
                .ToListAsync();

            foreach (var session in inactiveSessions)
            {
                session.IsActive = false;
                session.EndedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} inactive sessions", inactiveSessions.Count);

            return Result<int>.Ok(inactiveSessions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up inactive sessions");
            return Result<int>.Fail("Failed to cleanup sessions", "SESSION_CLEANUP_ERROR");
        }
    }
}
