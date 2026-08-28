using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Xiangqi;

/// <summary>
/// 长将上限:同一个将军最多重复三次,第四次不许再走。
/// <para>
/// <b>本组最要紧的不是「第四次被拒」那一条,是它旁边那两条。</b> 只钉「第四次被拒」,
/// 一个把上限写成 0 的实现也能通过;只钉「将军的重复」,一个把**所有**重复都拒掉的实现
/// 也能通过。所以三条一起:第三次接受、第四次拒绝、不将军的第四次接受。
/// </para>
/// <para>
/// <b>历史全部由逐步调用 <c>Apply</c> 累积。</b> 重放**不校验**历史里的步,所以手拼一串
/// <c>PlayedMove</c> 可以拼出一局不可能的棋,而那时断言测的是别的东西。
/// <see cref="Game.Play"/> 是让这件事不可能被绕过的地方 —— 它每一手都过规则,
/// 并且断言棋还在进行,所以写错一手会当场红。
/// </para>
/// </summary>
public class XiangqiRepeatedCheckTests
{
    private static readonly int Red = BoardSeats.FirstSeat;
    private static readonly int Black = BoardSeats.SecondSeat;

    /// <summary>
    /// 一局正在走的棋:每一手都过规则,并把它累积进历史。
    /// </summary>
    private sealed class Game
    {
        private readonly IGameRules _rules;
        private readonly string? _setup;
        private readonly List<PlayedMove> _history = [];

        internal Game(IGameRules rules, string? setup = null)
        {
            _rules = rules;
            _setup = setup;
        }

        private MatchState State => new(_setup, _history);

        /// <summary>走一步,并断言棋还在进行 —— 走错一手会在这里红,而不是在最后。</summary>
        internal Game Play(int fromRow, int fromCol, int toRow, int toCol, int seat)
        {
            var applied = Apply(fromRow, fromCol, toRow, toCol, seat);
            applied.Result.Should().Be(
                GameResult.Ongoing,
                $"({fromRow},{fromCol})→({toRow},{toCol}) 这一手本该只是普通的一步");
            _history.Add(PlayedMove.Positional(
                new Position(fromRow, fromCol), new Position(toRow, toCol), seat));
            return this;
        }

        /// <summary>走一步并返回结果,**不**落盘 —— 用来断言接受 / 拒绝 / 判胜。</summary>
        internal MoveApplication Apply(int fromRow, int fromCol, int toRow, int toCol, int seat)
            => _rules.Apply(
                State,
                MoveIntent.Slide(new Position(fromRow, fromCol), new Position(toRow, toCol)),
                seat);

        internal Action Trying(int fromRow, int fromCol, int toRow, int toCol, int seat)
            => () => Apply(fromRow, fromCol, toRow, toCol, seat);

        internal IReadOnlyList<PlayedMove> History => _history;
    }

    // ── 标准开局:炮往复将军 ──────────────────────────────────────────────────
    //
    // 摆出长将局面的八手(红方):仕 (9,3)→(8,4) 让位,帥 (9,4)→(9,3) 让出中线,
    // 中兵三步走到 (3,4) 吃掉黑中卒 —— 它是接下来那个炮的**炮架**。黑方这八手在
    // (0,0) 与 (1,0) 之间来回,不干涉中线。
    //
    // 帥 必须离开中线:留在 (9,4) 时,黑炮堵到 (2,4) 就隔着同一个兵架把红帥将了,
    // 于是红炮撤不回来 —— 循环走不下去。这一手不是装饰。
    private static Game StandardOpeningWithACannonBattery()
    {
        var game = new Game(BuiltInGameRules.Xiangqi);
        game.Play(9, 3, 8, 4, Red).Play(0, 0, 1, 0, Black);
        game.Play(9, 4, 9, 3, Red).Play(1, 0, 0, 0, Black);
        game.Play(6, 4, 5, 4, Red).Play(0, 0, 1, 0, Black);
        game.Play(5, 4, 4, 4, Red).Play(1, 0, 0, 0, Black);
        game.Play(4, 4, 3, 4, Red).Play(0, 0, 1, 0, Black);
        return game;
    }

    /// <summary>
    /// 一个将军循环:红炮 (7,1)→(7,4) 隔着 (3,4) 的兵将军;黑炮 (2,1)→(2,4) 堵上;
    /// 红炮撤回 (7,1);黑炮撤回 (2,1)。四手之后盘面**逐格回到**循环开始前。
    /// </summary>
    private static void OneCheckingCycle(Game game)
    {
        game.Play(7, 1, 7, 4, Red).Play(2, 1, 2, 4, Black);
        game.Play(7, 4, 7, 1, Red).Play(2, 4, 2, 1, Black);
    }

