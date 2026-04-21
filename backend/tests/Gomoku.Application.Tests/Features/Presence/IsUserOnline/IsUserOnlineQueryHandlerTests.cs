using Gomoku.Application.Features.Presence.IsUserOnline;

namespace Gomoku.Application.Tests.Features.Presence.IsUserOnline;

public class IsUserOnlineQueryHandlerTests
{
    private readonly Mock<IConnectionTracker> _tracker = new();
    private IsUserOnlineQueryHandler Build() => new(_tracker.Object);

    [Fact]
    public async Task Online_User_Returns_True()
    {
        var userId = UserId.NewId();
        _tracker.Setup(t => t.IsUserOnline(userId)).Returns(true);

        var result = await Build().Handle(new IsUserOnlineQuery(userId), default);

        result.UserId.Should().Be(userId.Value);
        result.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task Offline_User_Returns_False()
    {
        var userId = UserId.NewId();
        _tracker.Setup(t => t.IsUserOnline(userId)).Returns(false);

        var result = await Build().Handle(new IsUserOnlineQuery(userId), default);

        result.UserId.Should().Be(userId.Value);
        result.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_UserId_Tracker_Returns_False_Not_Throws()
    {
        // tracker 对未知 UserId 按契约返回 false(不抛);handler 透传
        var unknownId = UserId.NewId();
        _tracker.Setup(t => t.IsUserOnline(unknownId)).Returns(false);

        var result = await Build().Handle(new IsUserOnlineQuery(unknownId), default);

        result.IsOnline.Should().BeFalse();
    }
}
