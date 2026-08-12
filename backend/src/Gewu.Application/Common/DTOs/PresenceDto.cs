namespace Gomoku.Application.Common.DTOs;

/// <summary>`GET /api/presence/online-count` 端点返回 payload:当前在线用户数。</summary>
public sealed record OnlineCountDto(int Count);

/// <summary>`GET /api/presence/users/{id}` 端点返回 payload:指定用户是否在线。未知 UserId 仍返回 IsOnline=false(不 404)。</summary>
public sealed record PresenceDto(Guid UserId, bool IsOnline);
