using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.MakeMove;

/// <summary>在房间内当前用户的回合落一子。返回刚落的 <see cref="MoveDto"/>。</summary>
/// <param name="UserId">走子的玩家。</param>
/// <param name="RoomId">房间。</param>
/// <param name="Row">终点 / 落点行。</param>
/// <param name="Col">终点 / 落点列。</param>
/// <param name="FromRow">
/// 起点行;**落子类棋种(五子棋 / 一字棋)为 <c>null</c>**,走子类(中国象棋)非 <c>null</c>。
/// 已发布的客户端不送这两个字段,于是天然落在落子类那一侧 —— 形状向后兼容。
/// 与 <paramref name="FromCol"/> 必须同为 <c>null</c> 或同为非 <c>null</c>;
/// 形状对不对最终由规则判,handler 不猜哪些棋种走子。
/// </param>
/// <param name="FromCol">起点列;见 <paramref name="FromRow"/>。</param>
/// <param name="Text">
/// 文本载荷(成语接龙的一个成语);**位置类棋种为 <c>null</c>**,此时四个坐标非空。
/// </param>
public sealed record MakeMoveCommand(
    UserId UserId,
    RoomId RoomId,
    int? Row = null,
    int? Col = null,
    int? FromRow = null,
    int? FromCol = null,
    string? Text = null) : IRequest<MoveDto>;
