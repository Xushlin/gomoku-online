using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Presence.IsUserOnline;

/// <summary>调 <see cref="IConnectionTracker.IsUserOnline"/> 组装 <see cref="PresenceDto"/>。</summary>
public sealed class IsUserOnlineQueryHandler : IRequestHandler<IsUserOnlineQuery, PresenceDto>
{
    private readonly IConnectionTracker _tracker;

    /// <inheritdoc />
    public IsUserOnlineQueryHandler(IConnectionTracker tracker)
    {
        _tracker = tracker;
    }

    /// <inheritdoc />
    public Task<PresenceDto> Handle(IsUserOnlineQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new PresenceDto(request.UserId.Value, _tracker.IsUserOnline(request.UserId)));
}
