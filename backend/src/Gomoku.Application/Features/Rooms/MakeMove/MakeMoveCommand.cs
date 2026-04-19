using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.MakeMove;

/// <summary>在房间内当前用户的回合落一子。返回刚落的 <see cref="MoveDto"/>。</summary>
public sealed record MakeMoveCommand(UserId UserId, RoomId RoomId, int Row, int Col) : IRequest<MoveDto>;
