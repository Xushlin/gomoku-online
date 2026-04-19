using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Ai;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.CreateAiRoom;

/// <summary>
/// 创建一个 AI 对局房间。调用方(必须是真人)成为 Host + 黑方;seeded 机器人按
/// <paramref name="Difficulty"/> 立即加入为白方,房间状态一步进入 <c>Playing</c>。
/// 返回 <see cref="RoomStateDto"/> —— 前端拿到即可渲染棋盘。
/// </summary>
public sealed record CreateAiRoomCommand(
    UserId HostUserId,
    string Name,
    BotDifficulty Difficulty) : IRequest<RoomStateDto>;
