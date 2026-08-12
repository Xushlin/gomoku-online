using Gomoku.Application.Features.Presence.GetOnlineCount;

namespace Gomoku.Application.Tests.Features.Presence.GetOnlineCount;

public class GetOnlineCountQueryHandlerTests
{
    private readonly Mock<IConnectionTracker> _tracker = new();
    private GetOnlineCountQueryHandler Build() => new(_tracker.Object);

    [Fact]
    public async Task Returns_Tracker_Count()
    {
        _tracker.Setup(t => t.GetOnlineUserCount()).Returns(42);
        var result = await Build().Handle(new GetOnlineCountQuery(), default);
        result.Count.Should().Be(42);
    }

    [Fact]
    public async Task Zero_Count_Is_Not_Error()
    {
        _tracker.Setup(t => t.GetOnlineUserCount()).Returns(0);
        var result = await Build().Handle(new GetOnlineCountQuery(), default);
        result.Count.Should().Be(0);
    }
}
