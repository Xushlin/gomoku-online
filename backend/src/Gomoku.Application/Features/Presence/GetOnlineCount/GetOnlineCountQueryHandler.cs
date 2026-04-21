using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Presence.GetOnlineCount;

/// <summary>调 <see cref="IConnectionTracker.GetOnlineUserCount"/> 返回当前在线人数。</summary>
public sealed class GetOnlineCountQueryHandler : IRequestHandler<GetOnlineCountQuery, OnlineCountDto>
{
    private readonly IConnectionTracker _tracker;

    /// <inheritdoc />
    public GetOnlineCountQueryHandler(IConnectionTracker tracker)
    {
        _tracker = tracker;
    }

    /// <inheritdoc />
    public Task<OnlineCountDto> Handle(GetOnlineCountQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new OnlineCountDto(_tracker.GetOnlineUserCount()));
}
