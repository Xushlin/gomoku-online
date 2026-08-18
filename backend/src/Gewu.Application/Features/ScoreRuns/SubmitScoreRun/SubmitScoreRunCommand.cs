using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.ScoreRuns.SubmitScoreRun;

/// <summary>
/// 提交一局的放置序列。
/// <para>
/// 命令里**没有** score / lines / level / duration 字段,这是刻意的,和
/// <c>SubmitPuzzleAttemptCommand</c> 同一手法:四者都是服务端事实,客户端上报的自评数值
/// 在这里没有落点,也就无法影响计分。这不是"我们记得忽略它",而是**无处可放**。
/// </para>
/// </summary>
/// <param name="UserId">调用者。</param>
/// <param name="RunId">run id。</param>
/// <param name="Placements">按顺序的放置;第 i 项对应方块序列里第 i 个方块。</param>
public sealed record SubmitScoreRunCommand(
    UserId UserId, Guid RunId, IReadOnlyList<ScorePlacementDto> Placements)
    : IRequest<ScoreRunResultDto>;
