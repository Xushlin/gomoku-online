using FluentValidation.Results;
using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using Gomoku.Domain.Rooms;
using MediatR;
using ValidationException = Gomoku.Application.Common.Exceptions.ValidationException;

namespace Gomoku.Application.Features.Rooms.CreateAiRoom;

/// <summary>
/// 创建 AI 房间 handler。流程:
/// ① 加载 Host,防御式拒绝 Host 本身就是 bot 的请求。
/// ② 按难度加载 seeded bot(不存在 → <see cref="UserNotFoundException"/>,提示检查 migration)。
/// ③ <c>Room.Create</c> → <c>Room.JoinAsPlayer(bot)</c> —— 状态一步进入 Playing。
/// ④ AddAsync + SaveChanges 一次提交。
/// ⑤ 组装 <see cref="RoomStateDto"/> 返回。
/// 本 handler **不广播** SignalR 事件 —— 房间刚创建,客户端尚未 <c>JoinRoom</c> 加入
/// SignalR group,任何此时的广播都会丢失;后续客户端 <c>JoinRoom</c> 后会收到 room state。
/// </summary>
public sealed class CreateAiRoomCommandHandler : IRequestHandler<CreateAiRoomCommand, RoomStateDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public CreateAiRoomCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<RoomStateDto> Handle(CreateAiRoomCommand request, CancellationToken cancellationToken)
    {
        var host = await _users.FindByIdAsync(request.HostUserId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{request.HostUserId.Value}' was not found.");

        if (host.IsBot)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.HostUserId), "AI cannot host an AI room."),
            });
        }

        var bot = await _users.FindBotByDifficultyAsync(request.Difficulty, cancellationToken)
            ?? throw new UserNotFoundException(
                $"Seeded bot account for difficulty '{request.Difficulty}' was not found. " +
                "Did the AddBotSupport migration run?");

        var now = _clock.UtcNow;
        var room = Room.Create(RoomId.NewId(), request.Name, request.HostUserId, now);
        room.JoinAsPlayer(bot.Id, now);

        await _rooms.AddAsync(room, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var usernames = new Dictionary<Guid, string>
        {
            [host.Id.Value] = host.Username.Value,
            [bot.Id.Value] = bot.Username.Value,
        };
        return room.ToState(usernames);
    }
}
