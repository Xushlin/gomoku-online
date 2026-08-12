namespace Gomoku.Application.Common.DTOs;

/// <summary>
/// 通用分页结果容器。<paramref name="Total"/> 是**过滤后**的总数,客户端用
/// <c>ceil(Total / PageSize)</c> 算总页数。未来 `add-leaderboard-pagination` 等
/// 其它分页端点可复用此 record。
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
