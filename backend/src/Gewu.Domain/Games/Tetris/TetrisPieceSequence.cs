namespace Gewu.Domain.Games.Tetris;

/// <summary>
/// `seed → 方块序列` 的确定性生成器。
/// <para>
/// **这是本游戏唯一容许存在两份实现的东西**(服务端一份、客户端一份),而容许的理由与这个
/// 仓库别处不同:它是一个纯函数,无状态、无计时,可以用一条测试逐项对齐(前 N 个必须完全相同)。
/// 它不一致的症状也不是"合法游戏被拒",而是"第一个方块就不对" —— 立刻可见。
/// </para>
/// <para>
/// 用的是**七袋法**(seven-bag):每七个一袋,袋内是七种方块的一个排列。这不是为了好玩 ——
/// 纯随机会出现长串同种方块,而那让分数更多取决于运气而不是打法,一个分数榜最不需要的就是那个。
/// </para>
/// <para>
/// 洗牌用 xorshift 而不是 <c>System.Random</c>:<c>Random</c> 的算法在 .NET 版本之间**变过**,
/// 而这个序列必须跨版本、跨语言(客户端是 TypeScript)完全一致。一个"升级运行时之后所有历史
/// run 的重放结果都变了"的生成器,比没有生成器更糟。
/// </para>
/// </summary>
public static class TetrisPieceSequence
{
    /// <summary>一袋的大小 —— 七种方块各一个。</summary>
    public const int BagSize = 7;

    /// <summary>取序列的前 <paramref name="count"/> 个方块。</summary>
    /// <param name="seed">服务端下发的种子。</param>
    /// <param name="count">要多少个。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 为负。</exception>
    public static IReadOnlyList<TetrominoKind> Take(int seed, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var result = new List<TetrominoKind>(count);
        // 状态为 0 会让 xorshift 永远停在 0 —— 那会退化成"永远第一种方块"。
        var state = (uint)seed == 0 ? 0x9E3779B9u : (uint)seed;

        while (result.Count < count)
        {
            var bag = new List<TetrominoKind>
            {
                TetrominoKind.I, TetrominoKind.O, TetrominoKind.T, TetrominoKind.S,
                TetrominoKind.Z, TetrominoKind.J, TetrominoKind.L,
            };

            // Fisher–Yates,从后往前 —— 洗一袋。
            for (var i = BagSize - 1; i > 0; i--)
            {
                state = NextState(state);
                var j = (int)(state % (uint)(i + 1));
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            foreach (var kind in bag)
            {
                if (result.Count == count) break;
                result.Add(kind);
            }
        }

        return result;
    }

    /// <summary>xorshift32 —— 算法写死在这里,不用运行时的 RNG。</summary>
    /// <param name="state">当前状态,MUST 非零。</param>
    private static uint NextState(uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }
}
