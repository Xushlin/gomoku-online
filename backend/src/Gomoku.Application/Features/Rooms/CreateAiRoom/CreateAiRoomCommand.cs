using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Ai;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.CreateAiRoom;

/// <summary>
/// 创建一个 AI 对局房间。调用方(必须是真人)成为 Host;seeded 机器人按
/// <paramref name="Difficulty"/> 立即加入,房间状态一步进入 <c>Playing</c>。
/// <paramref name="HumanSide"/> 决定人坐哪一边:<c>Stone.Black</c>(默认)= 真人执黑、
/// AI 执白、真人先走;<c>Stone.White</c> = 真人执白、AI 执黑、AI worker 立刻走第 1 步。
/// 返回 <see cref="RoomStateDto"/>。
/// </summary>
public sealed record CreateAiRoomCommand(
    UserId HostUserId,
    string Name,
    BotDifficulty Difficulty,
    Stone HumanSide) : IRequest<RoomStateDto>;
