using Gewu.Domain.Ai;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Xiangqi;

/// <summary>
/// 中国象棋 AI。
/// <para>
/// **这里没有「不可战胜」那种断言。** 象棋不可能穷举,与一字棋 Hard 档那套穷举 minimax
/// 是两回事 —— 那里能断言「落在博弈论最优值上」,这里不能,而一个验不了的断言比没有更糟。
/// 能验的是三件事:着法合法、看得见一步吃子、以及不修改入参。
/// </para>
/// </summary>
public class XiangqiAiTests
{
    private static readonly XiangqiRules Rules = new();
    private static readonly IGameAiFactory Factory = new XiangqiAiFactory();

    /// <summary>红方 —— 先手。</summary>
    private const Stone Red = Stone.Black;

    private static Position P(int r, int c) => new(r, c);

    private static PlayedMove Put(int fr, int fc, int tr, int tc, Stone side)
        => new(P(fr, fc), P(tr, tc), side);

    private static IBoardGameAi Ai(BotDifficulty difficulty, int seed = 1)
        => Factory.Create(difficulty, new Random(seed));

    public static TheoryData<BotDifficulty> AllDifficulties() =>
        new() { BotDifficulty.Easy, BotDifficulty.Medium, BotDifficulty.Hard };

    // ---- 合法性:三档都不许走出规则会拒绝的棋 ----

    [Theory]
    [MemberData(nameof(AllDifficulties))]
    public void Every_difficulty_opens_with_a_legal_move(BotDifficulty difficulty)
    {
        var move = Ai(difficulty).SelectMove([], Red);

        // 判据不是「看起来像一步棋」,而是**规则接受它**。
        Rules.Invoking(r => r.Apply([], move, Red)).Should().NotThrow();
        move.From.Should().NotBeNull("象棋是走子类棋种");
    }

