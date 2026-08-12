using Gomoku.Application.Abstractions;
using Gomoku.Domain.Users;

namespace Gomoku.Application.Common.Mapping;

/// <summary>
/// 按需把一组 <see cref="UserId"/> 解析为"Guid → Username"字典,供 <see cref="RoomMapping"/> 使用。
/// 针对房间这种小聚合(host + 2 players + 少量 spectators),简单的 N 次 <c>FindByIdAsync</c> 足够;
/// 未来规模变大再优化为仓储级批量查询。
/// </summary>
public static class UsernameLookup
{
    /// <summary>批量查询用户名;未知 id 不进 dict。</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> LookupUsernamesAsync(
        this IUserRepository users,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var dict = new Dictionary<Guid, string>();
        foreach (var id in ids.Distinct())
        {
            var u = await users.FindByIdAsync(new UserId(id), cancellationToken);
            if (u is not null)
            {
                dict[id] = u.Username.Value;
            }
        }
        return dict;
    }
}
