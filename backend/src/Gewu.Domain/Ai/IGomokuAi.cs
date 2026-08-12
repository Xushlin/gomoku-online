using Gomoku.Domain.Entities;
using Gomoku.Domain.Enums;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Ai;

/// <summary>
/// 五子棋 AI 决策接口。实现 MUST 是纯函数式:
/// <list type="bullet">
/// <item>返回的 <see cref="Position"/> MUST 落在 <paramref name="board"/> 的空格上(<see cref="Stone.Empty"/>);</item>
/// <item>MUST NOT 修改入参 <paramref name="board"/>(实现内部如需试走,应先 <see cref="Board.Clone"/>);</item>
/// <item>MUST NOT 读取时钟 / 磁盘 / 网络 / 静态可变状态;</item>
/// <item>对相同 <paramref name="board"/> 快照 + 相同 <paramref name="myStone"/> 与相同随机源,输出 MUST 可复现。</item>
/// </list>
/// </summary>
public interface IGomokuAi
{
    /// <summary>
    /// 在给定棋盘快照上,为落子方 <paramref name="myStone"/> 选择下一步。
    /// </summary>
    /// <param name="board">当前棋盘快照;调用前后内容 MUST 一致。</param>
    /// <param name="myStone">己方棋色,MUST 是 <see cref="Stone.Black"/> 或 <see cref="Stone.White"/>。</param>
    /// <returns>一个合法的空格坐标。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="myStone"/> 为 <see cref="Stone.Empty"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="board"/> 已被 225 子全部占据;调用方应在棋盘满之前就已结束对局。
    /// </exception>
    Position SelectMove(Board board, Stone myStone);
}
