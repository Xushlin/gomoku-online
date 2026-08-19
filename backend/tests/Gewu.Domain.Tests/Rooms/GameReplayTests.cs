using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 从走子历史重建棋盘。
/// <para>
/// 这些用例此前调的是 <c>Game.ReplayBoard(rules)</c> —— 重放的归属在
/// <c>generalize-match-domain</c> 里从子实体搬到了规则:<c>Game</c> 只交出**发生过什么**,
/// 盘面怎么重建是规则的私事(象棋的盘面塞不进 <c>Board</c>)。断言一字未改。
/// </para>
/// </summary>
public class GameReplayTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private static Room PlayingRoom(out UserId black, out UserId white)
    {
        black = UserId.NewId();
        white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "Replay", black, Now, GameKeys.Gomoku);
        room.JoinAsPlayer(white, Now.AddMinutes(1), BuiltInGameRules.Gomoku, setup: null);
        return room;
    }

    [Fact]
    public void Empty_Moves_Yields_Empty_Board()
    {
        var room = PlayingRoom(out _, out _);
        var board = BuiltInGameRules.Gomoku.ReplayBoard(room.Game!.History());

        board.GetStone(new Position(0, 0)).Should().Be(Stone.Empty);
        board.GetStone(new Position(7, 7)).Should().Be(Stone.Empty);
        board.GetStone(new Position(14, 14)).Should().Be(Stone.Empty);
    }

    [Fact]
    public void Replay_Reflects_All_Moves()
    {
        var room = PlayingRoom(out var b, out var w);
        room.PlayMove(b, MoveIntent.Place(new Position(7, 7)), Now.AddSeconds(1), BuiltInGameRules.Gomoku);
        room.PlayMove(w, MoveIntent.Place(new Position(8, 8)), Now.AddSeconds(2), BuiltInGameRules.Gomoku);
        room.PlayMove(b, MoveIntent.Place(new Position(7, 8)), Now.AddSeconds(3), BuiltInGameRules.Gomoku);

        var board = BuiltInGameRules.Gomoku.ReplayBoard(room.Game!.History());

        board.GetStone(new Position(7, 7)).Should().Be(Stone.Black);
        board.GetStone(new Position(8, 8)).Should().Be(Stone.White);
        board.GetStone(new Position(7, 8)).Should().Be(Stone.Black);
        board.GetStone(new Position(7, 9)).Should().Be(Stone.Empty);
    }
}
