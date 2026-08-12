using Gomoku.Application.Features.Rooms.SendChatMessage;

namespace Gomoku.Application.Tests.Features.Rooms;

public class SendChatMessageCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private SendChatMessageCommandHandler Build() => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object);

    [Fact]
    public async Task Player_Sends_Room_Channel_Successfully()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, host);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = await Build().Handle(
            new SendChatMessageCommand(host.Id, room.Id, "  good luck  ", ChatChannel.Room),
            default);

        dto.Content.Should().Be("good luck");
        dto.Channel.Should().Be(ChatChannel.Room);
        dto.SenderUsername.Should().Be("Alice");

        _notifier.Verify(n => n.ChatMessagePostedAsync(room.Id, ChatChannel.Room,
            It.IsAny<ChatMessageDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Spectator_Sends_Spectator_Channel_Successfully()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var carol = RoomsFixtures.NewUser("Carol", "carol@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        room.JoinAsSpectator(carol.Id);

        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, carol);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = await Build().Handle(
            new SendChatMessageCommand(carol.Id, room.Id, "白方有机会", ChatChannel.Spectator),
            default);

        dto.Channel.Should().Be(ChatChannel.Spectator);
        _notifier.Verify(n => n.ChatMessagePostedAsync(room.Id, ChatChannel.Spectator,
            It.IsAny<ChatMessageDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Player_Posting_To_Spectator_Channel_Throws()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, host);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(
            new SendChatMessageCommand(host.Id, room.Id, "hmm", ChatChannel.Spectator),
            default);

        await act.Should().ThrowAsync<Gomoku.Domain.Exceptions.PlayerCannotPostSpectatorChannelException>();
    }
}
