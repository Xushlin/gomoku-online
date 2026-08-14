using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
namespace Gewu.Domain.Tests.Rooms;

public class RoomCreateTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Sets_Initial_State()
    {
        var id = RoomId.NewId();
        var host = UserId.NewId();
        var room = Room.Create(id, "  Alice's Room  ", host, Now, GameKeys.Gomoku);

        room.Id.Should().Be(id);
        room.GameKey.Should().Be(GameKeys.Gomoku);
        room.Name.Should().Be("Alice's Room"); // trimmed
        room.HostUserId.Should().Be(host);
        room.BlackPlayerId.Should().Be(host);
        room.WhitePlayerId.Should().BeNull();
        room.Status.Should().Be(RoomStatus.Waiting);
        room.CreatedAt.Should().Be(Now);
        room.LastUrgeAt.Should().BeNull();
        room.LastUrgeByUserId.Should().BeNull();
        room.Game.Should().BeNull();
        room.Spectators.Should().BeEmpty();
        room.ChatMessages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Create_Blank_Name_Throws(string? name)
    {
        var act = () => Room.Create(RoomId.NewId(), name!, UserId.NewId(), Now, GameKeys.Gomoku);
        act.Should().Throw<Gewu.Domain.Exceptions.InvalidRoomNameException>();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void Create_Short_Name_Throws(string name)
    {
        var act = () => Room.Create(RoomId.NewId(), name, UserId.NewId(), Now, GameKeys.Gomoku);
        act.Should().Throw<Gewu.Domain.Exceptions.InvalidRoomNameException>()
            .WithMessage("*out of range*");
    }

    [Fact]
    public void Create_Long_Name_Throws()
    {
        var long51 = new string('x', 51);
        var act = () => Room.Create(RoomId.NewId(), long51, UserId.NewId(), Now, GameKeys.Gomoku);
        act.Should().Throw<Gewu.Domain.Exceptions.InvalidRoomNameException>()
            .WithMessage("*out of range*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Create_Blank_GameKey_Throws(string? gameKey)
    {
        var act = () => Room.Create(RoomId.NewId(), "valid name", UserId.NewId(), Now, gameKey!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Records_Whatever_GameKey_It_Is_Given()
    {
        // Domain 不认识注册表,所以它**不**校验键是否已登记 —— 那是 Application 层
        // 两个建房 validator 的职责。这里断言的是分工,而不是漏洞:Room 保持为其入参的
        // 纯函数,测试里不需要一个注册表才能构造出来。
        var room = Room.Create(
            RoomId.NewId(), "valid name", UserId.NewId(), Now, "a-game-nobody-registered");

        room.GameKey.Should().Be("a-game-nobody-registered");
    }

    [Fact]
    public void Create_Sets_TicTacToe_GameKey()
    {
        var room = Room.Create(RoomId.NewId(), "ttt room", UserId.NewId(), Now, GameKeys.TicTacToe);

        room.GameKey.Should().Be("tictactoe");
        room.Status.Should().Be(RoomStatus.Waiting);
    }
}
