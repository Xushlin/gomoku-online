using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using Gomoku.Application.Features.Rooms.Common;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Gomoku.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Features.Rooms.MakeMove;

/// <summary>
/// 落子 handler。流程:加载聚合 → <c>Room.PlayMove</c> → SaveChanges(乐观并发) →
/// 推送 <c>RoomStateChanged</c> + <c>MoveMade</c>;若对局结束额外推 <c>GameEnded</c>。
/// 领域 / EF 异常不 catch,让全局中间件映射。
/// </summary>
public sealed class MakeMoveCommandHandler : IRequestHandler<MakeMoveCommand, MoveDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly GameOptions _gameOptions;

    /// <inheritdoc />
    public MakeMoveCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier,
        IOptions<GameOptions> gameOptions)
    {
        _rooms = rooms;
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

        var outcome = room.PlayMove(request.UserId, new Position(request.Row, request.Col), _clock.UtcNow);

        if (outcome.Result != GameResult.Ongoing)
        {
            await GameEloApplier.ApplyAsync(room, outcome.Result, _users, cancellationToken);
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