    [Theory]
    [MemberData(nameof(AllDifficulties))]
    public void Every_difficulty_stays_legal_for_a_dozen_plies(BotDifficulty difficulty)
    {
        // 让 AI 自己跟自己下十二步。任何一步被规则拒绝都会在这里炸出来 ——
        // 这比在开局单点上验一次强得多:局面越走越怪,AI 越容易走出边角上的非法着法。
        var ai = Ai(difficulty);
        var history = new List<PlayedMove>();
        var side = Red;

        for (var ply = 0; ply < 12; ply++)
        {
            var move = ai.SelectMove(history, side);
            var result = Rules.Apply(history, move, side);   // 非法就抛
            history.Add(new PlayedMove(move.From, move.To, side));
            if (result.Result != GameResult.Ongoing)
            {
                break;
            }
            side = side == Stone.Black ? Stone.White : Stone.Black;
        }

        history.Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(AllDifficulties))]
    public void Every_difficulty_finds_a_way_out_of_check(BotDifficulty difficulty)
    {
        // 被将时,**任何**合法着法都必然是解将的 —— 不解将的着法根本不合法。
        // 所以这条其实在验:AI 在被将的局面下仍然只从合法集合里挑。
        var history = new List<PlayedMove>
        {
            Put(6, 4, 6, 3, Red),          // 红兵让开 4 列
            Put(0, 0, 4, 4, Stone.White),  // 黑车直照红帅
        };

        var move = Ai(difficulty).SelectMove(history, Red);

        Rules.Invoking(r => r.Apply(history, move, Red)).Should().NotThrow();
    }

    // ---- 会吃白送的子 ----

    [Theory]
    [InlineData(BotDifficulty.Medium)]
    [InlineData(BotDifficulty.Hard)]
    public void It_takes_a_piece_that_is_hanging(BotDifficulty difficulty)
    {
        // 把一个黑车放到红车正前方、且无人保护 —— 白送。
        // Easy 档不参与:它只看一步的吃子价值而不看回应,虽然这一步它多半也会吃,
        // 但那是巧合而不是它的保证,把巧合写进断言会让测试变脆。
        var history = new List<PlayedMove>
        {
            Put(6, 0, 6, 1, Red),          // 红兵让开 0 列
            Put(0, 0, 5, 0, Stone.White),  // 黑车送到 (5,0),红车 (9,0) 直吃
        };

        var move = Ai(difficulty).SelectMove(history, Red);

        move.To.Should().Be(P(5, 0), "白送的车该被吃掉");
        move.From.Should().Be(P(9, 0));
    }

    [Fact]
    public void Easy_still_only_plays_legal_moves_in_that_position()
    {
        // Easy 档不保证吃子,但保证合法 —— 这才是它的契约。
        var history = new List<PlayedMove>
        {
            Put(6, 0, 6, 1, Red),
            Put(0, 0, 5, 0, Stone.White),
        };

        var move = Ai(BotDifficulty.Easy).SelectMove(history, Red);

        Rules.Invoking(r => r.Apply(history, move, Red)).Should().NotThrow();
    }

    // ---- 纯函数 ----

    [Fact]
    public void It_does_not_mutate_the_history_it_is_given()
    {
        var history = new List<PlayedMove> { Put(6, 0, 5, 0, Red) };

        Ai(BotDifficulty.Hard).SelectMove(history, Stone.White);

        history.Should().ContainSingle();
    }

    [Fact]
    public void The_same_seed_gives_the_same_move()
    {
        var a = Factory.Create(BotDifficulty.Medium, new Random(42)).SelectMove([], Red);
        var b = Factory.Create(BotDifficulty.Medium, new Random(42)).SelectMove([], Red);

        a.Should().Be(b);
    }

    [Fact]
    public void With_no_legal_move_it_refuses_rather_than_guessing()
    {
        // 困毙局面 —— 调用方本不该问,但如果问了,抛比编一步非法着法好。
        var history = new List<PlayedMove>
        {
            // 清空黑方除将以外的子(叠进坟场格),再用红兵吃掉幸存者。
            Put(0, 0, 5, 4, Stone.White), Put(0, 1, 5, 4, Stone.White),
            Put(0, 2, 5, 4, Stone.White), Put(0, 3, 5, 4, Stone.White),
            Put(0, 5, 5, 4, Stone.White), Put(0, 6, 5, 4, Stone.White),
            Put(0, 7, 5, 4, Stone.White), Put(0, 8, 5, 4, Stone.White),
            Put(2, 1, 5, 4, Stone.White), Put(2, 7, 5, 4, Stone.White),
            Put(3, 0, 5, 4, Stone.White), Put(3, 2, 5, 4, Stone.White),
            Put(3, 4, 5, 4, Stone.White), Put(3, 6, 5, 4, Stone.White),
            Put(3, 8, 5, 4, Stone.White),
            Put(6, 4, 5, 4, Red),          // 红兵吃掉幸存者,并留在 4 列挡住照面
            Put(6, 0, 6, 1, Red),          // 让开 0 列
            Put(9, 8, 2, 5, Red),          // 红车封 5 列
            Put(7, 1, 5, 3, Red),          // 红炮
            Put(9, 1, 3, 3, Red),          // 红马当炮架 → 炮打 (0,3)
            Put(9, 0, 1, 0, Red),          // 红车封第 1 行
        };

        var act = () => Ai(BotDifficulty.Medium).SelectMove(history, Stone.White);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_empty_side_is_rejected()
    {
        var act = () => Ai(BotDifficulty.Easy).SelectMove([], Stone.Empty);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- 着法枚举与规则同源 ----

    [Fact]
    public void Legal_moves_are_all_accepted_by_Apply()
    {
        // AI 从 LegalMoves 里挑,所以这条等价于「AI 的候选集合里没有一步是规则会拒的」。
        // 两份枚举一旦分叉,表现就是机器人走出非法着法 —— 用户看到的是它卡住了。
        var moves = Rules.LegalMoves([], Red);

        moves.Should().NotBeEmpty();
        foreach (var move in moves)
        {
            Rules.Invoking(r => r.Apply([], move, Red)).Should().NotThrow();
        }
    }

    [Fact]
    public void The_opening_has_the_textbook_number_of_legal_moves()
    {
        // 中国象棋开局红方共 44 着 —— 这是个可以独立查证的数,比「非空」有信息量得多:
        // 少一条说明某种棋子的走法漏了,多一条说明多生成了不该有的。
        Rules.LegalMoves([], Red).Should().HaveCount(44);
    }
}
