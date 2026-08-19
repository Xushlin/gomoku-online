using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.Abstractions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gewu.Application.Features.Rooms.GetRoomState;

/// <summary>完整房间状态查询 handler。</summary>
public sealed class GetRoomStateQueryHandler : IRequestHandler<GetRoomStateQuery, RoomStateDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly GameOptions _gameOptions;
    private readonly IGameRulesRegistry _rules;

    /// <inheritdoc />
    public GetRoomStateQueryHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IOptions<GameOptions> gameOptions,
        IGameRulesRegistry rules)
    {
        _rooms = rooms;
        _users = users;
        _gameOptions = gameOptions.Value;
        _rules = rules;
    }

    /// <inheritdoc />
    public async Task<RoomStateDto> Handle(GetRoomStateQuery request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        // 规则用来算这个座位的私有切片。**键解析不出来时给 null,而不是抛** ——
        // 一个指向本构建不认识的棋种的房间仍然该能被看到(名字、玩家、聊天都在),
        // 只是它没有私有状态可给。抛的话会把"看一眼房间"变成 404。
        var rules = _rules.For(room.GameKey);
        return room.ToState(
            usernames, _gameOptions.TurnTimeoutSeconds, RoomView.For(room, request.ViewerId, rules));
    }
}
