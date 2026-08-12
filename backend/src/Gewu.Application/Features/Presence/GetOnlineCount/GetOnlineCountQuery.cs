using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Presence.GetOnlineCount;

/// <summary>查询当前在线用户数(去重计数,同一用户多连接算一个)。</summary>
public sealed record GetOnlineCountQuery() : IRequest<OnlineCountDto>;
