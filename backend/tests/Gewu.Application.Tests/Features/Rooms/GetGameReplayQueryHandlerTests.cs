using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Features.Rooms.GetGameReplay;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;
using Move = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Application.Tests.Features.Rooms;

public class GetGameReplayQueryHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();

    private GetGameReplayQueryHandler Build() => new(_rooms.Object, _users.Object);

    /// <summary>
    /// 构造一个 Finished 房间:Alice(host/black) + Bob(white),Alice 黑方连五胜;
    /// 共落 9 步。
    /// </summary>
    private static Room FinishedRoom(User alice, User bob)
    {
        var room = Room.Create(RoomId.NewId(), "replay-test", alice.Id, RoomsFixtures.Now, GameKeys.Gomoku);
        room.JoinAsPlayer(bob.Id, RoomsFixtures.Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);

        // Alice (黑) 在第 7 行连五,Bob 在第 0 行被动应对
        var start = RoomsFixtures.Now.AddSeconds(2);
        for (var i = 0; i < 4; i++)
        {
            room.PlayMove(alice.Id, MoveIntent.Place(new Position(7, i)), start.AddSeconds(i * 2), BuiltInGameRules.Gomoku);
            room.PlayMove(bob.Id, MoveIntent.Place(new Position(0, i)), start.AddSeconds(i * 2 + 1), BuiltInGameRules.Gomoku);
        }
        room.PlayMove(alice.Id, MoveIntent.Place(new Position(7, 4)), start.AddSeconds(9), BuiltInGameRules.Gomoku);
        return room;
    }

    [Fact]
    public async Task Success_Returns_Replay_With_Ordered_Moves_And_Usernames()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = FinishedRoom(alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var dto = await Build().Handle(new GetGameReplayQuery(room.Id), default);

        dto.RoomId.Should().Be(room.Id.Value);
        dto.Name.Should().Be("replay-test");
        dto.Black.Id.Should().Be(alice.Id.Value);
        dto.Black.Username.Should().Be("Alice");
        dto.White.Id.Should().Be(bob.Id.Value);
        dto.White.Username.Should().Be("Bob");
        dto.Host.Id.Should().Be(alice.Id.Value);
        dto.Result.Should().Be(GameResult.Decided);
        dto.WinnerUserId.Should().Be(alice.Id.Value);
        dto.EndReason.Should().Be(GameEndReason.Decided);
        dto.Moves.Should().HaveCount(9);
        // Moves 按 Ply 升序
        dto.Moves.Select(m => m.Ply).Should().BeInAscendingOrder();
        dto.Moves[0].Ply.Should().Be(1);
        dto.Moves[8].Ply.Should().Be(9);
    }

    [Fact]
    public async Task Room_Not_Found_Throws_RoomNotFound()
    {
        var roomId = RoomId.NewId();
        _rooms.Setup(r => r.FindByIdAsync(roomId, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);

        var act = () => Build().Handle(new GetGameReplayQuery(roomId), default);

        await act.Should().ThrowAsync<RoomNotFoundException>();
    }

    [Fact]
    public async Task Playing_Room_Throws_GameNotFinished()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new GetGameReplayQuery(room.Id), default);

        await act.Should().ThrowAsync<GameNotFinishedException>();
    }

    [Fact]
    public async Task Waiting_Room_Throws_GameNotFinished()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var room = RoomsFixtures.WaitingRoom(alice);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new GetGameReplayQuery(room.Id), default);

        await act.Should().ThrowAsync<GameNotFinishedException>();
    }
}
