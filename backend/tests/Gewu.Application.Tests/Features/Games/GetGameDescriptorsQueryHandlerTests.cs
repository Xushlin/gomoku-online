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
            // 座位数是棋种形状,直接投影 —— 不像 SupportsAi 要问另一份注册表。
            dto.SeatCount.Should().Be(rules.SeatCount);
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
    public async Task The_unrated_games_are_tictactoe_doudizhu_wakeng_and_xiangqi_endgame()
    {
        // 此前这条是"一字棋是唯一不计分的对战棋种",再之前是"一字棋与斗地主"。名单在变,而
        // **每一次变的都是理由,不是数量** —— 三个棋种不计分,理由有两种:
        //
        // - 一字棋:没有人人对战,唯一的对手是机器人,而机器人对局是计分的 —— 那种阶梯排出来的
        //   是"谁刷弱档刷得多"。这是一处**判断**。
        // - 斗地主 / 挖坑:ELO 是两人模型,而它们按分结算 —— 一个按分的阶梯是另一条榜。
        //   这是**结构性**的,而它也让 `IsRated ⇒ SeatCount == 2` 不需要开例外。
        //
        // 这条断言仍然在守同一件事:不计分那一侧非空,所以那几条"两类都要出现过"的遍历断言
        // 不会退化成单边空转。
        //
        // **这一条曾经是本变更里唯一没被预告的红灯**(挖坑那次):另外五条走查都在自己的注释里
        // 写了"挖坑落地那天这条会红",而这一条只是一份写死的名单。
        //
        // 而 `play-from-position` 里它是**被预告过**的 —— 那次提案专门写了一节说它会红,并且
        // 说明红了之后要回答什么。答案是**第三种理由**:
        //
        // - 象棋残局:**开局就不公平**。一则残局有一方按构造是赢定的,那是谱主设计它的方式;
        //   给这样的局面算 ELO 是在给一个已知结局的局面发分。这既不是"没有人人对战"(它有),
        //   也不是"按分结算"(它按将死结算) —— 所以它是一条新的、需要自己写下来的理由,
        //   而不是往名单里加一个名字。
        //
        // 这条理由同时由一条遍历注册表的断言守着(`A_game_that_starts_from_a_chosen_position_is_never_rated`),
        // 所以它不靠这份名单被记得。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        items.Where(i => !i.IsRated).Select(i => i.GameKey)
            .Should().BeEquivalentTo(
                [GameKeys.TicTacToe, GameKeys.Doudizhu, GameKeys.Wakeng, GameKeys.XiangqiEndgame]);
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
    public async Task The_seat_counts_cover_both_two_and_more_than_two()
    {
        // **上面那条遍历只断言 DTO 与规则一致,它在一个恒返回 2 的实现下也是绿的** ——
        // 因为它比的是 `rules.SeatCount`,而如果投影写死 2,那条比较会红……只有当注册表里
        // 真的存在一个座位数不是 2 的棋种时才会。所以这一条钉的是**样本**:
        // 两侧都得有,否则那条遍历是单边的。
        //
        // 这与 `enable-xiangqi-human-play` 记下的是同一条:一条走到空集合、或只走到同一类的
        // 遍历,会全绿地什么都不验。
        var items = await Build().Handle(new GetGameDescriptorsQuery(), default);

        var counts = items.Select(i => i.SeatCount).Distinct().ToList();
        counts.Should().Contain(2, "五子棋 / 一字棋 / 象棋 / 成语接龙都是两个座位");
        counts.Should().Contain(c => c > 2, "斗地主与挖坑是三个 —— 少了它这条遍历只走一边");
        items.Should().OnlyContain(i => i.SeatCount >= 2, "一个人下不了对战棋种");
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
                nameof(Gewu.Application.Common.DTOs.GameDescriptorDto.SeatCount),
                "GameKey", "IsRated", "SupportsHumanVsHuman", "Rows", "Cols",
            });
    }
}
