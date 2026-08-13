using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Puzzles.GetPuzzleProgress;

/// <summary>取某游戏的整体进度。返回值全部是派生量。</summary>
/// <param name="UserId">调用者。</param>
/// <param name="GameKey">游戏键。</param>
public sealed record GetPuzzleProgressQuery(UserId UserId, string GameKey)
    : IRequest<PuzzleProgressDto>;
