using Gewu.Application.Common.DTOs;
using Gewu.Domain.Ai;
using Gewu.Domain.Enums;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.CreateAiRoom;

/// <summary>
/// 创建一个 AI 对局房间。调用方(必须是真人)成为 Host;seeded 机器人按
/// <paramref name="Difficulty"/> 立即加入,房间状态一步进入 <c>Playing</c>。
/// <paramref name="HumanSide"/> 决定人坐哪一边:<c>Stone.Black</c>(默认)= 真人执黑、
/// AI 执白、真人先走;<c>Stone.White</c> = 真人执白、AI 执黑、AI worker 立刻走第 1 步。
/// 返回 <see cref="RoomStateDto"/>。
/// <para>
/// <paramref name="GameKey"/> 必填,与 <c>CreateRoomCommand</c> 同理。机器人账号**跨棋种共用**
/// —— 一个 bot 账号是身份而不是策略,它在某局里跑哪套算法由 <c>(GameKey, Difficulty)</c>
/// 经 AI 注册表解析。新增棋种因此不往 users 表插新行。
/// </para>
/// </summary>
public sealed record CreateAiRoomCommand(
    UserId HostUserId,
    string Name,
    BotDifficulty Difficulty,
    Stone HumanSide,
    string GameKey) : IRequest<RoomStateDto>;
