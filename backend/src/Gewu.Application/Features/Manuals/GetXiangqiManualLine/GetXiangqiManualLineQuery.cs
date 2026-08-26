using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Manuals.GetXiangqiManualLine;

/// <summary>取一条古谱线路。不存在时返回 <c>null</c>,由端点译成 404。</summary>
/// <param name="LineId">线路主键。</param>
public sealed record GetXiangqiManualLineQuery(int LineId) : IRequest<ManualLineDto?>;
