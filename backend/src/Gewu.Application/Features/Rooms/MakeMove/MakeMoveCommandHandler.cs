using Gewu.Application.Abstractions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Application.Features.Rooms.Common;
using Gewu.Domain.Enums;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gewu.Application.Features.Rooms.MakeMove;

/// <summary>
/// 落子 handler。流程:加载聚合 → <c>Room.PlayMove</c> → SaveChanges(乐观并发) →
/// 推送 <c>RoomStateChanged</c> + <c>MoveMade</c>;若对局结束额外推 <c>GameEnded</c>。
/// 领域 / EF 异常不 catch,让全局中间件映射。
/// </summary>
public sealed class MakeMoveCommandHandler : IRequestHandler<MakeMoveCommand, MoveDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IGameRulesRegistry _rules;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly GameOptions _gameOptions;

    /// <inheritdoc />
    public MakeMoveCommandHandler(
        IRoomRepository rooms,
        IGameRulesRegistry rules,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier,
        IOptions<GameOptions> gameOptions)
    {
        _rooms = rooms;
        _rules = rules;
        _users = users;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
        _gameOptions = gameOptions.Value;
    }

    /// <inheritdoc />
    public async Task<MoveDto> Handle(MakeMoveCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var rules = _rules.For(room.GameKey)
            ?? throw new RoomNotFoundException(
                $"Room '{room.Id.Value}' declares unknown game '{room.GameKey}'.");

        var outcome = room.PlayMove(
            request.UserId, new Position(request.Row, request.Col), _clock.UtcNow, rules);

        if (outcome.Result != GameResult.Ongoing)
        {
            await GameEloApplier.ApplyAsync(room, outcome.Result, _rules, _users, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        var moveDto = new MoveDto(
            outcome.Move.Ply,
            outcome.Move.Row,
            outcome.Move.Col,
            outcome.Move.Stone,
            outcome.Move.PlayedAt);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames, _gameOptions.TurnTimeoutSeconds);

        await _notifier.RoomStateChangedAsync(room.Id, state, cancellationToken);
        await _notifier.MoveMadeAsync(room.Id, moveDto, cancellationToken);

        if (outcome.Result != GameResult.Ongoing)
        {
            var ended = new GameEndedDto(
                outcome.Result,
                room.Game!.WinnerUserId?.Value,
                room.Game.EndedAt!.Value,
                room.Game.EndReason!.Value);
            await _notifier.GameEndedAsync(room.Id, ended, cancellationToken);
        }

        return moveDto;
    }
}
