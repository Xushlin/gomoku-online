using Gewu.Domain.Enums;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.IdiomChain;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.IdiomChain;

/// <summary>
/// **`generalize-match-payload` 的验收条件,第一次被真正检验。**
/// <para>
/// `XiangqiThroughRoomTests` 证的是"走子类载荷能穿过聚合"。这里证的更强:一个
/// **没有盘面、没有坐标、规则永不判胜负**的棋种,走的是同一个 `Room`。
/// 哪一条需要给成语接龙开特例,就说明上一次抽象抽错了地方。
/// </para>
/// </summary>
public class IdiomChainThroughRoomTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IGameRules Rules = new IdiomChainRules(IdiomLexicons.Small);

    private static (Room Room, UserId First, UserId Second) PlayingRoom()
    {
        var first = UserId.NewId();
        var second = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "chain room", first, Now, GameKeys.IdiomChain);
        room.JoinAsPlayer(second, Now.AddSeconds(1), BuiltInGameRules.Gomoku);
        return (room, first, second);
    }

    private static MoveOutcome Say(Room room, UserId who, string word, int atSecond)
        => room.PlayMove(who, MoveIntent.Say(word), Now.AddSeconds(atSecond), Rules);

    [Fact]
    public void A_whole_chain_plays_through_the_real_aggregate()
    {
        var (room, first, second) = PlayingRoom();

        Say(room, first, "一心一意", 2).Result.Should().Be(GameResult.Ongoing);
        Say(room, second, "意气风发", 3).Result.Should().Be(GameResult.Ongoing);
        Say(room, first, "发号施令", 4).Result.Should().Be(GameResult.Ongoing);
        Say(room, second, "令行禁止", 5).Result.Should().Be(GameResult.Ongoing);

        var moves = room.Game!.Moves.OrderBy(m => m.Ply).ToList();
        moves.Select(m => m.Ply).Should().Equal(1, 2, 3, 4);
        moves.Select(m => m.Text)
            .Should().Equal("一心一意", "意气风发", "发号施令", "令行禁止");
    }

    [Fact]
    public void Every_recorded_move_is_textual_with_no_coordinates_at_all()
    {
        var (room, first, second) = PlayingRoom();
        Say(room, first, "一心一意", 2);
        Say(room, second, "意气风发", 3);

        foreach (var move in room.Game!.Moves)
        {
            move.Text.Should().NotBeNullOrWhiteSpace();
            move.Row.Should().BeNull();
            move.Col.Should().BeNull();
            move.FromRow.Should().BeNull();
            move.FromCol.Should().BeNull();
        }
    }

    [Fact]
    public void An_illegal_link_is_refused_and_leaves_the_game_untouched()
    {
        var (room, first, second) = PlayingRoom();
        Say(room, first, "一心一意", 2);

        var act = () => Say(room, second, "风和日丽", 3);

        act.Should().Throw<InvalidMoveException>();
        room.Game!.Moves.Should().HaveCount(1);
        room.Game.CurrentTurn.Should().Be(BoardSeats.SecondSeat, "the refused player still owes a move");
    }

    [Fact]
    public void Turn_order_is_the_kernels_not_the_games()
    {
        var (room, first, second) = PlayingRoom();
        Say(room, first, "一心一意", 2);

        // 第一个玩家连说两句 —— 由聚合根拦下,规则根本不会被调用。
        var act = () => Say(room, first, "意气风发", 3);

        act.Should().Throw<NotYourTurnException>();
    }

    [Fact]
    public void The_game_does_not_end_on_its_own_but_a_timeout_ends_it()
    {
        // 规则永不判胜负,所以结束只能来自内核既有的两条路径之一。
        var (room, first, second) = PlayingRoom();
        Say(room, first, "一心一意", 2);

        // 进行中时 Result 为 null —— 规则从不写它,而接龙也没有能让规则写它的局面。
        room.Game!.Result.Should().BeNull();

        var ended = room.TimeOutCurrentTurn(Now.AddMinutes(10), turnTimeoutSeconds: 60);

        ended.Should().NotBeNull();
        room.Status.Should().Be(RoomStatus.Finished);
        room.Game.EndReason.Should().Be(GameEndReason.TurnTimeout);
        room.Game.Result.Should().Be(GameResult.BlackWin, "the player who could not answer loses");
    }
}
