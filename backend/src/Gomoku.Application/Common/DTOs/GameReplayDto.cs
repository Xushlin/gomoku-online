using Gomoku.Domain.Enums;

namespace Gomoku.Application.Common.DTOs;

/// <summary>
/// 一局 Finished 对局的完整回放 payload。`Moves` MUST 按 <c>Ply</c> 升序;认输 / 超时
/// 结束时可能 `Moves == []`(空数组,非 null)。
/// </summary>
public sealed record GameReplayDto(
    Guid RoomId,
    string Name,
    UserSummaryDto Host,
    UserSummaryDto Black,
    UserSummaryDto White,
    DateTime StartedAt,
    DateTime EndedAt,
    GameResult Result,
    Guid? WinnerUserId,
    GameEndReason EndReason,
    IReadOnlyList<MoveDto> Moves);
