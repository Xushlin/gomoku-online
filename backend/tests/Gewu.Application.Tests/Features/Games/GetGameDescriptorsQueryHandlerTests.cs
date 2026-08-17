using Gewu.Application.Features.Games.GetGameDescriptors;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Games;

/// <summary>
/// <c>GET /api/games</c> 的 handler —— 注册表的只读投影。
/// <para>
/// 这些用例的重点不是"字段抄对了",而是**它是投影而不是第二份清单**:断言对着注册表本身
/// 逐条比,不写死"应该有 gomoku 和 tictactoe 两条"。写死清单的测试会在加中国象棋时静静通过,
/// 而那正是这个端点存在的意义失效的时刻。
/// </para>
/// </summary>
public class GetGameDescriptorsQueryHandlerTests
{
    private static readonly IGameRulesRegistry Registry = GomokuRules.Registry;

    private static GetGameDescriptorsQueryHandler Build() => new(Registry, GomokuRules.AiRegistry);

    [Fact]
    public async Task Returns_one_entry_per_registered_game_no_more_no_less()
    {
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        items.Select(i => i.GameKey)
            .Should().BeEquivalentTo(Registry.All.Select(r => r.GameKey));
    }

    [Fact]
    public async Task Every_field_mirrors_the_rules_instance()
    {
        // 逐条对着注册表比,而不是对着一份手写的期望值 —— 后者是第二份清单,
        // 也就是这个端点想要消灭的那个东西。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        foreach (var rules in Registry.All)
        {
            var dto = items.Single(i => i.GameKey == rules.GameKey);
            dto.IsRated.Should().Be(rules.IsRated);
            dto.SupportsHumanVsHuman.Should().Be(rules.SupportsHumanVsHuman);
            // SupportsAi 对着 AI 注册表比,不对着规则比 —— 规则不知道自己有没有机器人,
            // 而让它「知道」就是在同一件事上开第二个真源。
            dto.SupportsAi.Should().Be(GomokuRules.AiRegistry.For(rules.GameKey) is not null);
            // 尺寸当且仅当规则有盘面时非空。
            dto.Rows.Should().Be((rules as IBoardGameRules)?.Rows);
            dto.Cols.Should().Be((rules as IBoardGameRules)?.Cols);
        }
    }

    [Fact]
    public async Task A_game_with_an_ai_says_so_and_one_without_says_so()
    {
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        items.Single(i => i.GameKey == GameKeys.Gomoku).SupportsAi.Should().BeTrue();
        items.Single(i => i.GameKey == GameKeys.IdiomChain).SupportsAi.Should().BeFalse();
    }

    [Fact]
    public async Task The_descriptor_and_the_ai_room_validator_read_the_same_registry()
    {
        // 客户端按 supportsAi 决定画不画那个按钮,服务端按同一份注册表决定接不接受。
        // 两者只要来源不同,就会有一天出现一个永远 400 的按钮 —— 这条断言让它们不可能不同。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);
        var validator = new Gewu.Application.Features.Rooms.CreateAiRoom.CreateAiRoomCommandValidator(
            GomokuRules.Registry, GomokuRules.AiRegistry);

        foreach (var dto in items)
        {
            var command = new Gewu.Application.Features.Rooms.CreateAiRoom.CreateAiRoomCommand(
                Gewu.Domain.Users.UserId.NewId(), "a valid name",
                Gewu.Domain.Ai.BotDifficulty.Easy, Stone.Black, dto.GameKey);

            validator.Validate(command).IsValid.Should().Be(
                dto.SupportsAi, "'{0}' publishes supportsAi == {1}", dto.GameKey, dto.SupportsAi);
        }
    }

    [Fact]
    public async Task Gomoku_is_rated_and_fifteen_by_fifteen()
    {
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        var gomoku = items.Single(i => i.GameKey == GameKeys.Gomoku);
        gomoku.IsRated.Should().BeTrue();
        gomoku.SupportsHumanVsHuman.Should().BeTrue();
        gomoku.Rows.Should().Be(15);
        gomoku.Cols.Should().Be(15);
    }

    [Fact]
    public async Task Xiangqi_is_rated_and_open_to_human_play()
    {
        // 点名象棋,因为遍历守不住某个特定成员的值 —— Every_field_mirrors_the_rules_instance
        // 会在象棋被翻回 AI-only 之后依然全绿(它只断言 DTO 与规则一致,不管规则说什么)。
        // 计分这件事在客户端的可见后果是阶梯页出现,而那按 isRated 渲染,不需要任何新代码。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        var xiangqi = items.Single(i => i.GameKey == GameKeys.Xiangqi);
        xiangqi.SupportsHumanVsHuman.Should().BeTrue();
        xiangqi.IsRated.Should().BeTrue();
        xiangqi.SupportsAi.Should().BeTrue("象棋既有真人对手也有机器人");
    }

    [Fact]
    public async Task TicTacToe_is_the_only_unrated_versus_game()
    {
        // 一字棋是唯一不计分的对战棋种,也因此是好几条"两类都要出现过"的遍历断言在
        // 拒绝那一侧的唯一样本。哪天它也计分了,那些断言会变红 —— 那是想要的。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        items.Where(i => !i.IsRated).Should().ContainSingle()
            .Which.GameKey.Should().Be(GameKeys.TicTacToe);
    }

    [Fact]
    public async Task TicTacToe_is_unrated_and_three_by_three()
    {
        // 这一条是整个端点存在的理由的可执行形式:前端靠它决定一字棋卡片**没有**排行榜入口。
        // 若这个事实改由前端自己维护一份副本,失配的症状会是"一个永远空着的榜" ——
        // 与"新棋种还没人下过"在屏幕上一模一样,也就是不会被发现。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        var ttt = items.Single(i => i.GameKey == GameKeys.TicTacToe);
        ttt.IsRated.Should().BeFalse();
        ttt.SupportsHumanVsHuman.Should().BeFalse();
        ttt.Rows.Should().Be(3);
        ttt.Cols.Should().Be(3);
    }

    [Fact]
    public async Task Entries_are_ordered_by_game_key()
    {
        // 注册表是 DI 集合 → 字典,顺序不作保证。一个每次刷新都换序的列表在 UI 上
        // 会让人以为数据在变。排序放在服务端 —— 让每个客户端各排一次,它们迟早排得不一样。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        items.Select(i => i.GameKey).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_registry_with_one_game_yields_one_entry()
    {
        var items = await new GetGameDescriptorsQueryHandler(GomokuRules.GomokuOnly, GomokuRules.AiRegistry)
            .Handle(new GetGameDescriptorsQuery(), default);

        items.Should().ContainSingle().Which.GameKey.Should().Be(GameKeys.Gomoku);
    }

    [Fact]
    public async Task The_dto_does_not_carry_WinLength()
    {
        // WinLength 在 IGameRules 上是因为今天的棋种恰好都是「连 N 子」,而中国象棋没有这个概念。
        // 把一个对将来的棋种无意义的字段放进对外契约,只会让客户端学着去读它。
        await Task.CompletedTask;

        // 断言的是**整个**属性集合而不是「不含 WinLength」—— 加字段时它会红,那正是想要的:
        // 对外契约多一个字段该是一次有意的决定,不是一次顺手的提交。
        typeof(Gewu.Application.Common.DTOs.GameDescriptorDto)
            .GetProperties()
            .Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                nameof(Gewu.Application.Common.DTOs.GameDescriptorDto.SupportsAi),
                "GameKey", "IsRated", "SupportsHumanVsHuman", "Rows", "Cols",
            });
    }
}
