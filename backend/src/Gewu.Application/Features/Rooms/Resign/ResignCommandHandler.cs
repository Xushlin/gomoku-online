using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Application.Features.Rooms.Common;
using MediatR;
using Microsoft.Extensions.Options;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Features.Rooms.Resign;

/// <summary>
/// 玩家认输 handler。流程:
/// Load → <c>Room.Resign</c>(Domain 校验身份 + 状态)→ 共享 ELO helper → SaveChanges →
/// 广播 <c>RoomStateChanged</c> + <c>GameEnded</c>(**不**发 MoveMade;没有新 Move)。
/// Domain / 应用异常由全局中间件映射。
/// </summary>
public sealed class ResignCommandHandler : IRequestHandler<ResignCommand, GameEndedDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IGameRulesRegistry _rules;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly GameOptions _gameOptions;

    /// <inheritdoc />
    public ResignCommandHandler(
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
    public async Task<GameEndedDto> Handle(ResignCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var outcome = room.Resign(request.UserId, _clock.UtcNow);

        await GameEloApplier.ApplyAsync(room, outcome.Result, _rules, _users, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var ended = new GameEndedDto(
            outcome.Result,
            outcome.WinnerUserId?.Value,
            room.Game!.EndedAt!.Value,
            room.Game.EndReason!.Value);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames, _gameOptions.TurnTimeoutSeconds);

        await _notifier.RoomStateChangedAsync(room.Id, state, cancellationToken);
        await _notifier.GameEndedAsync(room.Id, ended, cancellationToken);

        return ended;
    }
}
