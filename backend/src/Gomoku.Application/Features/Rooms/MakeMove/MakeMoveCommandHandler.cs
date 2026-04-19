using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Gomoku.Domain.ValueObjects;
using MediatR;

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

    /// <inheritdoc />
    public MakeMoveCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<MoveDto> Handle(MakeMoveCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var outcome = room.PlayMove(request.UserId, new Position(request.Row, request.Col), _clock.UtcNow);

        if (outcome.Result != GameResult.Ongoing)
        {
            await ApplyEloAsync(room, outcome.Result, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        var moveDto = new MoveDto(
            outcome.Move.Ply,
            outcome.Move.Row,
            outcome.Move.Col,
            outcome.Move.Stone,
            outcome.Move.PlayedAt);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames);

        await _notifier.RoomStateChangedAsync(room.Id, state, cancellationToken);
        await _notifier.MoveMadeAsync(room.Id, moveDto, cancellationToken);

        if (outcome.Result != GameResult.Ongoing)
        {
            var ended = new GameEndedDto(
                outcome.Result,
                room.Game!.WinnerUserId?.Value,
                room.Game.EndedAt!.Value);
            await _notifier.GameEndedAsync(room.Id, ended, cancellationToken);
        }

        return moveDto;
    }

    private async Task ApplyEloAsync(Room room, GameResult result, CancellationToken cancellationToken)
    {
        var black = await _users.FindByIdAsync(room.BlackPlayerId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{room.BlackPlayerId.Value}' was not found.");
        var whiteId = room.WhitePlayerId!.Value;
        var white = await _users.FindByIdAsync(whiteId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{whiteId.Value}' was not found.");

        var outcomeForBlack = result switch
        {
            GameResult.BlackWin => GameOutcome.Win,
            GameResult.WhiteWin => GameOutcome.Loss,
            GameResult.Draw => GameOutcome.Draw,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unexpected GameResult for ELO."),
        };
        var outcomeForWhite = outcomeForBlack switch
        {
            GameOutcome.Win => GameOutcome.Loss,
            GameOutcome.Loss => GameOutcome.Win,
            _ => GameOutcome.Draw,
        };

        var (newBlackRating, newWhiteRating) = Gomoku.Domain.EloRating.EloRating.Calculate(
            black.Rating, black.GamesPlayed,
            white.Rating, white.GamesPlayed,
            outcomeForBlack);

        black.RecordGameResult(outcomeForBlack, newBlackRating);
        white.RecordGameResult(outcomeForWhite, newWhiteRating);
    }
}
