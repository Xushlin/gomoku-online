using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.Exceptions;
using MediatR;

namespace Gomoku.Application.Features.Rooms.Dissolve;

/// <summary>
/// 解散房间 handler。流程:
/// Load → <c>Room.Dissolve(senderId)</c> 校验身份与状态 →
/// <c>IRoomRepository.DeleteAsync</c> 标记删除 →
/// <c>IUnitOfWork.SaveChangesAsync</c> 提交 →
/// <c>IRoomNotifier.RoomDissolvedAsync</c> 广播。
/// 领域 / 仓储异常不 catch,让全局中间件映射。
/// </summary>
public sealed class DissolveRoomCommandHandler : IRequestHandler<DissolveRoomCommand, Unit>
{
    private readonly IRoomRepository _rooms;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;

    /// <inheritdoc />
    public DissolveRoomCommandHandler(
        IRoomRepository rooms,
        IUnitOfWork uow,
        IRoomNotifier notifier)
    {
        _rooms = rooms;
        _uow = uow;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(DissolveRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        room.Dissolve(request.SenderUserId);

        await _rooms.DeleteAsync(room, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _notifier.RoomDissolvedAsync(request.RoomId, cancellationToken);

        return Unit.Value;
    }
}
