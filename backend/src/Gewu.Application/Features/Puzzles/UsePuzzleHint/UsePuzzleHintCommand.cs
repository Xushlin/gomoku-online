using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Puzzles.UsePuzzleHint;

/// <summary>要一个提示。服务端揭示下一个片段并计费。</summary>
/// <param name="UserId">调用者。</param>
/// <param name="AttemptId">尝试 id。</param>
public sealed record UsePuzzleHintCommand(UserId UserId, Guid AttemptId)
    : IRequest<PuzzleHintDto>;
