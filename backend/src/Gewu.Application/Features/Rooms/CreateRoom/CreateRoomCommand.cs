using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.CreateRoom;

/// <summary>创建房间,调用方成为 Host 和黑方。返回房间摘要。</summary>
public sealed record CreateRoomCommand(UserId HostUserId, string Name) : IRequest<RoomSummaryDto>;
