using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Rooms;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetGameReplay;

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

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);

        string UserName(Guid id) => usernames.TryGetValue(id, out var n) ? n : "<unknown>";

        var moves = game.Moves
            .OrderBy(m => m.Ply)
            .Select(m => new MoveDto(m.Ply, m.Row, m.Col, m.Seat, m.PlayedAt, m.FromRow, m.FromCol, m.Text))
            .ToList()
            .AsReadOnly();

        return new GameReplayDto(
            RoomId: room.Id.Value,
            Name: room.Name,
            GameKey: room.GameKey,
            Host: new UserSummaryDto(room.HostUserId.Value, UserName(room.HostUserId.Value)),
            // 走 `Room.Seats`,不走 `BlackPlayerId` / `WhitePlayerId` —— 后两个只认 0 号与 1 号,
            // 于是三座位棋种的回放会**静默**丢掉 2 号座位上的人。`Room` 自己的文档写着
            // 「牌类棋种 MUST NOT 用这两个名字」,而这里此前照用不误。
            Seats: room.ToSeatDtos(usernames),
            StartedAt: game.StartedAt,
            EndedAt: game.EndedAt!.Value,
            Result: game.Result!.Value,
            WinnerUserId: game.WinnerUserId?.Value,
            EndReason: game.EndReason!.Value,
            Moves: moves);
    }
}
