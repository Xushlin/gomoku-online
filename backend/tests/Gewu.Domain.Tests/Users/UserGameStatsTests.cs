using Gewu.Domain.Games.Abstractions;

namespace Gewu.Domain.Tests.Users;

/// <summary>
/// <c>UserGameStats</c> —— 战绩与 Rating 的唯一真源。这些用例此前叫 <c>UserRecordGameResultTests</c>
/// 并跑在 <c>User</c> 上;方法搬家后断言的对象跟着换成了"某人在某棋种上的那一行"。
/// </summary>
public class UserGameStatsTests
{
    private static UserGameStats NewStats(string gameKey = GameKeys.Gomoku) =>
        UserGameStats.Start(UserId.NewId(), gameKey);

    [Fact]
    public void Start_Yields_Initial_Rating_And_Zeroed_Counters()
    {
        var userId = UserId.NewId();

        var stats = UserGameStats.Start(userId, GameKeys.Gomoku);

        stats.UserId.Should().Be(userId);
        stats.GameKey.Should().Be(GameKeys.Gomoku);
        stats.Rating.Should().Be(1200);
        stats.GamesPlayed.Should().Be(0);
        stats.Wins.Should().Be(0);
        stats.Losses.Should().Be(0);
        stats.Draws.Should().Be(0);
        stats.RowVersion.Should().NotBeNull();
        stats.RowVersion.Length.Should().Be(16);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_Rejects_Blank_GameKey(string gameKey)
    {
        var act = () => UserGameStats.Start(UserId.NewId(), gameKey);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Win_Increments_GamesPlayed_And_Wins_And_Sets_Rating()
    {
        var stats = NewStats();

        stats.RecordGameResult(GameOutcome.Win, 1216);

        stats.GamesPlayed.Should().Be(1);
        stats.Wins.Should().Be(1);
        stats.Losses.Should().Be(0);
        stats.Draws.Should().Be(0);
        stats.Rating.Should().Be(1216);
    }

    [Fact]
    public void Loss_Increments_GamesPlayed_And_Losses_And_Sets_Rating()
    {
        var stats = NewStats();

        stats.RecordGameResult(GameOutcome.Loss, 1184);

        stats.GamesPlayed.Should().Be(1);
        stats.Wins.Should().Be(0);
        stats.Losses.Should().Be(1);
        stats.Draws.Should().Be(0);
        stats.Rating.Should().Be(1184);
    }

    [Fact]
    public void Draw_Increments_GamesPlayed_And_Draws_And_Sets_Rating()
    {
        var stats = NewStats();

        stats.RecordGameResult(GameOutcome.Draw, 1200);

        stats.GamesPlayed.Should().Be(1);
        stats.Wins.Should().Be(0);
        stats.Losses.Should().Be(0);
        stats.Draws.Should().Be(1);
        stats.Rating.Should().Be(1200);
    }

    [Fact]
    public void Multiple_Results_Keep_Counters_In_Sync_With_GamesPlayed()
    {
        var stats = NewStats();

        stats.RecordGameResult(GameOutcome.Win, 1216);
        stats.RecordGameResult(GameOutcome.Loss, 1200);
        stats.RecordGameResult(GameOutcome.Draw, 1200);

        stats.GamesPlayed.Should().Be(3);
        stats.Wins.Should().Be(1);
        stats.Losses.Should().Be(1);
        stats.Draws.Should().Be(1);
        stats.Rating.Should().Be(1200);
        (stats.Wins + stats.Losses + stats.Draws).Should().Be(stats.GamesPlayed);
    }

    [Fact]
    public void Unknown_Outcome_Throws_And_Preserves_State()
    {
        var stats = NewStats();
        var before = (byte[])stats.RowVersion.Clone();

        var act = () => stats.RecordGameResult((GameOutcome)99, 9999);

        act.Should().Throw<ArgumentOutOfRangeException>();
        stats.GamesPlayed.Should().Be(0);
        stats.Wins.Should().Be(0);
        stats.Losses.Should().Be(0);
        stats.Draws.Should().Be(0);
        stats.Rating.Should().Be(1200);
        stats.RowVersion.Should().Equal(before);
    }

    [Fact]
    public void Invariant_Holds_After_Many_Mixed_Results()
    {
        var stats = NewStats();
        var sequence = new[]
        {
            GameOutcome.Win, GameOutcome.Win, GameOutcome.Loss, GameOutcome.Draw,
            GameOutcome.Win, GameOutcome.Loss, GameOutcome.Draw, GameOutcome.Loss,
        };

        foreach (var outcome in sequence)
        {
            stats.RecordGameResult(outcome, stats.Rating);
        }

        stats.GamesPlayed.Should().Be(sequence.Length);
        (stats.Wins + stats.Losses + stats.Draws).Should().Be(stats.GamesPlayed);
    }

    [Fact]
    public void RecordGameResult_Advances_RowVersion()
    {
        var stats = NewStats();
        var before = (byte[])stats.RowVersion.Clone();

        stats.RecordGameResult(GameOutcome.Win, 1220);

        stats.RowVersion.Should().NotEqual(before);
    }

    [Fact]
    public void Three_Successive_Records_Yield_Three_Distinct_RowVersions()
    {
        var stats = NewStats();
        var versions = new List<byte[]>();

        stats.RecordGameResult(GameOutcome.Win, 1220);
        versions.Add((byte[])stats.RowVersion.Clone());

        stats.RecordGameResult(GameOutcome.Loss, 1204);
        versions.Add((byte[])stats.RowVersion.Clone());

        stats.RecordGameResult(GameOutcome.Draw, 1204);
        versions.Add((byte[])stats.RowVersion.Clone());

        versions[0].Should().NotEqual(versions[1]);
        versions[1].Should().NotEqual(versions[2]);
        versions[0].Should().NotEqual(versions[2]);
    }

    [Fact]
    public void Two_Game_Keys_For_The_Same_Player_Do_Not_Touch_Each_Other()
    {
        // 这是整个变更的要点:同一个人的两个棋种是两行。写一行 MUST NOT 影响另一行。
        var userId = UserId.NewId();
        var gomoku = UserGameStats.Start(userId, GameKeys.Gomoku);
        var xiangqi = UserGameStats.Start(userId, "xiangqi");
        var xiangqiVersionBefore = (byte[])xiangqi.RowVersion.Clone();

        gomoku.RecordGameResult(GameOutcome.Win, 1500);

        xiangqi.Rating.Should().Be(1200);
        xiangqi.GamesPlayed.Should().Be(0);
        xiangqi.Wins.Should().Be(0);
        xiangqi.RowVersion.Should().Equal(xiangqiVersionBefore);
    }

    [Fact]
    public void User_No_Longer_Carries_Any_Of_The_Five_Stat_Properties()
    {
        // 判据式断言:镜像字段是第二份真源,它与这里漂移之后的症状是排行榜和资料页显示不同的分,
        // 且没有任何东西会拦住。所以"User 上不许有这些属性"值得由测试盯着,而不是靠记得。
        var names = typeof(User)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        names.Should().NotContain(new[] { "Rating", "GamesPlayed", "Wins", "Losses", "Draws" });
    }
}
