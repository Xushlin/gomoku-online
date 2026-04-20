using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.Dissolve;

/// <summary>
/// 由 Host 解散一个 Waiting 状态的房间。成功路径物理删除房间聚合(连带围观者 / 聊天级联清理),
/// 并广播 <c>RoomDissolved</c> SignalR 事件。返回 <see cref="Unit"/>(<c>204 No Content</c>)。
/// </summary>
public sealed record DissolveRoomCommand(UserId SenderUserId, RoomId RoomId) : IRequest<Unit>;
