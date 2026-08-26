using System.Text.Json;
using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Domain.Games.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Manuals.GetXiangqiManual;

/// <summary>
/// 古谱目录 handler。
/// <para>
/// 分组**来自数据**:局号是线路自己的列,章节列表由 <c>GroupBy</c> 得出,所以数据文件里
/// 某一局多一个变化,这里一行都不用改。硬编码「8 局」会在下一部谱落地时静静地对不上。
/// </para>
/// </summary>
public sealed class GetXiangqiManualQueryHandler
    : IRequestHandler<GetXiangqiManualQuery, ManualCatalogueDto?>
{
    private readonly IXiangqiManualRepository _manuals;

    /// <inheritdoc />
    public GetXiangqiManualQueryHandler(IXiangqiManualRepository manuals)
    {
        _manuals = manuals;
    }

    /// <inheritdoc />
    public async Task<ManualCatalogueDto?> Handle(
        GetXiangqiManualQuery request, CancellationToken cancellationToken)
    {
        var lines = await _manuals.ListLinesAsync(request.ManualKey, cancellationToken);
        if (lines.Count == 0)
        {
            return null;
        }

        var chapters = lines
            .GroupBy(l => l.Chapter)
            .OrderBy(g => g.Key)
            .Select(g => new ManualChapterDto(
                g.Key,
                g.OrderBy(l => l.OrderInChapter)
                    .Select(l => new ManualLineSummaryDto(
                        l.Id,
                        l.Title,
                        CountMoves(l.MovesJson),
                        l.Verdict,
                        CountPieces(l.StartPosition)))
                    .ToList()))
            .ToList();

        return new ManualCatalogueDto(request.ManualKey, GameKeys.Xiangqi, chapters);
    }

    /// <summary>半手数 —— 由着法数组算出,不是存的一列。</summary>
    private static int CountMoves(string movesJson)
    {
        using var doc = JsonDocument.Parse(movesJson);
        return doc.RootElement.GetArrayLength();
    }

    /// <summary>
    /// 起始局面上的子数 —— 由盘面串算出。
    /// <para>
    /// 界面用它区分残局与满盘,而它 **MUST NOT** 被当成「是不是标准开局」的判据:实测
    /// 满盘 163 局、标准开局 157 局,**有 6 局是 32 子却不是标准摆法**。两个判据混用会在
    /// 那 6 局上静静说错。
    /// </para>
    /// </summary>
    private static int CountPieces(string board)
    {
        var n = 0;
        foreach (var c in board)
        {
            if (c != '.') n++;
        }
        return n;
    }
}
