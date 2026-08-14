using Gewu.Application.Common.Mapping;
using Gewu.Application.Tests.Features.Rooms;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Common;

/// <summary>
/// 两个房间 DTO 都带棋种键。
/// <para>
/// 这不是一条形式化的字段存在性测试。客户端进入一个房间有四条路 —— 从建房页跳转、
/// 刷新、收藏链接、从"我的对局"进入 —— 只有第一条上它知道棋种。另外三条它只有一个
/// 房间 id,而没有这个字段就没有任何东西能区分 3×3 与 15×15,棋盘只能画错。
/// </para>
/// </summary>
public class RoomDtoGameKeyTests
{
    private static readonly IReadOnlyDictionary<Guid, string> NoNames =
        new Dictionary<Guid, string>();

    [Theory]
    [InlineData(GameKeys.Gomoku)]
    [InlineData(GameKeys.TicTacToe)]
    public void ToSummary_carries_the_game_key(string gameKey)
    {
        var host = RoomsFixtures.NewUser("Alice");
        var room = RoomsFixtures.WaitingRoom(host, "a room", gameKey);

        room.ToSummary(NoNames).GameKey.Should().Be(gameKey);
    }

    [Theory]
    [InlineData(GameKeys.Gomoku)]
    [InlineData(GameKeys.TicTacToe)]
    public void ToState_carries_the_game_key(string gameKey)
    {
        var host = RoomsFixtures.NewUser("Alice");
        var guest = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, guest, "a room", gameKey);

        room.ToState(NoNames, turnTimeoutSeconds: 60).GameKey.Should().Be(gameKey);
    }

    [Fact]
    public void The_key_is_whatever_the_room_says_not_a_default()
    {
        // 若哪天有人图省事在映射里写死 "gomoku",上面两条 Theory 的 gomoku 分支照样过。
        // 这条用一个绝不会被当作缺省的值,把那种写法钉死。
        var host = RoomsFixtures.NewUser("Alice");
        var room = RoomsFixtures.WaitingRoom(host, "a room", "a-game-nobody-registered");

        room.ToSummary(NoNames).GameKey.Should().Be("a-game-nobody-registered");
    }
}
