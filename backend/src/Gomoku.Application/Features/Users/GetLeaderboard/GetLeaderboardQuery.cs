using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Users.GetLeaderboard;

/// <summary>
/// 查询排行榜。无入参 —— 本次固定返回前 100 条;分页 / 搜索 / 过滤留给后续变更。
/// </summary>
public sealed record GetLeaderboardQuery : IRequest<IReadOnlyList<LeaderboardEntryDto>>;
