using Gewu.Application.Features.Games.GetGameDescriptors;
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

    private static GetGameDescriptorsQueryHandler Build() => new(Registry);

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
            dto.Rows.Should().Be(rules.Rows);
            dto.Cols.Should().Be(rules.Cols);
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
        var items = await new GetGameDescriptorsQueryHandler(GomokuRules.GomokuOnly)
            .Handle(new GetGameDescriptorsQuery(), default);

        items.Should().ContainSingle().Which.GameKey.Should().Be(GameKeys.Gomoku);
    }

    [Fact]
    public async Task The_dto_does_not_carry_WinLength()
    {
        // WinLength 在 IGameRules 上是因为今天的棋种恰好都是「连 N 子」,而中国象棋没有这个概念。
        // 把一个对将来的棋种无意义的字段放进对外契约,只会让客户端学着去读它。
        await Task.CompletedTask;

        typeof(Gewu.Application.Common.DTOs.GameDescriptorDto)
            .GetProperties()
            .Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                "GameKey", "IsRated", "SupportsHumanVsHuman", "Rows", "Cols",
            });
    }
}
