using ResultBoxes;
using Sekiban.Dcb.Commands;

namespace Sekiban.Pure.Command.Executor;

[GenerateSerializer]
public record CommandResponseSimple(
    string? LastSortableUniqueId,
    Guid EventId,
    bool Success = true,
    string? Message = null)
{
    public static CommandResponseSimple FromExecutionResult(ExecutionResult result) =>
        new(result.SortableUniqueId, result.EventId, true, null);
}

public static class CommandResponseSimpleExtensions
{
    public static ResultBox<CommandResponseSimple> ToSimpleCommandResponse(this ResultBox<ExecutionResult> result) =>
        result.Remap(CommandResponseSimple.FromExecutionResult);

    public static async Task<ResultBox<CommandResponseSimple>> ToSimpleCommandResponse(this Task<ResultBox<ExecutionResult>> resultTask) =>
        (await resultTask).ToSimpleCommandResponse();
}
