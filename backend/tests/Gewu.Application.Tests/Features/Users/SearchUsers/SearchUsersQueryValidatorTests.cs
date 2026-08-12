using Gomoku.Application.Features.Users.SearchUsers;

namespace Gomoku.Application.Tests.Features.Users.SearchUsers;

public class SearchUsersQueryValidatorTests
{
    private readonly SearchUsersQueryValidator _sut = new();

    [Fact]
    public void Default_And_Null_Search_Pass()
    {
        _sut.Validate(new SearchUsersQuery(null, 1, 20)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_Search_Pass()
    {
        _sut.Validate(new SearchUsersQuery("", 1, 20)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Page_Zero_Fails()
    {
        var r = _sut.Validate(new SearchUsersQuery(null, 0, 20));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(SearchUsersQuery.Page));
    }

    [Fact]
    public void PageSize_Over_100_Fails()
    {
        var r = _sut.Validate(new SearchUsersQuery(null, 1, 101));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(SearchUsersQuery.PageSize));
    }

    [Fact]
    public void Search_Over_20_Chars_Fails()
    {
        var longSearch = new string('a', 21);
        var r = _sut.Validate(new SearchUsersQuery(longSearch, 1, 20));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(SearchUsersQuery.Search));
    }

    [Fact]
    public void Search_Exactly_20_Chars_Passes()
    {
        var maxSearch = new string('a', 20);
        _sut.Validate(new SearchUsersQuery(maxSearch, 1, 20)).IsValid.Should().BeTrue();
    }
}
