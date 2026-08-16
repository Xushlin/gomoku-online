using Gewu.Domain.Enums;

namespace Gewu.Domain.Games.Xiangqi;

/// <summary>中国象棋的七种棋子。红黑同型不同名(帅/将、仕/士、相/象、俥/车…),这里只分型。</summary>
public enum XiangqiPieceType
{
    /// <summary>将 / 帅。九宫内上下左右一步。</summary>
    General = 1,

    /// <summary>士 / 仕。九宫内斜走一步。</summary>
    Advisor = 2,

    /// <summary>象 / 相。田字,不过河,塞象眼不可走。</summary>
    Elephant = 3,

    /// <summary>马。日字,蹩马腿不可走。</summary>
    Horse = 4,

    /// <summary>车。直线任意步,不越子。</summary>
    Chariot = 5,

    /// <summary>炮。走同车;吃子时中间必须恰有一个子(炮架)。</summary>
    Cannon = 6,

    /// <summary>兵 / 卒。向前一步,过河后可横走,永不后退。</summary>
    Soldier = 7,
}

/// <summary>
/// 盘面上的一枚棋子:类型 + 属于哪一方。
/// <para>
/// <see cref="Side"/> 用 <see cref="Stone"/>,且**本棋种中 <see cref="Stone.Black"/> 是红方** ——
/// 见 <see cref="XiangqiRules"/> 的说明。
/// </para>
/// </summary>
/// <param name="Type">棋子类型。</param>
/// <param name="Side">所属方。</param>
public readonly record struct XiangqiPiece(XiangqiPieceType Type, Stone Side);
