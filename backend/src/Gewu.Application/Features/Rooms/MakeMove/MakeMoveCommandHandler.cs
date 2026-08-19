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

        // 三种载荷,选**恰好一个**工厂。这里不再实现一遍「恰好一种」——那条不变量由
        // MoveIntent 的构造器强制,拼错了会当场抛,而不是悄悄传下去。
        // 哪个棋种收哪种,由规则判:聚合根与 handler 都不知道谁走子、谁说词。
        var intent = BuildIntent(request);

        var outcome = room.PlayMove(request.UserId, intent, _clock.UtcNow, rules);

        if (outcome.Result != GameResult.Ongoing)
        {
            await GameEloApplier.ApplyAsync(room, outcome.Result, _rules, _users, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        var moveDto = new MoveDto(
            outcome.Move.Ply,
            outcome.Move.Row,
            outcome.Move.Col,
            SeatWire.ToStone(outcome.Move.Seat),
            outcome.Move.PlayedAt,
            outcome.Move.FromRow,
            outcome.Move.FromCol,
            outcome.Move.Text);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);

        await _notifier.RoomStateChangedAsync(room, usernames, _gameOptions.TurnTimeoutSeconds, cancellationToken);
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

    /// <summary>把命令上的扁平载荷搬成一个 <see cref="MoveIntent"/>。</summary>
    /// <param name="request">落子命令。</param>
    private static MoveIntent BuildIntent(MakeMoveCommand request)
    {
        if (request.Text is not null)
        {
            return MoveIntent.Say(request.Text);
        }

        // 坐标缺失时**不**补默认值 —— 让 MoveIntent 的构造器拒绝,那是这条不变量唯一的家。
        var to = request.Row is int r && request.Col is int c
            ? new Position(r, c)
            : (Position?)null;

        return request.FromRow is int fr && request.FromCol is int fc && to is { } dest
            ? MoveIntent.Slide(new Position(fr, fc), dest)
            : new MoveIntent(null, to, null);
    }
}
