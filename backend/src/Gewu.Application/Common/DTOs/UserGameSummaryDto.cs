using Gewu.Domain.Enums;

namespace Gewu.Application.Common.DTOs;

/// <summary>
/// 用户战绩列表卡片。比 <see cref="GameReplayDto"/> 精简:不含 Host(和 0 号座位同一个人)、
/// 不含 Moves 数组(列表视图太重);点进去再拉 <c>/api/rooms/{id}/replay</c>。
/// <para>
/// <b>玩家由 <c>Seats</c> 一处说明</b> —— 此前是 <c>Black</c> / <c>White</c>,与
/// <see cref="GameReplayDto"/> 同一个缺陷、同一次实测:那两个是 0 号与 1 号座位的派生读法,
/// 于是三座位棋种的战绩里 2 号座位上的人不出现。仓储不按棋种过滤,所以三座位对局照样进这个列表。
/// </para>
/// <para>
/// <b>而「谁赢了」这一格,这个 DTO 说不出,消费方 MUST NOT 假装它说得出。</b>
/// <see cref="WinnerUserId"/> 只装得下一个座位,而斗地主两名农民是一起赢的 ——
/// 领域层写明了这个取舍,并把出路留给客户端(「从叫分历史里知道谁是地主」),
/// 而**那条出路在这里不成立**:本 DTO 刻意不含 <c>Moves</c>。所以三座位对局里
/// 「赢家不是我」MUST NOT 被读成「我输了」—— 没走出去的那个农民是赢家之一。
/// 判据与文案见 <c>web-user-profile</c> 的四支。
/// </para>
/// </summary>
public sealed record UserGameSummaryDto(
    Guid RoomId,
    string Name,
    IReadOnlyList<RoomSeatDto> Seats,
    DateTime StartedAt,
    DateTime EndedAt,
    GameResult Result,
    Guid? WinnerUserId,
    GameEndReason EndReason,
    int MoveCount);
