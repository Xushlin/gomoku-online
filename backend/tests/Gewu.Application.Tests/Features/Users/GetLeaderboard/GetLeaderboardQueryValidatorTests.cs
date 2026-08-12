using Gomoku.Application.Features.Users.GetLeaderboard;

namespace Gomoku.Application.Tests.Features.Users.GetLeaderboard;

public class GetLeaderboardQueryValidatorTests
{
    private readonly GetLeaderboardQueryValidator _sut = new();

    [Fact]
    public void Valid_Defaults_Pass()
    {
        _sut.Validate(new GetLeaderboardQuery(1, 20)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Page_Zero_Fails()
    {
        var r = _sut.Validate(new GetLeaderboardQuery(0, 20));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(GetLeaderboardQuery.Page));
    }

    [Fact]
    public void PageSize_Zero_Fails()
    {
        var r = _sut.Validate(new GetLeaderboardQuery(1, 0));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(GetLeaderboardQuery.PageSize));
    }

    [Fact]
    public void PageSize_Over_100_Fails()
    {
        var r = _sut.Validate(new GetLeaderboardQuery(1, 101));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(GetLeaderboardQuery.PageSize));
    }

    [Fact]
    public void PageSize_Exactly_100_Passes()
    {
        _sut.Validate(new GetLeaderboardQuery(1, 100)).IsValid.Should().BeTrue();
    }
}
