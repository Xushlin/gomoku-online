using Gewu.Application.Features.Rooms.CreateAiRoom;
using Gewu.Application.Features.Rooms.CreateRoom;
using Gewu.Domain.Ai;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Users;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 建房路径上的棋种校验 —— 两条独立的规则。
/// <para>
/// **已登记**:两条建房路径都要。价值全在"聚合被构造之前就拦住":一个 <c>GameKey</c>
/// 无人认识的 <c>Room</c> 一旦落库,加入 / 落子 / 读状态全部解析不出规则,只能靠手工改数据修复。
/// </para>
/// <para>
/// **开放人人对战**:只有真人房那条要。这一条此前不存在,于是
/// <c>POST /api/rooms { gameKey: "xiangqi" }</c> 会返回 201 并开出一局真人象棋 —— 而同一个
/// API 的 <c>GET /api/games</c> 在那一刻正声明象棋 <c>supportsHumanVsHuman: false</c>。
/// </para>
/// </summary>
public class CreateRoomGameKeyValidationTests
{
    private static readonly CreateRoomCommandValidator Human = new(GomokuRules.Registry);
    private static readonly CreateAiRoomCommandValidator Ai =
        new(GomokuRules.Registry, GomokuRules.AiRegistry);

    /// <summary>本平台上不存在的棋种 —— 围棋不在七款规划之内。</summary>
    private const string NotOnThePlatform = "go";

    private static CreateRoomCommand HumanRoom(string gameKey)
        => new(UserId.NewId(), "a valid name", gameKey);

    private static CreateAiRoomCommand AiRoom(string gameKey)
        => new(UserId.NewId(), "a valid name", BotDifficulty.Easy, Stone.Black, gameKey);

