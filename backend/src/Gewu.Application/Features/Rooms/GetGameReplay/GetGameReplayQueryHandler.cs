using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using Gomoku.Domain.Rooms;
using MediatR;

namespace Gomoku.Application.Features.Rooms.GetGameReplay;

/// <summary>
/// 按房间 Id 构造 <see cref="GameReplayDto"/>。仅 Finished 房间允许;其他状态抛
/// <see cref="GameNotFinishedException"/>(HTTP 409);房间不存在抛 <see cref="RoomNotFoundException"/>(HTTP 404)。
/// </summary>
public sealed class GetGameReplayQueryHandler : IRequestHandler<GetGameReplayQuery, GameReplayDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetGameReplayQueryHandler(IRoomRepository rooms, IUserRepository users)
    {
        _rooms = rooms;
        _users = users;
    }

    /// <inheritdoc />
    public async Task<GameReplayDto> Handle(GetGameReplayQuery request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        if (room.Status != RoomStatus.Finished || room.Game is null)
        {
            throw new GameNotFinishedException(
                $"Replay is only available for finished games; room '{room.Id.Value}' is {room.Status}.");
        }

        var game = room.Game;
        var whiteId = room.WhitePlayerId!.Value; // Finished 保证

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);

        string UserName(Guid id) => usernames.TryGetValue(id, out var n) ? n : "<unknown>";

        var moves = game.Moves
            .OrderBy(m => m.Ply)
            .Select(m => new MoveDto(m.Ply, m.Row, m.Col, m.Stone, m.PlayedAt))
            .ToList()
            .AsReadOnly();

        return new GameReplayDto(
            RoomId: room.Id.Value,
            Name: room.Name,
            Host: new UserSummaryDto(room.HostUserId.Value, UserName(room.HostUserId.Value)),
            Black: new UserSummaryDto(room.BlackPlayerId.Value, UserName(room.BlackPlayerId.Value)),
            White: new UserSummaryDto(whiteId.Value, UserName(whiteId.Value)),
            StartedAt: game.StartedAt,
            EndedAt: game.EndedAt!.Value,
            Result: game.Result!.Value,
            WinnerUserId: game.WinnerUserId?.Value,
            EndReason: game.EndReason!.Value,
            Moves: moves);
    }
}
