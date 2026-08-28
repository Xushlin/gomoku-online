using Gewu.Domain.Enums;

namespace Gewu.Application.Common.DTOs;

/// <summary>
/// 一局 Finished 对局的完整回放 payload。`Moves` MUST 按 <c>Ply</c> 升序;认输 / 超时
/// 结束时可能 `Moves == []`(空数组,非 null)。
/// <para>
/// <b>玩家由 <c>Seats</c> 一处说明,而这里此前是 <c>Black</c> / <c>White</c> 两个字段。</b>
/// 那两个是 0 号与 1 号座位的派生读法,于是一局已结束的斗地主经此端点出来时,2 号座位上的人
/// **在任何字段里都不出现** —— 实测过:三个人的一局,响应里只有两个,而端点 200 成功返回。
/// </para>
/// <para>
/// 比「少一个人」更硬的判据是**这份载荷自相矛盾**:<c>Moves[].Seat</c> 里有 0 / 1 / 2 三个座位号,
/// 而玩家字段只解析得出两个 —— 59 手里有 20 手的出手人是这份载荷自己说不出是谁的。所以
/// <c>Seats</c> 与 <c>Moves[].Seat</c> MUST 是同一套座位号:每个 <c>Move.Seat</c> 在 <c>Seats</c>
/// 里恰好匹配一条 <c>Index</c>。
/// </para>
/// <para>
/// 删掉而不是像 <see cref="RoomStateDto"/> 那样两者并留,理由是**留下来的那份会继续说谎**:
/// 那里的 <c>Black</c> / <c>White</c> 有真读者,这里改完之后零读者。一个没人读、又对三分之一
/// 棋种为假的字段,是下一个人照抄的模板。
/// </para>
/// </summary>
public sealed record GameReplayDto(
    Guid RoomId,
    string Name,
    string GameKey,
    UserSummaryDto Host,
    IReadOnlyList<RoomSeatDto> Seats,
    DateTime StartedAt,
    DateTime EndedAt,
    GameResult Result,
    Guid? WinnerUserId,
    GameEndReason EndReason,
    IReadOnlyList<MoveDto> Moves);