    [Fact]
    public void A_registered_game_with_human_play_passes_on_both_paths()
    {
        Human.Validate(HumanRoom(GameKeys.Gomoku)).IsValid.Should().BeTrue();
        Ai.Validate(AiRoom(GameKeys.Gomoku)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(NotOnThePlatform)]
    [InlineData("Gomoku")] // 大小写敏感
    [InlineData("")]
    [InlineData("   ")]
    public void An_unregistered_game_key_fails_on_both_paths(string gameKey)
    {
        var human = Human.Validate(HumanRoom(gameKey));
        var ai = Ai.Validate(AiRoom(gameKey));

        human.IsValid.Should().BeFalse();
        human.Errors.Should().Contain(e => e.PropertyName == nameof(CreateRoomCommand.GameKey));

        ai.IsValid.Should().BeFalse();
        ai.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAiRoomCommand.GameKey));
    }

    [Theory]
    [InlineData(GameKeys.TicTacToe)]
    [InlineData(GameKeys.Xiangqi)]
    public void A_game_without_human_play_is_refused_a_human_room_but_allowed_an_ai_room(string gameKey)
    {
        var human = Human.Validate(HumanRoom(gameKey));

        human.IsValid.Should().BeFalse();
        human.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateRoomCommand.GameKey)
            && e.ErrorMessage.Contains("human-vs-human", StringComparison.Ordinal));

        // 人机正是这些棋种支持的玩法。在这条路径上也拦住,等于把它们逐出平台。
        Ai.Validate(AiRoom(gameKey)).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 判定遍历注册表本身,而不是一份写死的名单 —— 加第四款棋会自动被覆盖。
    /// <para>
    /// 这句话此前被写过一次并且是假的(数据源是手写的 <c>{ Gomoku, TicTacToe }</c>),
    /// 所以下面那条"两类都覆盖到了"的断言是本测试的一部分,不是装饰:一个走到空集合、
    /// 或只走到同一类的遍历,会全绿地什么都不验。
    /// </para>
    /// </summary>
    [Fact]
    public void The_verdict_tracks_the_capability_across_the_whole_registry()
    {
        var supporting = 0;
        var refusing = 0;

        foreach (var rules in GomokuRules.Registry.All)
        {
            var accepted = Human.Validate(HumanRoom(rules.GameKey)).IsValid;

            accepted.Should().Be(
                rules.SupportsHumanVsHuman,
                "'{0}' declares SupportsHumanVsHuman == {1}",
                rules.GameKey,
                rules.SupportsHumanVsHuman);

            if (rules.SupportsHumanVsHuman) supporting++;
            else refusing++;
        }

        supporting.Should().BeGreaterThan(0, "otherwise the loop never exercised the accept path");
        refusing.Should().BeGreaterThan(0, "otherwise the loop never exercised the refuse path");
    }

    [Fact]
    public void The_test_registry_is_the_one_production_registers()
    {
        // 夹具此前手写成两项,却在注释里自称与生产 DI 一致;象棋因此在整个
        // Gewu.Application.Tests 里都不存在。这条断言让那件事无法再悄悄发生。
        GomokuRules.Registry.All.Select(r => r.GameKey)
            .Should().BeEquivalentTo(BuiltInGameRules.All(GomokuRules.Lexicon).Select(r => r.GameKey));
    }

    [Fact]
    public void The_test_ai_registry_is_the_one_production_registers()
    {
        // 上面那条的对侧。它此前不存在,而 AI 夹具正以完全相同的方式漂着:手写两项、
        // 注释自称与生产一致、象棋 AI 自 add-xiangqi-ai 起就不在里面 —— 同一个文件,隔七行。
        // 上一次只修了规则那半,没回头看这半。**造出机制不等于采用机制。**
        BuiltInGameAis.All.Select(f => f.GameKey)
            .Should().OnlyHaveUniqueItems()
            .And.Contain(GameKeys.Xiangqi, "象棋 AI 从 add-xiangqi-ai 起就在生产 DI 里");

        foreach (var factory in BuiltInGameAis.All)
        {
            GomokuRules.AiRegistry.For(factory.GameKey).Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData(GameKeys.Gomoku)]
    [InlineData(GameKeys.TicTacToe)]
    [InlineData(GameKeys.Xiangqi)]
    public void A_game_with_an_ai_can_open_an_ai_room(string gameKey)
    {
        Ai.Validate(AiRoom(gameKey)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_game_without_an_ai_is_refused_an_ai_room_but_allowed_a_human_room()
    {
        // 成语接龙**故意**没有 AI:查词典就能写出近乎不可战胜的机器人,而机器人对局计分。
        // 在此之前这条没人拦:POST /api/rooms/ai 回 201,房间进 Playing 且轮到一个不存在的
        // 机器人,60 秒后超时判真人胜 —— 计分棋种,于是零手棋换一场胜利与约 +46 ELO。实测。
        var ai = Ai.Validate(AiRoom(GameKeys.IdiomChain));

        ai.IsValid.Should().BeFalse();
        ai.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAiRoomCommand.GameKey));

        // 人人对战正是这个棋种存在的理由。在那条路径上拦住等于把它逐出平台。
        Human.Validate(HumanRoom(GameKeys.IdiomChain)).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// AI 房的判定遍历 <see cref="IGameAiRegistry"/> 本身 —— 加第五款棋会自动被覆盖。
    /// <para>
    /// 这一条此前**根本不存在**,而它不存在的原因正是缺陷能活到今天的原因:在成语接龙之前,
    /// 每一个已登记棋种都有 AI。**一条从未遇到过反例的规则,与一条没人检查的规则,长得一模一样。**
    /// </para>
    /// </summary>
    [Fact]
    public void The_ai_verdict_tracks_the_ai_registry_across_the_whole_registry()
    {
        var withAi = 0;
        var withoutAi = 0;

        foreach (var rules in GomokuRules.Registry.All)
        {
            var hasAi = GomokuRules.AiRegistry.For(rules.GameKey) is not null;

            Ai.Validate(AiRoom(rules.GameKey)).IsValid.Should().Be(
                hasAi,
                "'{0}' {1} an AI factory",
                rules.GameKey,
                hasAi ? "has" : "has no");

            if (hasAi) withAi++;
            else withoutAi++;
        }

        withAi.Should().BeGreaterThan(0, "otherwise the loop never exercised the accept path");
        withoutAi.Should().BeGreaterThan(0, "otherwise the loop never exercised the refuse path");
    }

    [Fact]
    public void The_ai_verdict_comes_from_the_registry_not_a_hardcoded_list()
    {
        // 只登记五子棋 AI 的注册表里,一字棋的 AI 房必须被拒 —— 若 validator 内联了白名单,
        // 它会照样放行。
        var gomokuAiOnly = new CreateAiRoomCommandValidator(GomokuRules.Registry, GomokuRules.GomokuAiOnly);

        gomokuAiOnly.Validate(AiRoom(GameKeys.Gomoku)).IsValid.Should().BeTrue();
        gomokuAiOnly.Validate(AiRoom(GameKeys.TicTacToe)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_unregistered_key_reports_one_error_on_the_ai_path_too()
    {
        // 「没这个棋」与「这个棋没有 AI」在键解析不出来时是同一件事的两种说法。
        var result = Ai.Validate(AiRoom(NotOnThePlatform));

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateAiRoomCommand.GameKey));
    }

    [Fact]
    public void The_verdict_comes_from_the_registry_not_a_hardcoded_list()
    {
        // 只登记五子棋的注册表里,一字棋必须被拒 —— 若 validator 内联了白名单,
        // 它会照样放行,这条断言就是唯一能抓住那件事的地方。
        var gomokuOnly = new CreateRoomCommandValidator(GomokuRules.GomokuOnly);

        gomokuOnly.Validate(HumanRoom(GameKeys.Gomoku)).IsValid.Should().BeTrue();
        gomokuOnly.Validate(HumanRoom(GameKeys.TicTacToe)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_unregistered_key_reports_one_error_not_two()
    {
        // 解析不出规则时,"没有这个棋种"与"这个棋种不开人人对战"是同一件事的两种说法。
        // 都报出来只会让调用方以为要改两处。
        var result = Human.Validate(HumanRoom(NotOnThePlatform));

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateRoomCommand.GameKey));
    }

    [Fact]
    public void A_bad_game_key_does_not_mask_other_errors()
    {
        // 名字和棋种同时不合法时,两条错误都要报出来 —— 否则客户端改完一个才发现还有一个。
        var result = Human.Validate(new CreateRoomCommand(UserId.NewId(), "ab", NotOnThePlatform));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateRoomCommand.Name));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateRoomCommand.GameKey));
    }
}
