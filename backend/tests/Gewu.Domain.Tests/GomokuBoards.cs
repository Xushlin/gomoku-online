using Gewu.Domain.Entities;
using Gewu.Domain.Games.NInARow;

namespace Gewu.Domain.Tests;

/// <summary>
/// 五子棋盘面的测试构造入口。
/// <para>
/// 棋盘尺寸从编译期常量变成了构造参数,但既有的判胜 / AI 测试断言的仍然是五子棋的行为,
/// 所以它们通过这里拿到**真正注册给五子棋的那套规则**造盘 —— 不是在测试里再写一遍
/// `new Board(15, 15, 5)`,那样规则改了测试却不会跟着变。
/// </para>
/// </summary>
internal static class GomokuBoards
{
    /// <summary>五子棋的边长。</summary>
    internal const int Size = 15;

    /// <summary>合法坐标上界(含)。</summary>
    internal const int MaxIndex = Size - 1;

    /// <summary>造一块空的五子棋盘。</summary>
    internal static Board New() => BuiltInGameRules.Gomoku.CreateBoard();
}
