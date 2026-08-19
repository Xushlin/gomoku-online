using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Xiangqi;

/// <summary>
/// 中国象棋走法。
/// <para>
/// **本棋种中 <c>Stone.Black</c> 是红方**（红先，对齐 <c>Game.CurrentTurn</c> 的初值），
/// 红方在下（第 5–9 行）。每个用例里的 <c>Red</c> / <c>BlackSide</c> 就是这个意思。
/// </para>
/// <para>
/// 棋盘坐标是 <c>(row, col)</c>，row 0 在上（黑方底线），row 9 在下（红方底线）。
/// </para>
/// </summary>
public class XiangqiRulesTests
{
    private static readonly IBoardGameRules Rules = (IBoardGameRules)BuiltInGameRules.Xiangqi;

    /// <summary>红方 —— 先手。</summary>
    private static readonly int Red = BoardSeats.FirstSeat;

    /// <summary>黑方 —— 后手。</summary>
    private static readonly int BlackSide = BoardSeats.SecondSeat;

    private static Position P(int row, int col) => new(row, col);

    private static PlayedMove Slide(int fromRow, int fromCol, int toRow, int toCol, int side)
        => PlayedMove.Positional(P(fromRow, fromCol), P(toRow, toCol), side);

    private static MoveApplication Apply(
        IReadOnlyList<PlayedMove> history, int fr, int fc, int tr, int tc, int side)
        => Rules.Apply(new MatchState(null, history), MoveIntent.Slide(P(fr, fc), P(tr, tc)), side);

    private static Action Applying(
        IReadOnlyList<PlayedMove> history, int fr, int fc, int tr, int tc, int side)
        => () => Apply(history, fr, fc, tr, tc, side);

    private static readonly IReadOnlyList<PlayedMove> Start = [];

    // ---- 身份与形状 ----

    [Fact]
    public void It_is_registered_as_a_ten_by_nine_game()
    {
        Rules.GameKey.Should().Be("xiangqi");
        Rules.Rows.Should().Be(10);
        Rules.Cols.Should().Be(9);
    }

    [Fact]
    public void It_is_not_an_n_in_a_row_game()
    {
        // 象棋没有「连几子」,也不用 Board。它实现了窄接口就说明 generalize-match-domain
        // 那次的拆分白做了。
        Rules.Should().NotBeAssignableTo<INInARowRules>();
    }

    [Fact]
    public void A_move_without_an_origin_is_rejected()
    {
        var act = () => Rules.Apply(new MatchState(null, Start), MoveIntent.Place(P(6, 0)), Red);

        act.Should().Throw<InvalidMoveException>().WithMessage("*origin*");
    }

