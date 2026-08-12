using Gomoku.Application.Features.Users.GetUserGames;

namespace Gomoku.Application.Tests.Features.Users.GetUserGames;

public class GetUserGamesPagedQueryValidatorTests
{
    private readonly GetUserGamesPagedQueryValidator _sut = new();

    [Fact]
    public void Valid_Defaults_Pass()
    {
        var q = new GetUserGamesPagedQuery(UserId.NewId(), Page: 1, PageSize: 20);
        var r = _sut.Validate(q);
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Zero_Page_Fails()
    {
        var q = new GetUserGamesPagedQuery(UserId.NewId(), Page: 0, PageSize: 20);
        var r = _sut.Validate(q);
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(q.Page));
    }

    [Fact]
    public void Zero_PageSize_Fails()
    {
        var q = new GetUserGamesPagedQuery(UserId.NewId(), Page: 1, PageSize: 0);
        var r = _sut.Validate(q);
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(q.PageSize));
    }

    [Fact]
    public void PageSize_Over_100_Fails()
    {
        var q = new GetUserGamesPagedQuery(UserId.NewId(), Page: 1, PageSize: 101);
        var r = _sut.Validate(q);
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(q.PageSize));
    }

    [Fact]
    public void PageSize_Exactly_100_Passes()
    {
        var q = new GetUserGamesPagedQuery(UserId.NewId(), Page: 1, PageSize: 100);
        var r = _sut.Validate(q);
        r.IsValid.Should().BeTrue();
    }
}
