using Gomoku.Domain.Exceptions;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomUrgeTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
    private const int Cooldown = 30;

    private static Room PlayingRoom(out UserId black, out UserId white)
    {
        black = UserId.NewId();
        white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "Urge Room", black, Now);
        room.JoinAsPlayer(white, Now.AddMinutes(1));
        return room;
    }

    [Fact]
    public void Opponents_Turn_Urge_Succeeds()
    {
        var room = PlayingRoom(out var b, out var w);
        // 开局轮到黑方,白方催黑方
        var outcome = room.UrgeOpponent(w, Now.AddMinutes(2), Cooldown);

        outcome.UrgedUser.Should().Be(b);
        room.LastUrgeAt.Should().Be(Now.AddMinutes(2));
        room.LastUrgeByUserId.Should().Be(w);
    }

    [Fact]
    public void Own_Turn_Urge_Throws()
    {
        var room = PlayingRoom(out var b, out _);
        // 轮到黑方,黑方想催自己 → Not opponent's turn
        var act = () => room.UrgeOpponent(b, Now.AddMinutes(2), Cooldown);
        act.Should().Throw<NotOpponentsTurnException>();
    }

    [Fact]
    public void Within_Cooldown_Throws()
    {
        var room = PlayingRoom(out _, out var w);
        room.UrgeOpponent(w, Now.AddMinutes(2), Cooldown);

        var act = () => room.UrgeOpponent(w, Now.AddMinutes(2).AddSeconds(10), Cooldown);
        act.Should().Throw<UrgeTooFrequentException>();
    }

    [Fact]
    public void After_Cooldown_Succeeds()
    {
        var room = PlayingRoom(out _, out var w);
        room.UrgeOpponent(w, Now.AddMinutes(2), Cooldown);

        // 等于 cooldownSeconds 不算过期(严格小于才抛) —— 边界测试要 >= cooldown
        var retry = () => room.UrgeOpponent(w, Now.AddMinutes(2).AddSeconds(Cooldown), Cooldown);
        retry.Should().NotThrow();
    }

    [Fact]
    public void Non_Player_Urge_Throws()
    {
        var room = PlayingRoom(out _, out _);
        var act = () => room.UrgeOpponent(UserId.NewId(), Now, Cooldown);
        act.Should().Throw<NotAPlayerException>();
    }

    [Fact]
    public void Non_Playing_State_Throws()
    {
        var room = Room.Create(RoomId.NewId(), "Room", UserId.NewId(), Now);
        var act = () => room.UrgeOpponent(UserId.NewId(), Now, Cooldown);
        act.Should().Throw<RoomNotInPlayException>();
    }

    [Fact]
    public void Spectator_Urge_Throws()
    {
        var room = PlayingRoom(out _, out _);
        var c = UserId.NewId();
        room.JoinAsSpectator(c);
        var act = () => room.UrgeOpponent(c, Now, Cooldown);
        act.Should().Throw<NotAPlayerException>();
    }
}
