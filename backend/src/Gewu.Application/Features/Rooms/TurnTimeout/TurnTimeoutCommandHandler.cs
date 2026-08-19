using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Application.Features.Rooms.Common;
using MediatR;
using Microsoft.Extensions.Options;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Features.Rooms.TurnTimeout;

/// <summary>
/// 超时 handler。Domain 自己防竞态 —— 若对手刚落子推新了 lastActivity,则抛
/// <see cref="Gewu.Domain.Exceptions.TurnNotTimedOutException"/>,Worker 的 try/catch 吞之。
/// <para>
/// **超时有两种结果**,而调用方要做的事不同:没有兜底的棋种判他负(广播 <c>GameEnded</c>),
/// 有兜底的棋种替他走一步(广播 <c>MoveMade</c>,与真人落子的广播序列完全相同)。
/// 后者的广播序列刻意与 <c>MakeMoveCommandHandler</c> 一致:客户端不需要区分"他走的"与
/// "系统替他走的"。
/// </para>
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

        // 未知棋种的处理与落子路径一致:那是一条损坏的房间记录,不是一次非法超时。
        var rules = _rules.For(room.GameKey)
            ?? throw new RoomNotFoundException(
                $"Room '{room.Id.Value}' declares unknown game '{room.GameKey}'.");

        var outcome = room.TimeOutCurrentTurn(
            _clock.UtcNow, _gameOptions.TurnTimeoutSeconds, rules);

        // 一步棋不结束对局就不动评分 —— 这与 MakeMoveCommandHandler 是同一条,不是新规则。
        var gameOver = outcome.Ended is not null
            || outcome.Move!.Result != GameResult.Ongoing;
        if (gameOver)
        {
            await GameEloApplier.ApplyAsync(room, _rules, _users, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);

        await _notifier.RoomStateChangedAsync(
            room, usernames, _gameOptions.TurnTimeoutSeconds, cancellationToken);

        if (outcome.Move is { } played)
        {
            var moveDto = new MoveDto(
                played.Move.Ply,
                played.Move.Row,
                played.Move.Col,
                played.Move.Seat,
                played.Move.PlayedAt,
                played.Move.FromRow,
                played.Move.FromCol,
                played.Move.Text);
            await _notifier.MoveMadeAsync(room.Id, moveDto, cancellationToken);
        }

        if (gameOver)
        {
            var ended = new GameEndedDto(
                room.Game!.Result!.Value,
                room.Game.WinnerUserId?.Value,
                room.Game.EndedAt!.Value,
                room.Game.EndReason!.Value);
            await _notifier.GameEndedAsync(room.Id, ended, cancellationToken);
        }

        return Unit.Value;
    }
}
