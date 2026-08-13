using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Presence.IsUserOnline;

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
