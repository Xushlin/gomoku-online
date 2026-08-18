using FluentAssertions;
using Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;

namespace Gewu.Application.Tests.Features.ScoreRuns;

/// <summary>
/// 窗口 → 起始时刻。纯函数,所以自然周这条规则不碰数据库就能测 —— 而它值得单独测,
/// 因为「自然周」与「滚动 7 天」在**大多数**日子上给出的榜是一样的,
/// 只有周一附近才分叉。一条挑在周三的用例两种实现都过。
/// </summary>
public sealed class ScoreWindowTests
{
    // 2026-08-19 是周三。
    private static readonly DateTime Wednesday = new(2026, 8, 19, 14, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime ThisMonday = new(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_week_starts_on_monday_midnight_utc()
    {
        ScoreWindows.StartOf(ScoreWindow.Week, Wednesday).Should().Be(ThisMonday);
    }

    [Fact]
    public void On_monday_the_week_starts_today_not_a_week_ago()
    {
        var mondayMorning = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);

        ScoreWindows.StartOf(ScoreWindow.Week, mondayMorning).Should().Be(ThisMonday);
    }

    [Fact]
    public void On_sunday_the_week_still_starts_on_the_monday_six_days_back()
    {
        // 这条钉住 DayOfWeek 的偏移:周日是 0,而 (int)DayOfWeek - 1 会把周日整天甩到上一周。
        var sunday = new DateTime(2026, 8, 23, 23, 0, 0, DateTimeKind.Utc);

        ScoreWindows.StartOf(ScoreWindow.Week, sunday).Should().Be(ThisMonday);
    }

    [Fact]
    public void The_week_is_not_a_rolling_seven_days()
    {
        // 周三减 7 天是上周三 —— 一个滚动窗口的实现会返回它。两者必须不同,
        // 否则这一整组用例都可能在验一个滚动实现。
        ScoreWindows.StartOf(ScoreWindow.Week, Wednesday)
            .Should().NotBe(Wednesday.AddDays(-7));
    }

    [Fact]
    public void The_month_starts_on_the_first_at_midnight_utc()
    {
        ScoreWindows.StartOf(ScoreWindow.Month, Wednesday)
            .Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void All_has_no_start()
    {
        ScoreWindows.StartOf(ScoreWindow.All, Wednesday).Should().BeNull();
    }

    [Fact]
    public void An_undefined_window_throws_instead_of_meaning_all()
    {
        // 兜底成 all 会让一个打错的窗口静静返回全部历史。这条钉住"大声失败"这个选择,
        // 因为它是真正的防线 —— 校验只负责把这个失败变成一个带字段名的 400。
        var act = () => ScoreWindows.StartOf((ScoreWindow)99, Wednesday);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(ScoreWindow.Week)]
    [InlineData(ScoreWindow.Month)]
    public void Every_boundary_is_utc(ScoreWindow window)
    {
        // 本地时区会让同一个榜在不同部署下切在不同时刻 —— 一个没人会想到去查的差异。
        ScoreWindows.StartOf(window, Wednesday)!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
