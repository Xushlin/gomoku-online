using Gewu.Domain.Enums;

namespace Gewu.Domain.Games.Abstractions;

/// <summary>
/// 座位号与 <see cref="Stone"/> 之间的换算 —— **只给棋盘类棋种用**。
/// <para>
/// 内核已经不知道"黑白"是什么了(见 <c>Game.CurrentTurn</c>)。而棋盘上那颗东西确实叫子,
/// 所以 <see cref="Stone"/> 没有被废弃,它下沉成了棋盘家族内部的词汇。这个类是那道边界:
/// 上面进来的是座位号,下面用的是棋色。
/// </para>
/// <para>
/// <c>add-xiangqi</c> 立下的「<see cref="Stone.Black"/> 就是红」那条读法**一个字不动** ——
/// 它说的正是"先手座位在象棋里画成红色",而先手座位就是 <c>0</c>。这次改动把那句注释
/// 从一个约定变成了一处结构。
/// </para>
/// </summary>
public static class BoardSeats
{
    // 这三个刻意**不是** const。
    //
    // C# 里常量表达式 `0` 可以隐式转换成任意枚举 —— 所以 `const int FirstSeat = 0` 落在一个
    // `Stone` 参数上会静默编译成 `Stone.Empty`。这次重构第一版就是 const,而它把两处
    // `Stone.Black` 悄悄改成了 `Stone.Empty`:一处棋盘断言、一处 AI 的 SelectMove 调用,
    // 编译器一声不吭,是运行时的测试失败把它揪出来的(`SecondSeat = 1` 没事 —— 只有 0 有这个特权)。
    //
    // `static readonly` 不是常量表达式,那条隐式转换就不适用,于是同样的错误变成编译错误。

    /// <summary>先手座位。</summary>
    public static readonly int FirstSeat = 0;

    /// <summary>后手座位。</summary>
    public static readonly int SecondSeat = 1;

    /// <summary>棋盘类棋种的座位数。</summary>
    public static readonly int SeatCount = 2;

    /// <summary>座位号 → 棋色。</summary>
    /// <param name="seat">座位号。</param>
    /// <exception cref="System.ArgumentOutOfRangeException">不是 0 或 1。</exception>
    public static Stone ToStone(int seat)
    {
        // if/else 而不是 switch 表达式:switch 的模式要求常量,而这些座位号刻意不是常量
        // (见上面那段注释 —— const 0 会隐式变成 Stone.Empty)。
        if (seat == FirstSeat) return Stone.Black;
        if (seat == SecondSeat) return Stone.White;
        throw new System.ArgumentOutOfRangeException(
            nameof(seat), seat, "A board game has exactly two seats.");
    }

    /// <summary>棋色 → 座位号。</summary>
    /// <param name="stone">棋色,不能是 <see cref="Stone.Empty"/>。</param>
    /// <exception cref="System.ArgumentOutOfRangeException">是 <see cref="Stone.Empty"/>。</exception>
    public static int ToSeat(Stone stone) => stone switch
    {
        Stone.Black => FirstSeat,
        Stone.White => SecondSeat,
        _ => throw new System.ArgumentOutOfRangeException(
            nameof(stone), stone, "Stone.Empty is not a seat."),
    };
}
