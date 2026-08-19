using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Application.Features.Rooms.Common;
using MediatR;
using Microsoft.Extensions.Options;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Features.Rooms.TurnTimeout;

/// <summary>
/// 超时判负 handler。流程与 <c>ResignCommandHandler</c> 对称,区别仅在 Domain 方法:
/// <c>Room.TimeOutCurrentTurn</c>(Domain 自己防竞态 —— 若对手刚落子推新了 lastActivity,
/// 则抛 <see cref="Gewu.Domain.Exceptions.TurnNotTimedOutException"/>)。Worker 的 try/catch 吞之。
/// </summary>
public sealed class TurnTimeoutCommandHandler : IRequestHandler<TurnTimeoutCommand, Unit>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IGameRulesRegistry _rules;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly GameOptions _gameOptions;

    /// <inheritdoc />
    public TurnTimeoutCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IGameRulesRegistry rules,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier,
        IOptions<GameOptions> gameOptions)
    {
        _rooms = rooms;
        _users = users;
        _rules = rules;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
        _gameOptions = gameOptions.Value;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(TurnTimeoutCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var outcome = room.TimeOutCurrentTurn(_clock.UtcNow, _gameOptions.TurnTimeoutSeconds);

        await GameEloApplier.ApplyAsync(room, _rules, _users, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var ended = new GameEndedDto(
            outcome.Result,
            outcome.WinnerUserId?.Value,
            room.Game!.EndedAt!.Value,
            room.Game.EndReason!.Value);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);

        await _notifier.RoomStateChangedAsync(room, usernames, _gameOptions.TurnTimeoutSeconds, cancellationToken);
        await _notifier.GameEndedAsync(room.Id, ended, cancellationToken);

        return Unit.Value;
    }
}
