using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.ScoreRuns.StartScoreRun;

/// <summary>
/// 开一局计分类游戏。
/// <para>
/// 命令里**没有** seed 字段,这是刻意的:种子由服务端生成,客户端上报的种子在这里没有落点,
/// 也就无法挑一个对自己有利的方块序列。
/// </para>
/// </summary>
/// <param name="UserId">调用者。</param>
/// <param name="GameKey">游戏键。</param>
public sealed record StartScoreRunCommand(UserId UserId, string GameKey)
    : IRequest<ScoreRunStartedDto>;
