using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 内存版的 <c>UserGameStats</c> 账本,替代 <c>IUserRepository</c> 的 get-or-create 行为。
/// <para>
/// 除了"取到同一行"之外,它还让"**建了几行**"变成可断言的 —— 不计分棋种一行都不该建、
/// 未结束的局一行都不该建,这两条都是本变更的判据,而它们只有在能数行时才测得出来。
/// </para>
/// </summary>
internal sealed class FakeGameStats
{
    private readonly Dictionary<(Guid UserId, string GameKey), UserGameStats> _rows = new();

    /// <summary>账本里现有的行数。</summary>
    public int Count => _rows.Count;

    /// <summary>取或建,语义同仓储实现。</summary>
    public UserGameStats GetOrCreate(UserId userId, string gameKey)
    {
        var key = (userId.Value, gameKey);
        if (!_rows.TryGetValue(key, out var row))
        {
            row = UserGameStats.Start(userId, gameKey);
            _rows[key] = row;
        }
        return row;
    }

    /// <summary>只读查,没有返回 <c>null</c>。</summary>
    public UserGameStats? Find(UserId userId, string gameKey) =>
        _rows.TryGetValue((userId.Value, gameKey), out var row) ? row : null;

    /// <summary>只读查(按 User),没有返回 <c>null</c>。</summary>
    public UserGameStats? Find(User user, string gameKey = GameKeys.Gomoku) => Find(user.Id, gameKey);

    /// <summary>
    /// 取某人某棋种那行,断言它**应该**存在。缺行时抛一个说得清是谁的异常,
    /// 比 <c>KeyNotFoundException</c> 好读。
    /// </summary>
    public UserGameStats Of(User user, string gameKey = GameKeys.Gomoku) =>
        Find(user, gameKey)
        ?? throw new InvalidOperationException(
            $"Expected a UserGameStats row for '{user.Username.Value}' / '{gameKey}', but none was created.");

    /// <summary>预置一行既有战绩(模拟"这个人在这个棋种上已经下过若干局")。</summary>
    public UserGameStats Seed(User user, string gameKey, int rating, int gamesPlayed)
    {
        var row = GetOrCreate(user.Id, gameKey);
        for (var i = 0; i < gamesPlayed; i++)
        {
            row.RecordGameResult(GameOutcome.Draw, rating);
        }
        return row;
    }
}
