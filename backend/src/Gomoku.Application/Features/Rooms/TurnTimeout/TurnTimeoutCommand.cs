using Gomoku.Domain.Rooms;
using MediatR;

namespace Gomoku.Application.Features.Rooms.TurnTimeout;

/// <summary>
/// 超时判当前回合玩家负的**内部命令**,仅由 <c>TurnTimeoutWorker</c> 发送。
/// MUST NOT 暴露 REST 端点、MUST NOT 路由 SignalR Hub —— 避免被客户端构造"踢人"指令。
/// </summary>
public sealed record TurnTimeoutCommand(RoomId RoomId) : IRequest<Unit>;
