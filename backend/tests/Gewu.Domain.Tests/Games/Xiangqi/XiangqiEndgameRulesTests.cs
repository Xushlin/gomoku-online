using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Xiangqi;

/// <summary>
/// 从一则古谱残局开局。
/// <para>
/// 重点是三件**会静默出错**的事:从错的局面开局(画出来完全正常)、先手假设成红(那 7 局
/// 一开局就轮错人),以及坏设置退回标准开局(和一局正常的棋长得一样)。
/// </para>
/// </summary>
public class XiangqiEndgameRulesTests
{
    private static readonly XiangqiEndgameRules Rules = new();
    private static readonly IBoardGameRules Standard = (IBoardGameRules)BuiltInGameRules.Xiangqi;

    private static readonly int Red = BoardSeats.FirstSeat;
    private static readonly int Black = BoardSeats.SecondSeat;

    /// <summary>标准开局的盘面串 —— 与古谱产物里那个常量同源。</summary>
    private const string StandardBoard =
        "rnbakabnr..........c.....c.p.p.p.p.p..................P.P.P.P.P.C.....C..........RNBAKABNR";

    /// <summary>摆一块盘面串。测试里手写 90 个点是不可读的。</summary>
    private static string Board(params (string Piece, int Row, int Col)[] pieces)
    {
        var cells = new char[XiangqiSetup.BoardLength];
        Array.Fill(cells, '.');
        foreach (var (piece, row, col) in pieces) cells[(row * 9) + col] = piece[0];
        return new string(cells);
    }

    /// <summary>红帅 (9,4)、红车 (9,0);黑将 (0,4)、黑卒 (3,4) —— **4 个子**。</summary>
    private static string Endgame() =>
        Board(("k", 0, 4), ("p", 3, 4), ("K", 9, 4), ("R", 9, 0));

    private static MatchState State(string board, int firstSeat, params PlayedMove[] history)
        => new(new XiangqiSetup(board, firstSeat).Encode(), history);

    private static MoveApplication Apply(MatchState state, int fr, int fc, int tr, int tc, int seat)
        => Rules.Apply(state, MoveIntent.Slide(new Position(fr, fc), new Position(tr, tc)), seat);

    // ---- 身份 ----

    [Fact]
    public void Is_a_two_seat_board_game_that_is_never_rated()
    {
        Rules.GameKey.Should().Be(GameKeys.XiangqiEndgame);
        Rules.Rows.Should().Be(10);
        Rules.Cols.Should().Be(9);
        Rules.SeatCount.Should().Be(2);
        Rules.SupportsHumanVsHuman.Should().BeTrue("这正是它存在的理由");
        Rules.IsRated.Should().BeFalse("残局开局就不公平 —— 有一方按构造是赢定的");
    }

    [Fact]
    public void Declares_both_new_seams()
    {
        Rules.Should().BeAssignableTo<IPositionalStartRules>();
        Rules.Should().BeAssignableTo<IFirstSeatRules>();
        Rules.Should().NotBeAssignableTo<IDealtGameRules>(
            "一份设置只能有一个来源;两个来源会让「谁负责它的内容」没有答案");
    }

    // ---- 从设置开局 ----

    /// <summary>
    /// **这是本组的核心判据。**
    /// <para>
    /// 红车 (9,0) → (4,0) 在这个 4 子残局里畅通;而在标准开局下 (7,0) 有兵、(6,0) 有兵,
    /// 车走不过去。所以这一步**能不能走**,直接回答了「它是从哪块盘面判的」——
    /// 若实现仍从标准开局重放,这一步会被拒。
    /// </para>
    /// </summary>
    [Fact]
    public void Judges_from_the_setup_position_not_from_the_standard_opening()
    {
        var state = State(Endgame(), Red);

        var applied = Apply(state, 9, 0, 4, 0, Red);

        applied.Result.Should().Be(GameResult.Ongoing);
    }

    /// <summary>而反面对照:同一步棋在标准开局下**确实**被拒 —— 否则上面那条恒真。</summary>
    [Fact]
    public void The_same_move_is_rejected_from_the_standard_opening()
    {
        var act = () => Standard.Apply(
            new MatchState(null, []),
            MoveIntent.Slide(new Position(9, 0), new Position(4, 0)),
            Red);

        act.Should().Throw<InvalidMoveException>();
    }

    /// <summary>反过来:一步在标准开局下合法、在这个残局下没有子可动的走法,MUST 被拒。</summary>
    [Fact]
    public void A_move_whose_origin_is_empty_in_the_setup_is_rejected()
    {
        // (9,6) 在标准开局上是红相,而这个残局里那格是空的。
        var act = () => Apply(State(Endgame(), Red), 9, 6, 7, 4, Red);

        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*no piece*");
    }

    // ---- 先走方 ----

