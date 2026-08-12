using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Bots.ExecuteBotMove;

/// <summary>
/// AI 走子内部命令。**不经 REST / SignalR**,只由 <c>AiMoveWorker</c> 后台服务发出。
/// handler 从 room 快照里 replay Board、调 <see cref="Domain.Ai.IGomokuAi.SelectMove"/>、
/// 再把选出的落点**嵌套** dispatch 为 <c>MakeMoveCommand</c>,以复用现有落子 handler 的
/// 全套校验 / 事务 / ELO 更新 / SignalR 广播链路。
/// </summary>
public sealed record ExecuteBotMoveCommand(UserId BotUserId, RoomId RoomId) : IRequest<Unit>;
