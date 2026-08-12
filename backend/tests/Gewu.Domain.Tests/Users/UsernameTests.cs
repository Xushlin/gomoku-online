namespace Gomoku.Domain.Tests.Users;

public class UsernameTests
{
    [Theory]
    [InlineData("alice")]
    [InlineData("Bob_2")]
    [InlineData("小明明")]
    [InlineData("玩家123")]
    [InlineData("a_b_c")]
    public void Valid_Names_Succeed(string input)
    {
        var username = new Username(input);
        username.Value.Should().Be(input);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("123456789012345678901")]
    public void Length_Out_Of_Range_Throws(string input)
    {
        var act = () => new Username(input);
        act.Should().Throw<InvalidUsernameException>()
            .WithMessage("*out of range*");
    }

    [Theory]
    [InlineData("alice bob")]
    [InlineData("bad-name")]
    [InlineData("🐱user")]
    [InlineData("邮件@x")]
    public void Disallowed_Characters_Throw(string input)
    {
        var act = () => new Username(input);
        act.Should().Throw<InvalidUsernameException>()
            .WithMessage("*disallowed characters*");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("00000")]
    public void All_Digits_Throws(string input)
    {
        var act = () => new Username(input);
        act.Should().Throw<InvalidUsernameException>()
            .WithMessage("*digits only*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_Or_Whitespace_Throws(string? input)
    {
        var act = () => new Username(input!);
        act.Should().Throw<InvalidUsernameException>();
    }

    [Fact]
    public void Case_Insensitive_Equality_Preserves_Original_Casing()
    {
        var a = new Username("Alice");
        var b = new Username("ALICE");
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Value.Should().Be("Alice");
        b.Value.Should().Be("ALICE");
    }

    [Fact]
    public void Chinese_Characters_Counted_By_Length()
    {
        var name = new Username("中文用户名");
        name.Value.Should().Be("中文用户名");
    }

    [Fact]
    public void Two_Chinese_Characters_Below_Minimum_Throws()
    {
        var act = () => new Username("小明");
        act.Should().Throw<InvalidUsernameException>()
            .WithMessage("*out of range*");
    }
}
