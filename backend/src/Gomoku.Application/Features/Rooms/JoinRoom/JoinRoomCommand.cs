using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.JoinRoom;

/// <summary>用户作为白方加入房间,触发对局开始。</summary>
public sealed record JoinRoomCommand(UserId UserId, RoomId RoomId) : IRequest<RoomStateDto>;
