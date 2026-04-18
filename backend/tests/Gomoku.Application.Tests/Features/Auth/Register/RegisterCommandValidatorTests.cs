using Gomoku.Application.Features.Auth.Register;

namespace Gomoku.Application.Tests.Features.Auth.Register;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _sut = new();

    [Fact]
    public void Valid_Input_Passes()
    {
        var result = _sut.Validate(new RegisterCommand("alice@example.com", "Alice", "Password1"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Email_Fails(string email)
    {
        var result = _sut.Validate(new RegisterCommand(email, "Alice", "Password1"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Long_Email_Fails()
    {
        var longLocal = new string('a', 250);
        var email = $"{longLocal}@example.com";
        var result = _sut.Validate(new RegisterCommand(email, "Alice", "Password1"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage.Contains("254"));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a_very_long_username_over_20")]
    public void Username_Length_Fails(string username)
    {
        var result = _sut.Validate(new RegisterCommand("alice@example.com", username, "Password1"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("Abc1234")]
    public void Password_Too_Short_Fails(string password)
    {
        var result = _sut.Validate(new RegisterCommand("alice@example.com", "Alice", password));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("8"));
    }

    [Fact]
    public void Password_Without_Letter_Fails()
    {
        var result = _sut.Validate(new RegisterCommand("alice@example.com", "Alice", "12345678"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("letter"));
    }

    [Fact]
    public void Password_Without_Digit_Fails()
    {
        var result = _sut.Validate(new RegisterCommand("alice@example.com", "Alice", "password"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password" && e.ErrorMessage.Contains("digit"));
    }
}