    [Fact]
    public void The_third_time_the_same_check_is_given_is_still_accepted()
    {
        var game = StandardOpeningWithACannonBattery();
        OneCheckingCycle(game);   // 第 1 次
        OneCheckingCycle(game);   // 第 2 次

        // 第 3 次 —— 上限是 3,所以它 MUST 被接受。
        var applied = game.Apply(7, 1, 7, 4, Red);

        applied.Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void The_fourth_time_is_refused_with_its_own_code()
    {
        var game = StandardOpeningWithACannonBattery();
        OneCheckingCycle(game);
        OneCheckingCycle(game);
        OneCheckingCycle(game);   // 第 3 次也走完了

        var fourth = game.Trying(7, 1, 7, 4, Red);

        fourth.Should().Throw<InvalidMoveException>()
            .Which.Code.Should().Be(
                "repeated-check",
                "被拒的原因不在玩家刚点的那两格上,而在十几手之前 —— "
                + "「这一步不合法」说不出「同一个将军已经三次了」");
    }

    /// <summary>
    /// **本组的反面对照,而它不是可选的。** 上面两条对「把所有重复都拒掉」的实现同样是绿的。
    /// <para>
    /// 同样的形状、同样的次数、同样由双方往复造出来 —— 只有一处不同:红炮在 (7,1) 与 (7,0)
    /// 之间来回,一次也没将军。第 4 次 MUST 被接受:这条规则限制的是**长将**,不是重复本身。
    /// </para>
    /// </summary>
    [Fact]
    public void A_fourth_repetition_that_is_not_a_check_is_accepted()
    {
        var game = new Game(BuiltInGameRules.Xiangqi);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            game.Play(7, 1, 7, 0, Red).Play(0, 0, 1, 0, Black);
            game.Play(7, 0, 7, 1, Red).Play(1, 0, 0, 0, Black);
        }

        // 第 4 次走出同一个(不将军的)局面。
        var applied = game.Apply(7, 1, 7, 0, Red);

        applied.Result.Should().Be(
            GameResult.Ongoing,
            "限制的是长将;双方各自往复一个不将军的局面,平台不管");
    }

    /// <summary>
    /// 两条对照的**前提**:上面那个循环里红方真的在将军,而这一个里真的没有。
    /// <para>
    /// 少了这一条,两条对照可能在同一个原因上通过 —— 例如「将军」的判定整个失灵时,
    /// 「第四次被拒」会红(那还好),但如果实现改成对**所有**第四次重复都拒,
    /// 两条也可能一起绿一起红而没人知道是哪个原因。这一条把「是不是将军」单独量出来:
    /// 黑方在循环中**只有堵/撤那一步能走**,而那正是「在应将」的可观察形式。
    /// </para>
    /// </summary>
    [Fact]
    public void The_checking_cycle_really_checks_and_the_quiet_one_really_does_not()
    {
        var checking = StandardOpeningWithACannonBattery();
        checking.Play(7, 1, 7, 4, Red);

        // 被将的一方:将不能挪(同一列都被那门炮罩着),只能堵。
        checking.Trying(0, 4, 1, 4, Black).Should().Throw<InvalidMoveException>(
            "(1,4) 与 (0,4) 在同一列上,同一个炮架把它一起罩着");

        var quiet = new Game(BuiltInGameRules.Xiangqi);
        quiet.Play(7, 1, 7, 0, Red);

        // 没被将的一方:随便走别的都行 —— 这里走一步与那门炮无关的马。
        quiet.Apply(0, 1, 2, 2, Black).Result.Should().Be(GameResult.Ongoing);
    }

    // ── 残局(xiangqi-endgame):同一份判定 ────────────────────────────────────

    /// <summary>摆一块盘面串 —— 测试里手写 90 个点是不可读的。</summary>
    private static string Board(params (string Piece, int Row, int Col)[] pieces)
    {
        var cells = new char[XiangqiSetup.BoardLength];
        Array.Fill(cells, '.');
        // 9 是列数。`XiangqiBoard.ColCount` 是 internal,而隔壁 XiangqiEndgameRulesTests
        // 的同名辅助函数也是这么写的 —— 两处保持一样,比这里独创一个换算好。
        foreach (var (piece, row, col) in pieces)
        {
            cells[(row * 9) + col] = piece[0];
        }
        return new string(cells);
    }

