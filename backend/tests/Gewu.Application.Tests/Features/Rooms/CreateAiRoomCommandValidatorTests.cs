using Gomoku.Application.Features.Rooms.CreateAiRoom;
using Gomoku.Domain.Enums;

namespace Gomoku.Application.Tests.Features.Rooms;

public class CreateAiRoomCommandValidatorTests
{
    private readonly CreateAiRoomCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]                                // 太短
    [InlineData("this-name-is-way-way-way-way-way-too-long-definitely-over-50-chars")] // 超 50
    public void Invalid_Name_Fails(string name)
    {
        var result = _validator.Validate(
            new CreateAiRoomCommand(UserId.NewId(), name, BotDifficulty.Easy, Stone.Black));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAiRoomCommand.Name));
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("three")]
    [InlineData("a valid 50-char room name padded with...some text")] // ~50
    public void Valid_Name_Passes_If_Length_OK(string name)
    {
        var result = _validator.Validate(
            new CreateAiRoomCommand(UserId.NewId(), name, BotDifficulty.Medium, Stone.Black));

        // 先计算 trim 后长度判断真假;短于 3 或超 50 的字符串应失败。
        var trimmedLen = name.Trim().Length;
        var expectedValid = trimmedLen >= 3 && trimmedLen <= 50;
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(Stone.Black)]
    [InlineData(Stone.White)]
    public void Valid_HumanSide_Passes(Stone side)
    {
        var result = _validator.Validate(
            new CreateAiRoomCommand(UserId.NewId(), "ok name", BotDifficulty.Easy, side));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_HumanSide_Fails()
    {
        var result = _validator.Validate(
            new CreateAiRoomCommand(UserId.NewId(), "ok name", BotDifficulty.Easy, Stone.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAiRoomCommand.HumanSide));
    }
}
