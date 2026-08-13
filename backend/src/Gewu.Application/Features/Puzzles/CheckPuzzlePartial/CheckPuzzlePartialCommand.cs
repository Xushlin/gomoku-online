using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Puzzles.CheckPuzzlePartial;

/// <summary>
/// 校验一份部分答案(一条成语、一个区域)。判错时服务端给该尝试记一次错
/// —— 错误计数因此是服务端观测量,不是客户端自述。
/// </summary>
/// <param name="UserId">调用者。</param>
/// <param name="AttemptId">尝试 id。</param>
/// <param name="PartialJson">这一部分的提交内容。</param>
public sealed record CheckPuzzlePartialCommand(UserId UserId, Guid AttemptId, string PartialJson)
    : IRequest<PuzzleCheckResultDto>;