    /// <summary>
    /// 残局房里的同一条规则 —— 而这条断言是「两个棋种共用一份判定」的可执行形式。
    /// <para>
    /// 残局正是长将最常出现的地方,所以这不是为了对称:三个子的局面里红车除了将军
    /// 几乎无事可做。
    /// </para>
    /// </summary>
    [Fact]
    public void The_endgame_game_key_is_bound_by_the_same_limit()
    {
        // 黑将 (0,4) 孤身;红帥 (9,3) 不在中线(否则将帅照面会改变黑方的可走格);
        // 红俥 (2,0) 一步到 (2,4) 就是将军,黑将只能在 (0,4) 与 (0,5) 之间躲。
        var setup = new XiangqiSetup(Board(("k", 0, 4), ("K", 9, 3), ("R", 2, 0)), Red).Encode();
        var game = new Game(new XiangqiEndgameRules(), setup);

        game.Play(2, 0, 2, 4, Red).Play(0, 4, 0, 5, Black);      // 第 1 次
        game.Play(2, 4, 2, 5, Red).Play(0, 5, 0, 4, Black);
        game.Play(2, 5, 2, 4, Red).Play(0, 4, 0, 5, Black);      // 第 2 次
        game.Play(2, 4, 2, 5, Red).Play(0, 5, 0, 4, Black);
        game.Play(2, 5, 2, 4, Red).Play(0, 4, 0, 5, Black);      // 第 3 次
        game.Play(2, 4, 2, 5, Red).Play(0, 5, 0, 4, Black);

        game.Trying(2, 5, 2, 4, Red).Should().Throw<InvalidMoveException>()
            .Which.Code.Should().Be("repeated-check");
    }

    /// <summary>
    /// 将死仍然判胜。
    /// <para>
    /// **这一条刻意不写成「既达到上限又是将死」** —— 那个组合构造不出来:局面相同 ⇒ 合法着法
    /// 集合相同 ⇒ 若此刻将死,此前那次也将死,棋在那时就该结束了。在任何上限值下都成立。
    /// 一条构造不出来的断言永远不会失败,所以这里量的是能量的那一半:一步将军的杀着仍然判胜,
    /// 没有被新加的那条挡下来。
    /// </para>
    /// </summary>
    [Fact]
    public void A_mating_check_still_wins()
    {
        // 黑将 (0,3) 困在角上:红俥落到 (0,0) 沿底线将军;(0,4) 走不了(将帅照面),
        // (1,3) 走不了(红兵 (2,3) 罩着),(0,2) 出了九宫。
        var setup = new XiangqiSetup(
            Board(("k", 0, 3), ("K", 9, 4), ("R", 5, 0), ("P", 2, 3)), Red).Encode();
        var game = new Game(new XiangqiEndgameRules(), setup);

        var applied = game.Apply(5, 0, 0, 0, Red);

        applied.Result.Should().Be(GameResult.Decided);
        applied.WinnerSeat.Should().Be(Red);
    }

    // ── 与 AI 的着法枚举一致 ──────────────────────────────────────────────────

    /// <summary>
    /// 被禁的那一步不在 <c>LegalMoves</c> 里,**而其余着法还在**。
    /// <para>
    /// 两半都要:少了后一半,一个返回空表的实现也能通过前一半。而 `LegalMoves` 是 AI 的
    /// 着法来源 —— 它与 `Apply` 不一致时,表现是**机器人走出规则会拒绝的棋**,
    /// 用户看到的是它卡住了。
    /// </para>
    /// </summary>
    [Fact]
    public void The_forbidden_move_is_absent_from_the_enumeration_but_the_others_remain()
    {
        var game = StandardOpeningWithACannonBattery();
        OneCheckingCycle(game);
        OneCheckingCycle(game);
        OneCheckingCycle(game);

        var rules = (XiangqiRules)BuiltInGameRules.Xiangqi;
        var moves = rules.LegalMoves(game.History, Stone.Black);   // Stone.Black 是红方

        var forbidden = MoveIntent.Slide(new Position(7, 1), new Position(7, 4));
        moves.Should().NotContain(forbidden, "Apply 会拒绝它,所以枚举里不能有");
        moves.Should().NotBeEmpty("红方还有一堆别的棋可走 —— 空表也能通过上一条");

        // 而每一条留下来的都真的能被 Apply 接受 —— 这是那条一致性要求的直接形式。
        foreach (var move in moves)
        {
            game.Trying(
                move.From!.Value.Row, move.From!.Value.Col,
                move.To!.Value.Row, move.To!.Value.Col,
                Red).Should().NotThrow($"LegalMoves 给出了 {move.From} → {move.To}");
        }
    }
}
