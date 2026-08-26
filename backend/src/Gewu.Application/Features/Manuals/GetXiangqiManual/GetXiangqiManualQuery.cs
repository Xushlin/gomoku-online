using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Manuals.GetXiangqiManual;

/// <summary>
/// 取一部象棋古谱的目录。公开资料,无需身份。
/// <para>
/// 谱不存在时返回 <c>null</c>,由端点译成 404 —— 一部谱只因为有线路才存在,所以
/// 「零条线路」与「没有这部谱」是同一件事。返回一个空目录会让**打错的键**看起来像
/// **一部空谱**,而那正是这个变更在播种器上刚修掉的那个毛病。
/// </para>
/// </summary>
/// <param name="ManualKey">古谱键,例如 meihuapu。</param>
public sealed record GetXiangqiManualQuery(string ManualKey) : IRequest<ManualCatalogueDto?>;
