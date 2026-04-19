using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.CreateRoom;

/// <summary>创建房间,调用方成为 Host 和黑方。返回房间摘要。</summary>
public sealed record CreateRoomCommand(UserId HostUserId, string Name) : IRequest<RoomSummaryDto>;