    [Fact]
    public void The_first_seat_comes_from_the_setup()
    {
        Rules.FirstSeat(State(Endgame(), Red)).Should().Be(Red);
        Rules.FirstSeat(State(Endgame(), Black)).Should().Be(
            Black, "1634 局残局里 7 局是黑先走 —— 先手是数据,不是「红先」这条约定");
    }

    /// <summary>黑先走的残局里,黑真的能动 —— 而它动的是黑子。</summary>
    [Fact]
    public void Black_can_move_first_in_a_black_first_position()
    {
        var applied = Apply(State(Endgame(), Black), 3, 4, 4, 4, Black);

        applied.Result.Should().Be(GameResult.Ongoing);
    }

    // ---- 设置的校验 ----

    [Fact]
    public void Accepts_a_well_formed_setup()
    {
        var act = () => Rules.ValidateSetup(new XiangqiSetup(Endgame(), Black).Encode());

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("board", "not-a-setup")]
    [InlineData("length", "rnbakabnr:0")]
    [InlineData("seat", "SEAT_PLACEHOLDER")]
    public void Rejects_a_malformed_setup_and_says_which_rule_failed(string what, string setup)
    {
        var actual = what == "seat" ? new XiangqiSetup(Endgame(), 7).Encode() : setup;

        var act = () => Rules.ValidateSetup(actual);

        act.Should().Throw<InvalidGameSetupException>().Which.Message.Should().NotBeEmpty();
    }

    [Fact]
    public void Rejects_a_position_without_exactly_one_general_per_side()
    {
        // 黑将缺席 —— 「将死」在这样的局面上判不出来。
        var noBlackKing = Board(("p", 3, 4), ("K", 9, 4), ("R", 9, 0));

        var act = () => Rules.ValidateSetup(new XiangqiSetup(noBlackKing, Red).Encode());

        act.Should().Throw<InvalidGameSetupException>()
            .Which.Message.Should().Contain("exactly one general");
    }

    [Fact]
    public void Rejects_a_general_outside_its_palace()
    {
        var kingOnTheEdge = Board(("k", 0, 4), ("K", 9, 0), ("R", 9, 8));

        var act = () => Rules.ValidateSetup(new XiangqiSetup(kingOnTheEdge, Red).Encode());

        act.Should().Throw<InvalidGameSetupException>()
            .Which.Message.Should().Contain("palace");
    }

    /// <summary>
    /// **坏设置 MUST NOT 退回标准开局。**
    /// <para>
    /// 那种坏的表现是「这局怎么是开局」,而它和一局正常的棋在界面上完全一样 —— 没有任何
    /// 断言会红,除非有人正好记得自己选的是哪一则残局。
    /// </para>
    /// </summary>
    [Fact]
    public void A_bad_setup_never_silently_becomes_the_standard_opening()
    {
        var truncated = new XiangqiSetup(Endgame()[..89], Red).Encode();

        var act = () => Apply(new MatchState(truncated, []), 9, 6, 7, 4, Red);

        act.Should().Throw<InvalidGameSetupException>();
    }

    // ---- 与标准象棋共用同一份走子逻辑 ----

    /// <summary>
    /// 同一个局面 + 同一步棋,两个棋种 MUST 判得一样。
    /// <para>
    /// 样本用**标准开局**——那是两条路径都能到达的唯一局面,所以这条断言不会在单一路径上
    /// 恒真:左边从设置里读出这块盘,右边用它的常量摆出这块盘。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(9, 6, 7, 4, true)]   // 相三进五 —— 合法
    [InlineData(9, 6, 7, 5, false)]  // 相走成日字 —— 非法
    [InlineData(6, 0, 5, 0, true)]   // 兵进一 —— 合法
    [InlineData(6, 0, 6, 1, false)]  // 兵未过河横走 —— 非法
    public void Agrees_with_standard_xiangqi_on_the_shared_position(
        int fr, int fc, int tr, int tc, bool legal)
    {
        var intent = MoveIntent.Slide(new Position(fr, fc), new Position(tr, tc));

        var fromEndgame = () => Rules.Apply(State(StandardBoard, Red), intent, Red);
        var fromStandard = () => Standard.Apply(new MatchState(null, []), intent, Red);

        if (legal)
        {
            fromEndgame.Should().NotThrow();
            fromStandard.Should().NotThrow();
        }
        else
        {
            fromEndgame.Should().Throw<InvalidMoveException>();
            fromStandard.Should().Throw<InvalidMoveException>();
        }
    }

    // ---- 没有 AI ----

    /// <summary>
    /// `LegalMoves` 只收历史,收不到设置 —— 所以它 MUST **说清楚做不到**,而不是返回一份
    /// 按标准开局算出来的、看起来像真的答案。
    /// <para>
    /// 一份错的合法着法表,表现是「机器人走出规则会拒绝的棋」,而用户看到的是「机器人卡住了」。
    /// </para>
    /// </summary>
    [Fact]
    public void Refuses_to_enumerate_legal_moves_from_a_history_alone()
    {
        var act = () => Rules.LegalMoves([], Stone.Black);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("setup");
    }
}
