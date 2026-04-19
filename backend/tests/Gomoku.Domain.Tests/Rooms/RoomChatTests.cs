using Gomoku.Domain.Exceptions;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomChatTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private static Room PlayingRoom(out UserId black, out UserId white)
    {
        black = UserId.NewId();
        white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "Chat Room", black, Now);
        room.JoinAsPlayer(white, Now.AddMinutes(1));
        return room;
    }

    [Fact]
    public void Player_Posts_To_Room_Channel()
    {
        var room = PlayingRoom(out var b, out _);
        var msg = room.PostChatMessage(b, "Alice", "  good luck  ", ChatChannel.Room, Now.AddMinutes(2));

        msg.Content.Should().Be("good luck");
        msg.Channel.Should().Be(ChatChannel.Room);
        msg.SenderUserId.Should().Be(b);
        msg.SenderUsername.Should().Be("Alice");
        msg.SentAt.Should().Be(Now.AddMinutes(2));
        room.ChatMessages.Should().ContainSingle();
    }

    [Fact]
    public void Spectator_Posts_To_Spectator_Channel()
    {
        var room = PlayingRoom(out _, out _);
        var carol = UserId.NewId();
        room.JoinAsSpectator(carol);

        var msg = room.PostChatMessage(carol, "Carol", "白要赢了", ChatChannel.Spectator, Now.AddMinutes(2));

        msg.Channel.Should().Be(ChatChannel.Spectator);
    }

    [Fact]
    public void Player_Cannot_Post_To_Spectator_Channel()
    {
        var room = PlayingRoom(out var b, out _);
        var act = () => room.PostChatMessage(b, "Alice", "hmm", ChatChannel.Spectator, Now.AddMinutes(2));
        act.Should().Throw<PlayerCannotPostSpectatorChannelException>();
    }

    [Fact]
    public void Non_Member_Cannot_Post()
    {
        var room = PlayingRoom(out _, out _);
        var act = () => room.PostChatMessage(UserId.NewId(), "Eve", "hi", ChatChannel.Room, Now);
        act.Should().Throw<NotInRoomException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Content_Throws(string? content)
    {
        var room = PlayingRoom(out var b, out _);
        var act = () => room.PostChatMessage(b, "Alice", content!, ChatChannel.Room, Now);
        act.Should().Throw<InvalidChatContentException>();
    }

    [Fact]
    public void Too_Long_Content_Throws()
    {
        var room = PlayingRoom(out var b, out _);
        var longContent = new string('a', 501);
        var act = () => room.PostChatMessage(b, "Alice", longContent, ChatChannel.Room, Now);
        act.Should().Throw<InvalidChatContentException>()
            .WithMessage("*out of range*");
    }
}
