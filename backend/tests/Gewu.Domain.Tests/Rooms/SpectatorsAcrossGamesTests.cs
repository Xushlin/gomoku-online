using System.Text.RegularExpressions;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 围观与围观评论是**对战内核的能力**,不属于任何一个棋种。
/// <para>
/// 这些用例不新建任何机制 —— 整套东西早就在内核里。它们把一件**已经为真但从未被断言**的事
/// 变成可检验的:「围观对所有对战棋种可用」此前是一个由"代码里没有棋种分支"推出来的**推断**。
/// 而这个仓库反复付过同一种账 —— <c>SupportsHumanVsHuman</c> 被声明、被发布、被当作承重事实
/// 使用,却没有任何机制维持它。**一条没有断言的正确结论,与一条没人检查的结论,长得一模一样。**
/// </para>
/// </summary>
public class SpectatorsAcrossGamesTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>每一个开放人人对战的棋种。围观应当对它们全部成立。</summary>
    public static TheoryData<string> HumanPlayGameKeys()
    {
        var data = new TheoryData<string>();
        foreach (var rules in BuiltInGameRules.All(IdiomLexicons.Small))
        {
            if (rules.SupportsHumanVsHuman)
            {
                data.Add(rules.GameKey);
            }
        }
        return data;
    }

    [Fact]
    public void The_walk_covers_more_than_one_game()
    {
        // 一个只走到单个棋种的遍历会全绿地什么都不验。这条守的**只是**这件事。
        //
        // 我最初在这里写的是「在象棋翻过来之前,这里只有五子棋」——**那句话是假的**,而变异测试
        // 当场证伪了它:把象棋翻回 AI-only 之后本条依然通过,因为成语接龙(上一个变更)也开人人
        // 对战。也就是说象棋带来的是第三个,不是第二个。
        //
        // 留下这段是因为错的那句注释比缺的注释更危险:它会让下一个人以为这条断言守着
        // 「象棋没被悄悄翻回去」,而它守不住那个。要守那件事,就得点名象棋 —— 见下一条。
        BuiltInGameRules.All(IdiomLexicons.Small)
            .Count(r => r.SupportsHumanVsHuman)
            .Should().BeGreaterThan(1);
    }

    [Fact]
    public void Xiangqi_is_among_the_games_this_walk_covers()
    {
        // 上一条守不住"象棋被翻回去" —— 这一条守得住,而它需要点名象棋才行。
        // 一条遍历断言能覆盖多少,取决于被遍历的集合;要断言某个特定成员在里面,只能说出它的名字。
        HumanPlayGameKeys().Should().Contain(row => row.Contains(GameKeys.Xiangqi));
    }

    private static (Room Room, UserId A, UserId B, UserId C, UserId D) RoomWithTwoSpectators(string gameKey)
    {
        var a = UserId.NewId();
        var b = UserId.NewId();
        var c = UserId.NewId();
        var d = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "watched room", a, Now, gameKey);
        room.JoinAsPlayer(b, Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);
        room.JoinAsSpectator(c);
        room.JoinAsSpectator(d);
        return (room, a, b, c, d);
    }

    [Theory]
    [MemberData(nameof(HumanPlayGameKeys))]
    public void Two_spectators_can_both_comment_on_any_human_play_game(string gameKey)
    {
        var (room, _, _, c, d) = RoomWithTwoSpectators(gameKey);

        room.PostChatMessage(c, "Carol", "红方这步妙", ChatChannel.Spectator, Now.AddSeconds(2));
        room.PostChatMessage(d, "Dave", "我看黑方要输", ChatChannel.Spectator, Now.AddSeconds(3));

        var spectatorChat = room.ChatMessages.Where(m => m.Channel == ChatChannel.Spectator).ToList();
        spectatorChat.Should().HaveCount(2);
        spectatorChat.Select(m => m.SenderUsername).Should().BeEquivalentTo(["Carol", "Dave"]);
    }

    [Theory]
    [MemberData(nameof(HumanPlayGameKeys))]
    public void Players_cannot_post_to_the_spectator_channel_in_any_game(string gameKey)
    {
        // 这一半最容易悄悄坏掉,因为它的失效方式是"多显示了东西",而多显示不会报错。
        var (room, a, b, _, _) = RoomWithTwoSpectators(gameKey);

        var byBlack = () => room.PostChatMessage(a, "Alice", "hmm", ChatChannel.Spectator, Now.AddSeconds(2));
        var byWhite = () => room.PostChatMessage(b, "Bob", "hmm", ChatChannel.Spectator, Now.AddSeconds(3));

        byBlack.Should().Throw<PlayerCannotPostSpectatorChannelException>();
        byWhite.Should().Throw<PlayerCannotPostSpectatorChannelException>();
    }

    [Theory]
    [MemberData(nameof(HumanPlayGameKeys))]
    public void The_room_channel_is_shared_by_players_and_spectators_in_any_game(string gameKey)
    {
        var (room, a, _, c, _) = RoomWithTwoSpectators(gameKey);

        room.PostChatMessage(a, "Alice", "good luck", ChatChannel.Room, Now.AddSeconds(2));
        room.PostChatMessage(c, "Carol", "加油", ChatChannel.Room, Now.AddSeconds(3));

        room.ChatMessages.Where(m => m.Channel == ChatChannel.Room).Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(HumanPlayGameKeys))]
    public void Spectating_is_idempotent_and_closed_to_players_in_any_game(string gameKey)
    {
        var (room, a, _, c, _) = RoomWithTwoSpectators(gameKey);

        room.JoinAsSpectator(c); // 重复围观
        room.Spectators.Count(s => s == c).Should().Be(1);

        var byPlayer = () => room.JoinAsSpectator(a);
        byPlayer.Should().Throw<PlayerCannotSpectateException>();
    }

    [Theory]
    [MemberData(nameof(HumanPlayGameKeys))]
    public void There_is_no_cap_on_spectators_in_any_game(string gameKey)
    {
        var (room, _, _, _, _) = RoomWithTwoSpectators(gameKey);

        for (var i = 0; i < 50; i++)
        {
            room.JoinAsSpectator(UserId.NewId());
        }

        room.Spectators.Should().HaveCount(52);
    }

    [Fact]
    public void The_room_aggregate_never_branches_on_game_key_for_spectating_or_chat()
    {
        // 上面那些断言证的是行为;这一条证的是它**没有别的路可走**。一个 switch (GameKey) 的
        // 实现可以在今天这四个棋种上碰巧全绿,而它的第一个反例会是第五个棋种。
        var source = File.ReadAllText(
            Path.Combine(SolutionRoot(), "src", "Gewu.Domain", "Rooms", "Room.cs"));

        var spectatorAndChat = string.Concat(
            Section(source, "public void JoinAsSpectator"),
            Section(source, "public void LeaveAsSpectator"),
            Section(source, "public ChatMessage PostChatMessage"));

        spectatorAndChat.Should().NotContain("GameKey");
        Regex.IsMatch(spectatorAndChat, @"\bGameKeys\.")
            .Should().BeFalse("spectating and chat must not name a single game");
    }

    /// <summary>从一个方法签名截到下一个 <c>public</c> 成员之前。</summary>
    private static string Section(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"'{signature}' should exist in Room.cs");
        var next = source.IndexOf("\n    public ", start + signature.Length, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }

    /// <summary>从测试程序集向上找到解决方案根 —— 源码断言要读文件。</summary>
    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gewu.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Gewu.slnx not found above the test binaries.");
    }
}
