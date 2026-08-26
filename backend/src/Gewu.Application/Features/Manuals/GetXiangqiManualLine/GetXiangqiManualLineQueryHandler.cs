using System.Text.Json;
using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Domain.Games.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Manuals.GetXiangqiManualLine;

/// <summary>
/// 单条古谱 handler。
/// <para>
/// 座位由**下标奇偶**给出,红先 —— 与播种时校验用的同一条规则。存一列「谁走的」会是
/// 第二份真源,而它和下标只可能在数据坏了的时候不一致,那时该报的是导入,不是这里。
/// </para>
/// </summary>
public sealed class GetXiangqiManualLineQueryHandler
    : IRequestHandler<GetXiangqiManualLineQuery, ManualLineDto?>
{
    private readonly IXiangqiManualRepository _manuals;

    /// <inheritdoc />
    public GetXiangqiManualLineQueryHandler(IXiangqiManualRepository manuals)
    {
        _manuals = manuals;
    }

    /// <inheritdoc />
    public async Task<ManualLineDto?> Handle(
        GetXiangqiManualLineQuery request, CancellationToken cancellationToken)
    {
        var line = await _manuals.GetLineAsync(request.LineId, cancellationToken);
        if (line is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(line.MovesJson);
        var moves = new List<ManualMoveDto>(doc.RootElement.GetArrayLength());
        var ply = 0;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var seat = ply % 2 == 0 ? BoardSeats.FirstSeat : BoardSeats.SecondSeat;
            moves.Add(new ManualMoveDto(
                ply + 1,
                item[0].GetInt32(),
                item[1].GetInt32(),
                item[2].GetInt32(),
                item[3].GetInt32(),
                seat));
            ply++;
        }

        return new ManualLineDto(
            line.Id,
            line.ManualKey,
            GameKeys.Xiangqi,
            line.Chapter,
            line.Title,
            line.WinnerSeat,
            moves);
    }
}
