using ResultBoxes;
using Sekiban.Pure.Command;
using System.Reflection;

namespace EsCQRSQuestions.Domain.Extensions;

/// <summary>
/// コマンド関連の拡張メソッドを提供します
/// </summary>
public static class CommandExtensions
{    
    /// <summary>
    /// CommandResponseをLastSortableUniqueIdを含む簡易形式に変換します
    /// </summary>
    public static ResultBox<CommandResponseSimple> ToSimpleCommandResponse<T>(this ResultBox<T> response) 
        where T : class
    {
        return response.Conveyor(commandResponse => 
        {
            try
            {
                // CommandResponseの型に合わせてプロパティにアクセス
                var type = commandResponse.GetType();
                
                PropertyInfo? partitionKeysProperty = type.GetProperty("PartitionKeys");
                object? partitionKeys = partitionKeysProperty?.GetValue(commandResponse);
                
                if (partitionKeys == null)
                {
                    return ResultBox.FromValue(new CommandResponseSimple(Guid.Empty, ""));
                }
                
                PropertyInfo? aggregateIdProperty = partitionKeys.GetType().GetProperty("AggregateId");
                object? aggregateId = aggregateIdProperty?.GetValue(partitionKeys);
                
                PropertyInfo? lastSortableUniqueIdProperty = type.GetProperty("LastSortableUniqueId");
                object? lastSortableUniqueId = lastSortableUniqueIdProperty?.GetValue(commandResponse);
                
                if (aggregateId is Guid id)
                {
                    string uniqueId = lastSortableUniqueId?.ToString() ?? "";
                    return ResultBox.FromValue(new CommandResponseSimple(id, uniqueId));
                }
                
                return ResultBox.FromValue(new CommandResponseSimple(Guid.Empty, ""));
            }
            catch (Exception ex)
            {
                // デバッグ情報
                Console.WriteLine($"ERROR in ToSimpleCommandResponse: {ex.Message}");
                return ResultBox.FromValue(new CommandResponseSimple(Guid.Empty, ""));
            }
        });
    }
    
    /// <summary>
    /// Guid値を含むResultBoxをCommandResponseSimpleに変換するオーバーロード
    /// </summary>
    public static ResultBox<CommandResponseSimple> ToSimpleCommandResponse(this ResultBox<Guid> response)
    {
        return response.Conveyor(guid => 
            ResultBox.FromValue(new CommandResponseSimple(guid, ""))
        );
    }

    /// <summary>
    /// 直接CommandResponseSimpleを作成する簡易メソッド
    /// </summary>
    public static CommandResponseSimple CreateSimple(Guid aggregateId, string lastSortableUniqueId)
    {
        return new CommandResponseSimple(aggregateId, lastSortableUniqueId);
    }
}

/// <summary>
/// CommandResponseのシンプルな表現
/// </summary>
[GenerateSerializer]
public record CommandResponseSimple(
    Guid AggregateId,
    string LastSortableUniqueId
);
