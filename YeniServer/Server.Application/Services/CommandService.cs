using Microsoft.EntityFrameworkCore;
using Server.Application.Common;
using Server.Application.DTOs.Command;
using Server.Application.Interfaces;
using Server.Infrastructure.Data;
using Server.Domain.Constants;

namespace Server.Application.Services;

public class CommandService : ICommandService
{
    private readonly ApplicationDbContext _context;

    public CommandService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CommandDto>> GetCommandByIdAsync(Guid id)
    {
        var command = await _context.Commands.FindAsync(id);
        if (command == null)
            return Result<CommandDto>.Fail("Command not found", "COMMAND_NOT_FOUND");

        return Result<CommandDto>.Ok(MapToDto(command));
    }

    public async Task<Result<List<CommandDto>>> GetCommandsByDeviceAsync(Guid deviceId)
    {
        var commands = await _context.Commands
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(100)
            .ToListAsync();

        var dtos = commands.Select(MapToDto).ToList();
        return Result<List<CommandDto>>.Ok(dtos);
    }

    public async Task<Result<CommandDto>> ExecuteCommandAsync(ExecuteCommandRequest request, Guid? userId)
    {
        // Validate command type
        if (!AgentCommands.IsValidCommand(request.CommandType))
        {
            return Result<CommandDto>.Fail($"Invalid command type: {request.CommandType}", "INVALID_COMMAND");
        }

        // Verify device exists
        var device = await _context.Devices.FindAsync(request.DeviceId);
        if (device == null)
            return Result<CommandDto>.Fail("Device not found", "DEVICE_NOT_FOUND");

        // Get command category
        var category = AgentCommands.GetCategory(request.CommandType);

        var command = new Domain.Entities.Command
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            UserId = userId, // nullable
            CommandType = request.CommandType,
            Category = category,
            Parameters = request.Parameters,
            SessionId = request.SessionId,
            Status = "Pending",
            Priority = 0, // Normal priority by default
            CreatedAt = DateTime.UtcNow
        };

        _context.Commands.Add(command);
        await _context.SaveChangesAsync();

        return Result<CommandDto>.Ok(MapToDto(command));
    }

    public async Task<Result> UpdateCommandResultAsync(CommandResultRequest request)
    {
        var command = await _context.Commands.FindAsync(request.CommandId);
        if (command == null)
            return Result.Fail("Command not found", "COMMAND_NOT_FOUND");

        command.Status = request.Status;
        command.Result = request.Result;
        command.CompletedAt = DateTime.UtcNow;

        // Calculate execution duration if command was sent
        if (command.SentAt.HasValue && command.CompletedAt.HasValue)
        {
            command.ExecutionDurationMs = (long)(command.CompletedAt.Value - command.SentAt.Value).TotalMilliseconds;
        }

        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> MarkCommandAsSentAsync(Guid commandId)
    {
        var command = await _context.Commands.FindAsync(commandId);
        if (command == null)
            return Result.Fail("Command not found", "COMMAND_NOT_FOUND");

        command.SentAt = DateTime.UtcNow;
        command.Status = "Sent";
        await _context.SaveChangesAsync();
        
        return Result.Ok();
    }

    private CommandDto MapToDto(Domain.Entities.Command command)
    {
        return new CommandDto
        {
            Id = command.Id,
            DeviceId = command.DeviceId,
            UserId = command.UserId,
            CommandType = command.CommandType,
            Parameters = command.Parameters,
            SessionId = command.SessionId,
            Status = command.Status,
            Result = command.Result,
            CreatedAt = command.CreatedAt,
            CompletedAt = command.CompletedAt
        };
    }
}
