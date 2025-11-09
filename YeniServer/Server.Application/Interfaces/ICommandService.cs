using Server.Application.Common;
using Server.Application.DTOs.Command;

namespace Server.Application.Interfaces;

public interface ICommandService
{
    Task<Result<CommandDto>> GetCommandByIdAsync(Guid id);
    Task<Result<List<CommandDto>>> GetCommandsByDeviceAsync(Guid deviceId);
    Task<Result<CommandDto>> ExecuteCommandAsync(ExecuteCommandRequest request, Guid userId);
    Task<Result> UpdateCommandResultAsync(CommandResultRequest request);
    Task<Result> MarkCommandAsSentAsync(Guid commandId);
}
