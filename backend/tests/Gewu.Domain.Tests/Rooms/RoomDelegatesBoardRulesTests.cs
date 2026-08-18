using System.Reflection;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 聚合根与规则之间的那条边界。
/// <para>
/// <c>Room.PlayMove</c> 现在只验三件事:房间在不在对局中、这人是不是玩家、是不是他的回合。
/// 剩下的全部下沉给了 <c>rules.Apply</c>。这些用例盯的就是这条分工 ——
/// **做完之后加象棋不应该需要再碰 `Room`**,而这只有在「聚合根什么都不判盘面」时才成立。
/// </para>
/// </summary>
public class RoomDelegatesBoardRulesTests
{
    private static readonly DateTime Now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>记录被调用了几次、拿到了什么的探针规则。</summary>
    private sealed class SpyRules : IGameRules
    {
        private readonly GameResult _result;

        public SpyRules(GameResult result = GameResult.Ongoing) => _result = result;

        public string GameKey => "spy";
        public int Rows => 9;
        public int Cols => 9;
        public int SeatCount => 2;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => true;

        public int Calls { get; private set; }
        public MoveIntent? LastIntent { get; private set; }
        public int LastSeat { get; private set; }
        public int LastHistoryCount { get; private set; }
        public Exception? Throw { get; set; }

        public MoveApplication Apply(
            IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
        {
            Calls++;
            LastIntent = intent;
            LastSeat = seat;
            LastHistoryCount = history.Count;
            if (Throw is not null)
            {
                throw Throw;
            }
            return new MoveApplication(_result);
        }
    }

    private static (Room Room, UserId Black, UserId White) PlayingRoom()
    {
        var black = UserId.NewId();
        var white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "spy room", black, Now, "spy");
        room.JoinAsPlayer(white, Now.AddSeconds(1));
        return (room, black, white);
    }

    [Fact]
    public void A_legal_move_reaches_the_rules_with_the_right_side_and_history()
    {
        var (room, black, white) = PlayingRoom();
        var rules = new SpyRules();

        room.PlayMove(black, MoveIntent.Place(new Position(1, 1)), Now.AddSeconds(2), rules);
        room.PlayMove(white, MoveIntent.Place(new Position(2, 2)), Now.AddSeconds(3), rules);

        rules.Calls.Should().Be(2);
        rules.LastSeat.Should().Be(BoardSeats.SecondSeat);
        rules.LastIntent.Should().Be(MoveIntent.Place(new Position(2, 2)));
        rules.LastHistoryCount.Should().Be(1, "第二步看到的历史里应该只有第一步");
    }

    [Fact]
    public void A_non_player_never_reaches_the_rules()
    {
        var (room, _, _) = PlayingRoom();
        var rules = new SpyRules();

        var act = () => room.PlayMove(
            UserId.NewId(), MoveIntent.Place(new Position(1, 1)), Now.AddSeconds(2), rules);

        act.Should().Throw<NotAPlayerException>();
        rules.Calls.Should().Be(0, "身份是聚合根的事,不该浪费一次规则调用");
    }

    [Fact]
    public void Playing_out_of_turn_never_reaches_the_rules()
    {
        var (room, _, white) = PlayingRoom();
        var rules = new SpyRules();

        var act = () => room.PlayMove(
            white, MoveIntent.Place(new Position(1, 1)), Now.AddSeconds(2), rules);

        act.Should().Throw<NotYourTurnException>();
        rules.Calls.Should().Be(0);
    }

    [Fact]
    public void When_the_rules_reject_the_move_the_aggregate_is_untouched()
    {
        var (room, black, _) = PlayingRoom();
        var rules = new SpyRules { Throw = new InvalidMoveException("nope") };

        var act = () => room.PlayMove(
            black, MoveIntent.Place(new Position(1, 1)), Now.AddSeconds(2), rules);

        act.Should().Throw<InvalidMoveException>();
        room.Game!.Moves.Should().BeEmpty();
        room.Game.CurrentTurn.Should().Be(BoardSeats.FirstSeat, "回合不该因为一步非法走子而翻转");
        room.Status.Should().Be(RoomStatus.Playing);
    }

    [Fact]
    public void A_decisive_result_from_the_rules_finishes_the_game()
    {
        var (room, black, _) = PlayingRoom();
        var rules = new SpyRules(GameResult.BlackWin);

        room.PlayMove(black, MoveIntent.Place(new Position(1, 1)), Now.AddSeconds(2), rules);

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.BlackWin);
        room.Game.WinnerUserId.Should().Be(black);
        room.Game.EndReason.Should().Be(GameEndReason.Decided);
    }

    [Fact]
    public void A_draw_from_the_rules_finishes_with_no_winner()
    {
        var (room, black, _) = PlayingRoom();
        var rules = new SpyRules(GameResult.Draw);

        room.PlayMove(black, MoveIntent.Place(new Position(1, 1)), Now.AddSeconds(2), rules);

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.Draw);
        room.Game.WinnerUserId.Should().BeNull();
        room.Game.EndReason.Should().Be(GameEndReason.Decided);
    }

    [Fact]
    public void An_origin_bearing_move_is_stored_and_replayed()
    {
        // 聚合根不关心这个棋种走不走子 —— 它照样存起点。象棋靠的就是这条。
        var (room, black, _) = PlayingRoom();
        var rules = new SpyRules();

        room.PlayMove(
            black, MoveIntent.Slide(new Position(0, 1), new Position(2, 2)), Now.AddSeconds(2), rules);

        var stored = room.Game!.Moves.Single();
        stored.FromRow.Should().Be(0);
        stored.FromCol.Should().Be(1);
        stored.Row.Should().Be(2);
        stored.Col.Should().Be(2);

        room.Game.History().Single().Should()
            .Be(PlayedMove.Positional(new Position(0, 1), new Position(2, 2), BoardSeats.FirstSeat));
    }

    [Fact]
    public void A_placement_move_stores_a_null_origin()
    {
        var (room, black, _) = PlayingRoom();
        var rules = new SpyRules();

        room.PlayMove(black, MoveIntent.Place(new Position(3, 4)), Now.AddSeconds(2), rules);

        var stored = room.Game!.Moves.Single();
        stored.FromRow.Should().BeNull();
        stored.FromCol.Should().BeNull();
        stored.FromPosition().Should().BeNull();
    }

    [Fact]
    public void The_end_reason_enum_names_no_specific_game()
    {
        // 一字棋从上线第一天起就在给三连记录「Connected5」,象棋会给将死记录同一个词。
        // 那不是陈旧,是错的 —— 这个字段回答的是「怎么结束的」,不是「什么条件赢的」。
        var names = Enum.GetNames<GameEndReason>();

        names.Should().BeEquivalentTo(["Decided", "Resigned", "TurnTimeout"]);
        // 底层值不动:数据库存的是 int,既有行不需要改写。
        ((int)GameEndReason.Decided).Should().Be(0);
    }

    [Fact]
    public void The_aggregate_exposes_history_not_a_board()
    {
        // Game 交出的是「发生过什么」,不是一块棋盘。象棋的盘面塞不进 Board,
        // 所以这条边界是它能进这个聚合的前提。
        var members = typeof(Game)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();

        members.Should().NotContain("ReplayBoard");
        members.Should().Contain("History");
    }
}
