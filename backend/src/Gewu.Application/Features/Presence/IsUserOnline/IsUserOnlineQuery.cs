using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Presence.IsUserOnline;

/// <summary>
/// 查询指定用户当前是否在线(至少有一条活 SignalR 连接)。
/// 未知 UserId 返回 <c>IsOnline = false</c>,不抛 404 —— presence 端点是"在不在"二值,
/// 不区分"用户不存在"与"用户不在线"。
/// </summary>
public sealed record IsUserOnlineQuery(UserId UserId) : IRequest<PresenceDto>;
