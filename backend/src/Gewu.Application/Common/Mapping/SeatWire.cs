using Gewu.Domain.Enums;

namespace Gewu.Application.Common.Mapping;

/// <summary>
/// 座位号 ↔ 线上 <see cref="Stone"/> 的桥,**只用在 DTO 边界上**。
/// <para>
/// 内核自 <c>generalize-match-seats</c> 起说座位号,而线上格式仍是 <c>'Black' | 'White'</c> ——
/// 于是前端一行不用改,那次改动是纯后端内部重构。
/// </para>
/// <para>
/// **这是带触发条件的债,不是疏漏。** 触发条件:第一个 <c>SeatCount != 2</c> 的棋种落地那天,
/// DTO 加座位字段,本类删除。写下这条的理由是 —— 一层没有理由的边界映射,下一个读到它的人
/// 会当成手滑,然后要么绕开它、要么以为它是真源。
/// </para>
/// <para>
/// 它与 <c>BoardSeats</c> 刻意分开:那一个是**棋盘家族内部**的词汇换算,永久存在;这一个是
/// **线上格式**的临时兼容,会被删掉。两者数值相同而寿命不同,合成一个就没法只删一半。
/// </para>
/// </summary>
internal static class SeatWire
{
    /// <summary>座位号 → 线上棋色。</summary>
    /// <param name="seat">座位号。</param>
    public static Stone ToStone(int seat) => seat == 0 ? Stone.Black : Stone.White;

    /// <summary>线上棋色 → 座位号。</summary>
    /// <param name="stone">棋色。</param>
    public static int ToSeat(Stone stone) => stone == Stone.Black ? 0 : 1;
}
