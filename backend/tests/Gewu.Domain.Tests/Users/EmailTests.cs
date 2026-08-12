namespace Gomoku.Domain.Tests.Users;

public class EmailTests
{
    [Theory]
    [InlineData("alice@example.com", "alice@example.com")]
    [InlineData("Alice@Example.COM", "alice@example.com")]
    [InlineData("ALICE@EXAMPLE.COM", "alice@example.com")]
    [InlineData("bob.smith+tag@sub.example.co.uk", "bob.smith+tag@sub.example.co.uk")]
    public void Valid_Email_Is_Lowercased(string input, string expected)
    {
        var email = new Email(input);
        email.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Null_Or_Whitespace_Throws(string? input)
    {
        var act = () => new Email(input!);
        act.Should().Throw<InvalidEmailException>();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@example.com")]
    [InlineData("alice@@example.com")]
    public void Invalid_Format_Throws(string input)
    {
        var act = () => new Email(input);
        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*invalid format*");
    }

    [Fact]
    public void Too_Long_Throws()
    {
        var local = new string('a', 250);
        var input = $"{local}@x.co";
        input.Length.Should().BeGreaterThan(254);

        var act = () => new Email(input);
        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*exceeds maximum length of 254*");
    }

    [Fact]
    public void Normalized_Equality()
    {
        var a = new Email("Alice@Example.com");
        var b = new Email("alice@EXAMPLE.COM");
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
