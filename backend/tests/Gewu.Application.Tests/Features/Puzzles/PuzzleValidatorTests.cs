using Gewu.Application.Features.Puzzles.CheckPuzzlePartial;
using Gewu.Application.Features.Puzzles.StartPuzzleAttempt;
using Gewu.Application.Features.Puzzles.SubmitPuzzleAttempt;
using Gewu.Domain.Users;

namespace Gewu.Application.Tests.Features.Puzzles;

/// <summary>
/// puzzle-core 三个带载荷命令的入参校验。
/// <para>
/// 这些校验器刻意**不解读** JSON 结构 —— 内容对平台不透明,懂它的是各游戏的
/// <c>IPuzzleRules</c>。所以这里只测"空被拦、超长被拦、正常放过",不测语义。
/// </para>
/// </summary>
public class PuzzleValidatorTests
{
    private static readonly UserId Caller = new(Guid.NewGuid());

    // ---- StartPuzzleAttempt ----

    [Fact]
    public void Start_accepts_a_well_formed_command()
    {
        var result = new StartPuzzleAttemptCommandValidator()
            .Validate(new StartPuzzleAttemptCommand(Caller, "idiom-crossword", 0));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_rejects_a_blank_game_key(string gameKey)
    {
        var result = new StartPuzzleAttemptCommandValidator()
            .Validate(new StartPuzzleAttemptCommand(Caller, gameKey, 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Start_rejects_a_negative_level_index()
    {
        var result = new StartPuzzleAttemptCommandValidator()
            .Validate(new StartPuzzleAttemptCommand(Caller, "idiom-crossword", -1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Start_does_not_reject_an_unknown_game_key()
    {
        // "这个游戏不存在" 是注册表的判断,结果是 404;校验器只管参数格式,
        // 否则同一个错会有 400 和 404 两种表现。
        var result = new StartPuzzleAttemptCommandValidator()
            .Validate(new StartPuzzleAttemptCommand(Caller, "no-such-game", 0));

        result.IsValid.Should().BeTrue();
    }

    // ---- CheckPuzzlePartial ----

    [Fact]
    public void Check_accepts_a_well_formed_command()
    {
        var result = new CheckPuzzlePartialCommandValidator()
            .Validate(new CheckPuzzlePartialCommand(Caller, Guid.NewGuid(), "{\"word\":\"一心一意\"}"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Check_rejects_an_empty_attempt_id()
    {
        var result = new CheckPuzzlePartialCommandValidator()
            .Validate(new CheckPuzzlePartialCommand(Caller, Guid.Empty, "{}"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Check_rejects_an_empty_payload()
    {
        var result = new CheckPuzzlePartialCommandValidator()
            .Validate(new CheckPuzzlePartialCommand(Caller, Guid.NewGuid(), ""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Check_rejects_an_absurdly_large_payload()
    {
        var oversized = new string('x', CheckPuzzlePartialCommandValidator.MaxPayloadLength + 1);

        var result = new CheckPuzzlePartialCommandValidator()
            .Validate(new CheckPuzzlePartialCommand(Caller, Guid.NewGuid(), oversized));

        result.IsValid.Should().BeFalse();
    }

    // ---- SubmitPuzzleAttempt ----

    [Fact]
    public void Submit_accepts_a_well_formed_command()
    {
        var result = new SubmitPuzzleAttemptCommandValidator()
            .Validate(new SubmitPuzzleAttemptCommand(Caller, Guid.NewGuid(), "{\"grid\":[]}"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Submit_rejects_an_empty_payload()
    {
        var result = new SubmitPuzzleAttemptCommandValidator()
            .Validate(new SubmitPuzzleAttemptCommand(Caller, Guid.NewGuid(), "   "));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Submit_allows_a_larger_payload_than_check()
    {
        // 完整答案自然比一条成语大,所以上限更宽 —— 这里锁住这个关系,
        // 免得有人把两个常量改成一样的。
        SubmitPuzzleAttemptCommandValidator.MaxPayloadLength
            .Should().BeGreaterThan(CheckPuzzlePartialCommandValidator.MaxPayloadLength);
    }
}
