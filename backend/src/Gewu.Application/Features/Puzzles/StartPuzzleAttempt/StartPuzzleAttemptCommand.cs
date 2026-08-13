using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Puzzles.StartPuzzleAttempt;

/// <summary>发起一次闯关尝试。开始时间由服务端时钟决定。</summary>
/// <param name="UserId">调用者。</param>
/// <param name="GameKey">游戏键。</param>
/// <param name="LevelIndex">关卡序号。</param>
public sealed record StartPuzzleAttemptCommand(UserId UserId, string GameKey, int LevelIndex)
    : IRequest<PuzzleAttemptStartedDto>;
