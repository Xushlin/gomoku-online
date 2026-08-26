using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Manuals.ListXiangqiManuals;

/// <summary>
/// 古谱清单 handler。
/// <para>
/// 清单**来自库**,而不是客户端写死的七个键:加一辑是加一份数据文件加一行注册,前端不改。
/// 条数同样是算出来的 —— 一个与线路并存的计数是第二份真源。
/// </para>
/// </summary>
public sealed class ListXiangqiManualsQueryHandler
    : IRequestHandler<ListXiangqiManualsQuery, IReadOnlyList<ManualSummaryDto>>
{
    private readonly IXiangqiManualRepository _manuals;

    /// <inheritdoc />
    public ListXiangqiManualsQueryHandler(IXiangqiManualRepository manuals) => _manuals = manuals;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ManualSummaryDto>> Handle(
        ListXiangqiManualsQuery request, CancellationToken cancellationToken)
    {
        var manuals = await _manuals.ListManualsAsync(cancellationToken);
        return manuals
            .Select(m => new ManualSummaryDto(m.Manual.Key, m.Manual.Name, m.LineCount, m.Manual.Grouped))
            .ToList();
    }
}
