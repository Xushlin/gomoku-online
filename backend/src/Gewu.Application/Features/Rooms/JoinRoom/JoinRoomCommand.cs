using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.JoinRoom;

/// <summary>用户作为白方加入房间,触发对局开始。</summary>
public sealed record JoinRoomCommand(UserId UserId, RoomId RoomId) : IRequest<RoomStateDto>;
