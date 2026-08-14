using Gewu.Application.Features.Rooms.CreateAiRoom;
using Gewu.Application.Features.Rooms.CreateRoom;
using Gewu.Domain.Ai;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Users;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 建房路径上的棋种校验。
/// <para>
/// 这条规则的价值全在"聚合被构造之前就拦住":一个 <c>GameKey</c> 无人认识的 <c>Room</c>
/// 一旦落库,加入 / 落子 / 读状态全部解析不出规则,只能靠手工改数据修复。所以两条建房
/// 路径都必须挡,而不是只挡一条。
/// </para>
/// </summary>
public class CreateRoomGameKeyValidationTests
{
    private static readonly CreateRoomCommandValidator Human = new(GomokuRules.Registry);
    private static readonly CreateAiRoomCommandValidator Ai = new(GomokuRules.Registry);

    private static CreateRoomCommand HumanRoom(string gameKey)
        => new(UserId.NewId(), "a valid name", gameKey);

    private static CreateAiRoomCommand AiRoom(string gameKey)
        => new(UserId.NewId(), "a valid name", BotDifficulty.Easy, Stone.Black, gameKey);

    [Theory]
    [InlineData(GameKeys.Gomoku)]
    [InlineData(GameKeys.TicTacToe)]
    public void A_registered_game_key_passes_on_both_paths(string gameKey)
    {
        Human.Validate(HumanRoom(gameKey)).IsValid.Should().BeTrue();
        Ai.Validate(AiRoom(gameKey)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("xiangqi")]        // 规划中,尚未登记
    [InlineData("idiom-crossword")] // 是个游戏,但不是棋盘对抗棋种
    [InlineData("Gomoku")]          // 大小写敏感
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
    public void A_bad_game_key_does_not_mask_other_errors()
    {
        // 名字和棋种同时不合法时,两条错误都要报出来 —— 否则客户端改完一个才发现还有一个。
        var result = Human.Validate(new CreateRoomCommand(UserId.NewId(), "ab", "xiangqi"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateRoomCommand.Name));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateRoomCommand.GameKey));
    }
}
