using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Puzzles.SubmitPuzzleAttempt;

/// <summary>
/// 提交完整答案。
/// <para>
/// 命令里**没有**耗时、错误数、提示数这类字段,这是刻意的:三者都是服务端事实,
/// 客户端上报任何自评数值都不会被读取,因为无处可放。
/// </para>
/// </summary>
/// <param name="UserId">调用者。</param>
/// <param name="AttemptId">尝试 id。</param>
/// <param name="SubmissionJson">完整答案提交内容。</param>
public sealed record SubmitPuzzleAttemptCommand(UserId UserId, Guid AttemptId, string SubmissionJson)
    : IRequest<PuzzleSubmitResultDto>;
