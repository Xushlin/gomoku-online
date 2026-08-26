using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Manuals.ListXiangqiManuals;

/// <summary>列出全部象棋古谱。公开资料,无需身份。</summary>
public sealed record ListXiangqiManualsQuery : IRequest<IReadOnlyList<ManualSummaryDto>>;
