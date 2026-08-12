using Gomoku.Application.Features.Auth.ChangePassword;

namespace Gomoku.Application.Tests.Features.Auth.ChangePassword;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _sut = new();

    private static ChangePasswordCommand Cmd(string cur = "cur", string @new = "NewPass1")
        => new(UserId.NewId(), cur, @new);

    [Fact]
    public void Valid_Passes()
    {
        _sut.Validate(Cmd()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_Current_Fails()
    {
        var r = _sut.Validate(Cmd(cur: ""));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.CurrentPassword));
    }

    [Fact]
    public void Empty_New_Fails()
    {
        var r = _sut.Validate(Cmd(@new: ""));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }

    [Fact]
    public void New_Too_Short_Fails()
    {
        var r = _sut.Validate(Cmd(@new: "Ab12345")); // 7 chars
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void New_No_Letter_Fails()
    {
        var r = _sut.Validate(Cmd(@new: "12345678"));
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void New_No_Digit_Fails()
    {
        var r = _sut.Validate(Cmd(@new: "abcdefgh"));
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void New_Exactly_8_With_Letter_And_Digit_Passes()
    {
        _sut.Validate(Cmd(@new: "abcdefg1")).IsValid.Should().BeTrue();
    }
}
