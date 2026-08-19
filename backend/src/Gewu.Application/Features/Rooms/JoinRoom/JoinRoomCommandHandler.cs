using Gewu.Application.Abstractions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gewu.Application.Features.Rooms.JoinRoom;

/// <summary>加入房间为白方并启动对局;广播 <c>PlayerJoined</c> + <c>RoomStateChanged</c>。</summary>
public sealed class JoinRoomCommandHandler : IRequestHandler<JoinRoomCommand, RoomStateDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly GameOptions _gameOptions;
    private readonly IGameRulesRegistry _rules;
    private readonly ISeedProvider _seeds;

    /// <inheritdoc />
    public JoinRoomCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier,
        IOptions<GameOptions> gameOptions,
        IGameRulesRegistry rules,
        ISeedProvider seeds)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
        _gameOptions = gameOptions.Value;
        _rules = rules;
        _seeds = seeds;
    }

    /// <inheritdoc />
    public async Task<RoomStateDto> Handle(JoinRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        // 座位数由规则说,而不是房间自己存一份 —— 与 PlayMove 收规则是同一个形状。
        // 未知棋种的处理与落子路径一致:那是一条损坏的房间记录,不是一次非法加入。
        var rules = _rules.For(room.GameKey)
            ?? throw new RoomNotFoundException(
                $"Room '{room.Id.Value}' declares unknown game '{room.GameKey}'.");

        room.JoinAsPlayer(
            request.UserId, _clock.UtcNow, rules, MatchSetup.For(rules, _seeds));
        await _uow.SaveChangesAsync(cancellationToken);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames, _gameOptions.TurnTimeoutSeconds, RoomView.For(room, request.UserId));
        var joiner = new UserSummaryDto(request.UserId.Value,
            usernames.TryGetValue(request.UserId.Value, out var n) ? n : "<unknown>");

        await _notifier.PlayerJoinedAsync(room.Id, joiner, cancellationToken);
        await _notifier.RoomStateChangedAsync(room, usernames, _gameOptions.TurnTimeoutSeconds, cancellationToken);

        return state;
    }
}
