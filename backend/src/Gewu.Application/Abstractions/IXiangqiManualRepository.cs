using Gewu.Domain.Manuals;

namespace Gewu.Application.Abstractions;

/// <summary>
/// 古谱线路的读取口。**只读** —— 古谱是资料,运行期没有人写它,写入只发生在播种。
/// </summary>
public interface IXiangqiManualRepository
{
    /// <summary>按目录顺序列出一部古谱的全部线路。</summary>
    /// <param name="manualKey">古谱键。</param>
    /// <param name="ct">取消标记。</param>
    /// <returns>按「局、局内次序」升序。</returns>
    Task<IReadOnlyList<XiangqiManualLine>> ListLinesAsync(string manualKey, CancellationToken ct = default);

    /// <summary>取一条线路。</summary>
    /// <param name="id">线路主键。</param>
    /// <param name="ct">取消标记。</param>
    /// <returns>不存在时为 <c>null</c>。</returns>
    Task<XiangqiManualLine?> GetLineAsync(int id, CancellationToken ct = default);
}
