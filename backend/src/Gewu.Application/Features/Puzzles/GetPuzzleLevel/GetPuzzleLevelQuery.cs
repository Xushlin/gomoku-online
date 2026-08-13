using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Puzzles.GetPuzzleLevel;

/// <summary>取单个关卡的可下发内容(布局,不含答案)。</summary>
/// <param name="GameKey">游戏键。</param>
/// <param name="LevelIndex">关卡序号。</param>
public sealed record GetPuzzleLevelQuery(string GameKey, int LevelIndex)
    : IRequest<PuzzleLevelDto>;
