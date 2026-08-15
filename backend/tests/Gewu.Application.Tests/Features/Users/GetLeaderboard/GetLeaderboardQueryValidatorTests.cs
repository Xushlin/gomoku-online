using Gewu.Application.Features.Users.GetLeaderboard;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Users.GetLeaderboard;

public class GetLeaderboardQueryValidatorTests
{
    private readonly GetLeaderboardQueryValidator _sut = new();

    [Fact]
    public void Valid_Defaults_Pass()
    {
        _sut.Validate(new GetLeaderboardQuery(GameKeys.Gomoku, 1, 20)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Page_Zero_Fails()
    {
        var r = _sut.Validate(new GetLeaderboardQuery(GameKeys.Gomoku, 0, 20));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(GetLeaderboardQuery.Page));
    }

    [Fact]
    public void PageSize_Zero_Fails()
    {
        var r = _sut.Validate(new GetLeaderboardQuery(GameKeys.Gomoku, 1, 0));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(GetLeaderboardQuery.PageSize));
    }

    [Fact]
    public void PageSize_Over_100_Fails()
    {
        var r = _sut.Validate(new GetLeaderboardQuery(GameKeys.Gomoku, 1, 101));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(GetLeaderboardQuery.PageSize));
    }

    [Fact]
    public void PageSize_Exactly_100_Passes()
    {
        _sut.Validate(new GetLeaderboardQuery(GameKeys.Gomoku, 1, 100)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_GameKey_Fails(string gameKey)
    {
        var r = _sut.Validate(new GetLeaderboardQuery(gameKey, 1, 20));
        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(GetLeaderboardQuery.GameKey));
    }

    [Fact]
    public void An_Unregistered_GameKey_Passes_Validation()
    {
        // **刻意不校验是否已登记。** 未登记的棋种该返回空榜而不是 400 —— 与房间列表同一处理。
        // 在校验器里塞一份"哪些棋种存在"的清单,还会造出注册表之外的第二份真源。
        _sut.Validate(new GetLeaderboardQuery("a-game-nobody-registered", 1, 20))
            .IsValid.Should().BeTrue();
    }
}