    [Fact]
    public void Moving_an_empty_square_is_rejected()
    {
        Applying(Start, 5, 0, 4, 0, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Moving_the_opponents_piece_is_rejected()
    {
        // (3,0) 是黑方的卒。
        Applying(Start, 3, 0, 4, 0, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Capturing_your_own_piece_is_rejected()
    {
        // 红车 (9,0) 走到自家马 (9,1)。
        Applying(Start, 9, 0, 9, 1, Red).Should().Throw<InvalidMoveException>();
    }

    // ---- 兵 / 卒 ----

    [Fact]
    public void A_red_soldier_steps_forward_up_the_board()
    {
        Apply(Start, 6, 0, 5, 0, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_soldier_never_steps_backward()
    {
        Applying(Start, 6, 0, 7, 0, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_soldier_cannot_step_sideways_before_crossing_the_river()
    {
        Applying(Start, 6, 0, 6, 1, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_soldier_may_step_sideways_once_across_the_river()
    {
        // 红兵 (6,0) → (5,0) → (4,0):第 4 行已过河。再横走一步。
        var history = new List<PlayedMove>
        {
            Slide(6, 0, 5, 0, Red), Slide(3, 8, 4, 8, BlackSide),
            Slide(5, 0, 4, 0, Red), Slide(4, 8, 5, 8, BlackSide),
        };

        Apply(history, 4, 0, 4, 1, Red).Result.Should().Be(GameResult.Ongoing);
    }

    // ---- 马 ----

    [Fact]
    public void A_horse_moves_in_an_L()
    {
        // 红马 (9,1) → (7,2):蹩腿格是 (8,1),开局是空的。
        Apply(Start, 9, 1, 7, 2, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_horse_is_blocked_by_its_leg()
    {
        // 先把红炮从 (7,1) 挪到 (8,1),正好蹩住 (9,1) 的马往 (7,2) 的腿。
        var history = new List<PlayedMove>
        {
            Slide(7, 1, 8, 1, Red), Slide(3, 8, 4, 8, BlackSide),
        };

        Applying(history, 9, 1, 7, 2, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_horse_rejects_a_non_L_move()
    {
        Applying(Start, 9, 1, 8, 1, Red).Should().Throw<InvalidMoveException>();
    }

    // ---- 象 / 相 ----

    [Fact]
    public void An_elephant_moves_two_diagonally()
    {
        // 红相 (9,2) → (7,4)。象眼 (8,3) 开局是空的。
        Apply(Start, 9, 2, 7, 4, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void An_elephant_is_blocked_by_the_eye()
    {
        // 把红炮挪到 (8,3) 塞住象眼。
        var history = new List<PlayedMove>
        {
            Slide(7, 1, 8, 1, Red), Slide(3, 8, 4, 8, BlackSide),
            Slide(8, 1, 8, 3, Red), Slide(4, 8, 5, 8, BlackSide),
        };

        Applying(history, 9, 2, 7, 4, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void An_elephant_never_crosses_the_river()
    {
        // 红相 (9,2) → (7,0) → 再往 (5,2) 还在己方;试图走到第 3 行(过河)必须被拒。
        var history = new List<PlayedMove>
        {
            Slide(9, 2, 7, 0, Red), Slide(3, 8, 4, 8, BlackSide),
            Slide(7, 0, 5, 2, Red), Slide(4, 8, 5, 8, BlackSide),
        };

        Applying(history, 5, 2, 3, 0, Red).Should().Throw<InvalidMoveException>();
    }

    // ---- 车 ----

    [Fact]
    public void A_chariot_slides_along_an_empty_file()
    {
        // 红兵 (6,0) 先让开,车 (9,0) 才能沿 0 列上行。
        var history = new List<PlayedMove>
        {
            Slide(6, 0, 5, 0, Red), Slide(3, 8, 4, 8, BlackSide),
        };

        Apply(history, 9, 0, 6, 0, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_chariot_cannot_jump()
    {
        // 开局 (9,0) 的车被自家兵 (6,0) 挡着,走不到 (4,0)。
        Applying(Start, 9, 0, 4, 0, Red).Should().Throw<InvalidMoveException>();
    }

    // ---- 炮 ----

    [Fact]
    public void A_cannon_slides_like_a_chariot_when_not_capturing()
    {
        // 红炮 (7,1) 沿第 7 行走到 (7,4) —— 中间空。
        Apply(Start, 7, 1, 7, 4, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_cannon_needs_exactly_one_screen_to_capture()
    {
        // 红炮 (7,1) 沿 1 列打黑马 (0,1):中间有黑卒 (3,1)? 开局 (3,1) 是空的,
        // 卒在 (3,0)/(3,2)/…。1 列上从 (7,1) 到 (0,1) 之间只有黑炮 (2,1) 一个子 —— 正好一个炮架。
        Apply(Start, 7, 1, 0, 1, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_cannon_cannot_capture_without_a_screen()
    {
        // 红炮 (7,1) 打黑炮 (2,1):中间无子 —— 没有炮架。
        Applying(Start, 7, 1, 2, 1, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_cannon_cannot_slide_over_a_piece()
    {
        // 不吃子时不得越子:红炮 (7,1) 想停在 (1,1),中间有黑炮 (2,1)。
        Applying(Start, 7, 1, 1, 1, Red).Should().Throw<InvalidMoveException>();
    }

    // ---- 将 / 士 ----

    [Fact]
    public void A_general_steps_one_square_inside_the_palace()
    {
        // 红帅 (9,4) → (8,4)。
        Apply(Start, 9, 4, 8, 4, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_general_cannot_leave_the_palace()
    {
        var history = new List<PlayedMove>
        {
            Slide(9, 4, 8, 4, Red), Slide(3, 8, 4, 8, BlackSide),
            Slide(8, 4, 7, 4, Red), Slide(4, 8, 5, 8, BlackSide),
        };

        // (6,4) 已出九宫(红宫是 7–9 行)。
        Applying(history, 7, 4, 6, 4, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_general_cannot_move_diagonally()
    {
        Applying(Start, 9, 4, 8, 3, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void An_advisor_moves_one_diagonal_inside_the_palace()
    {
        Apply(Start, 9, 3, 8, 4, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void An_advisor_cannot_move_orthogonally()
    {
        Applying(Start, 9, 3, 8, 3, Red).Should().Throw<InvalidMoveException>();
    }

    // ---- 自将与照面 ----
    //
    // 这几条要的是**局面**，不是一串好看的棋。`Apply` 重放历史时不再校验每一步
    // （它们当初就是这么被接受的），所以测试可以直接把子搬到想要的位置上 ——
    // 比拼一串合法着法可读得多，也不会因为某步棋恰好将军而跑偏。

    /// <summary>把子从 a 搬到 b，仅用于摆局面。</summary>
    private static PlayedMove Put(int fr, int fc, int tr, int tc, int side)
        => Slide(fr, fc, tr, tc, side);

    [Fact]
    public void Flying_generals_is_illegal()
    {
        // 清空 4 列上挡在两将之间的兵与卒，两将就只隔着空格。
        var history = new List<PlayedMove>
        {
            Put(6, 4, 6, 3, Red),        // 红兵横移出 4 列
            Put(3, 4, 3, 3, BlackSide),  // 黑卒横移出 4 列
        };

        // 红帅 (9,4) → (8,4)：仍在 4 列，而 4 列上两将之间已无子 —— 照面，非法。
        Applying(history, 9, 4, 8, 4, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Two_generals_on_the_same_file_are_fine_with_a_piece_between_them()
    {
        // 对照组：只挪开红兵，黑卒 (3,4) 还挡在中间 —— 不构成照面。
        var history = new List<PlayedMove> { Put(6, 4, 6, 3, Red) };

        Apply(history, 9, 4, 8, 4, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Moving_into_check_is_illegal()
    {
        // 黑车搬到 (5,4)，正照着 4 列；红兵先让开。
        var history = new List<PlayedMove>
        {
            Put(6, 4, 6, 3, Red),
            Put(0, 0, 5, 4, BlackSide),  // 黑车下到 4 列
        };

        // 红帅走进 (8,4) 就在车口上 —— 送将。
        Applying(history, 9, 4, 8, 4, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Leaving_your_general_in_check_is_illegal()
    {
        // 黑车已照住红帅所在的 4 列（红帅在 (9,4)，中间清空）。
        // 红方走一步与解将无关的棋 —— 非法，因为走完仍在将军中。
        var history = new List<PlayedMove>
        {
            Put(6, 4, 6, 3, Red),
            Put(9, 3, 8, 3, Red),        // 红仕让开，别挡住车路（它本就不在 4 列，只为凑步）
            Put(0, 0, 4, 4, BlackSide),  // 黑车到 (4,4)，直照红帅
        };

        Applying(history, 9, 0, 8, 0, Red).Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Blocking_the_check_is_legal()
    {
        // 同一个被将的局面，但这一步是解将：红马从 (9,1) 跳到 (7,2)？那不在 4 列上。
        // 真正能挡的是把某个子送进 (8,4) 或 (7,4)。红相 (9,2) 走田字到 (7,4) 正好挡住。
        var history = new List<PlayedMove>
        {
            Put(6, 4, 6, 3, Red),
            Put(0, 0, 4, 4, BlackSide),  // 黑车照住 4 列
        };

        Apply(history, 9, 2, 7, 4, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Capturing_the_checking_piece_is_legal()
    {
        var history = new List<PlayedMove>
        {
            Put(6, 4, 6, 3, Red),
            Put(0, 0, 7, 4, BlackSide),  // 黑车贴到 (7,4) 将军
        };

        // 红相 (9,2) 走田字吃掉 (7,4) 的车。
        Apply(history, 9, 2, 7, 4, Red).Result.Should().Be(GameResult.Ongoing);
    }

    // ---- 将死与困毙 ----
    //
    // 这两条要的是**残局**。用一串合法着法走到残局是不现实的，而 `Apply` 重放历史时
    // 不再校验每一步，所以测试直接摆子。`Remove` 靠的是「走到某格会覆盖该格的子」——
    // 把要清掉的子依次叠到同一个坟场格上，每一次覆盖删掉前一个。

    /// <summary>坟场格：清子时所有子都往这里叠，只有最后一个会留下。</summary>
    private static readonly Position Graveyard = P(5, 4);

    /// <summary>把 (row, col) 上的子清掉 —— 叠到坟场格，覆盖掉上一个。</summary>
    private static PlayedMove Remove(int row, int col, int side)
        => PlayedMove.Positional(P(row, col), Graveyard, side);

    /// <summary>清掉黑方除将以外的全部棋子，最后用一枚红子吃掉坟场里的幸存者。</summary>
    private static List<PlayedMove> BlackGeneralAlone()
    {
        var h = new List<PlayedMove>();
        foreach (var col in new[] { 0, 1, 2, 3, 5, 6, 7, 8 })   // 跳过 (0,4) 的将
        {
            h.Add(Remove(0, col, BlackSide));
        }
        h.Add(Remove(2, 1, BlackSide));
        h.Add(Remove(2, 7, BlackSide));
        foreach (var col in new[] { 0, 2, 4, 6, 8 })
        {
            h.Add(Remove(3, col, BlackSide));
        }
        // 坟场里还剩最后一枚黑子。用红兵 (6,4) 吃掉它 —— 那枚兵正好就在 (5,4) 的下方。
        h.Add(PlayedMove.Positional(P(6, 4), Graveyard, Red));
        // **这枚兵就留在 (5,4)。** 坟场格选在 4 列上是有意的:两将都在 4 列,把这一列清空
        // 就会构成将帅照面,于是红方任何一步都成了「自将」。第一版把它挪走了,三条残局用例
        // 全挂在 flying generals —— 规则是对的,是摆的局面本身非法。
        return h;
    }

    /// <summary>把红兵从 (6, col) 横移一格，给车让开直线。</summary>
    private static PlayedMove MoveRedSoldierAside(int col)
        => PlayedMove.Positional(P(6, col), P(6, col + 1), Red);

    [Fact]
    public void Checkmate_ends_the_game()
    {
        var h = BlackGeneralAlone();
        h.Add(MoveRedSoldierAside(0));                 // (6,0) → (6,1)，让开 0 列
        h.Add(PlayedMove.Positional(P(6, 8), P(6, 7), Red));  // (6,8) → (6,7)，让开 8 列
        h.Add(PlayedMove.Positional(P(9, 0), P(1, 0), Red));  // 红车占住第 1 行，封死 (1,4)

        // 红另一车 (9,8) 直上 (0,8)：第 0 行已清空 → 照住黑将 (0,4)。
        // 逃格 (0,3) / (0,5) 都在同一条第 0 行上，(1,4) 被 (1,0) 的车封住 —— 将死。
        var result = Apply(h, 9, 8, 0, 8, Red);

        result.Result.Should().Be(GameResult.Decided);
        result.WinnerSeat.Should().Be(BoardSeats.FirstSeat, "将死之后赢的是走子方");
    }

    [Fact]
    public void A_check_with_an_escape_does_not_end_the_game()
    {
        // 对照组：同样是第 0 行的车照将，但不封 (1,4) —— 黑将能逃，对局继续。
        var h = BlackGeneralAlone();
        h.Add(PlayedMove.Positional(P(6, 8), P(6, 7), Red));

        var result = Apply(h, 9, 8, 0, 8, Red);

        result.Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Stalemate_is_a_loss_in_xiangqi_not_a_draw()
    {
        // 困毙：黑将没被将军，但三个逃格全被封 —— 象棋判负，**不是和棋**。
        var h = BlackGeneralAlone();
        h.Add(MoveRedSoldierAside(0));                  // 让开 0 列
        h.Add(PlayedMove.Positional(P(9, 8), P(2, 5), Red));   // 红车封 5 列 → 盖住 (0,5)
        h.Add(PlayedMove.Positional(P(7, 1), P(5, 3), Red));   // 红炮到 (5,3)
        h.Add(PlayedMove.Positional(P(9, 1), P(3, 3), Red));   // 红马当炮架 → 炮打到 (0,3)

        // 最后一步：红车 (9,0) → (1,0)，封住 (1,4)。
        // 此刻 (0,4) 本身没有被任何子攻击（车在第 1 行、另一车在 5 列、炮打 3 列），
        // 而 (0,3)/(0,5)/(1,4) 三个逃格全被封 —— 困毙。
        var result = Apply(h, 9, 0, 1, 0, Red);

        result.Result.Should().Be(GameResult.Decided);
        result.WinnerSeat.Should().Be(BoardSeats.FirstSeat, "将死之后赢的是走子方");
    }

    // ---- 无状态 ----

    [Fact]
    public void The_same_instance_serving_two_histories_does_not_mix_them()
    {
        var moved = new List<PlayedMove> { Slide(6, 0, 5, 0, Red) };

        // 在「兵已经走了」的历史下,(6,0) 是空的 —— 从那里再走一步必须被拒。
        Applying(moved, 6, 0, 5, 0, Red).Should().Throw<InvalidMoveException>();
        // 而在空历史下同一步是合法的。前一次调用没有污染它。
        Apply(Start, 6, 0, 5, 0, Red).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Apply_does_not_mutate_the_history_it_is_given()
    {
        var history = new List<PlayedMove> { Slide(6, 0, 5, 0, Red) };

        Apply(history, 3, 0, 4, 0, BlackSide);

        history.Should().ContainSingle();
    }
}
