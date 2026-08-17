namespace Gewu.Domain.Games.Tetris;

/// <summary>
/// 七种标准方块。底层数值稳定 —— 它进入 <see cref="TetrisPieceSequence"/> 的输出,
/// 而那个序列由种子决定并被客户端复现,改数值等于改所有既有 run 的重放结果。
/// </summary>
public enum TetrominoKind
{
    /// <summary>直条。</summary>
    I = 0,
    /// <summary>方块。</summary>
    O = 1,
    /// <summary>T 形。</summary>
    T = 2,
    /// <summary>S 形。</summary>
    S = 3,
    /// <summary>Z 形。</summary>
    Z = 4,
    /// <summary>J 形。</summary>
    J = 5,
    /// <summary>L 形。</summary>
    L = 6,
}

/// <summary>
/// 方块的形状表:每种 × 四个旋转态 → 占用的 (行, 列) 偏移。
/// <para>
/// 偏移以每个旋转态自己的左上角为原点,行向下为正。表是**写死的**而不是靠旋转算法算出来的:
/// 算法要处理 I 与 O 的特例,而一张四十九个条目的表可以逐项肉眼核对,也不会因为
/// "旋转中心取在哪"这种约定分歧而与客户端不一致。
/// </para>
/// </summary>
public static class Tetromino
{
    /// <summary>旋转态个数。</summary>
    public const int Rotations = 4;

    private static readonly (int Row, int Col)[][][] Shapes = BuildShapes();

    /// <summary>取某种方块某个旋转态占用的格子偏移。</summary>
    /// <param name="kind">方块种类。</param>
    /// <param name="rotation">旋转态,取模 4。</param>
    public static IReadOnlyList<(int Row, int Col)> CellsOf(TetrominoKind kind, int rotation)
        => Shapes[(int)kind][((rotation % Rotations) + Rotations) % Rotations];

    /// <summary>该旋转态的宽度(列数)—— 用来判断放置的列是否越界。</summary>
    /// <param name="kind">方块种类。</param>
    /// <param name="rotation">旋转态。</param>
    public static int WidthOf(TetrominoKind kind, int rotation)
    {
        var cells = CellsOf(kind, rotation);
        var max = 0;
        foreach (var (_, col) in cells)
        {
            if (col > max) max = col;
        }
        return max + 1;
    }

    private static (int Row, int Col)[][][] BuildShapes()
    {
        // 每种方块给出旋转态 0 的格子,其余三个由 90° 旋转推出 —— 推导在**构造时**跑一次,
        // 结果是一张静态表。这样既不用手抄 112 个条目,也不用在重放的热路径上算旋转。
        var basis = new Dictionary<TetrominoKind, (int Row, int Col)[]>
        {
            [TetrominoKind.I] = [(0, 0), (0, 1), (0, 2), (0, 3)],
            [TetrominoKind.O] = [(0, 0), (0, 1), (1, 0), (1, 1)],
            [TetrominoKind.T] = [(0, 1), (1, 0), (1, 1), (1, 2)],
            [TetrominoKind.S] = [(0, 1), (0, 2), (1, 0), (1, 1)],
            [TetrominoKind.Z] = [(0, 0), (0, 1), (1, 1), (1, 2)],
            [TetrominoKind.J] = [(0, 0), (1, 0), (1, 1), (1, 2)],
            [TetrominoKind.L] = [(0, 2), (1, 0), (1, 1), (1, 2)],
        };

        var all = new (int Row, int Col)[7][][];
        foreach (var (kind, cells) in basis)
        {
            var states = new (int Row, int Col)[Rotations][];
            var current = cells;
            for (var r = 0; r < Rotations; r++)
            {
                states[r] = Normalise(current);
                current = RotateClockwise(current);
            }
            all[(int)kind] = states;
        }
        return all;
    }

    /// <summary>顺时针 90°:(row, col) → (col, -row)。</summary>
    private static (int Row, int Col)[] RotateClockwise((int Row, int Col)[] cells)
        => [.. cells.Select(c => (c.Col, -c.Row))];

    /// <summary>平移到左上角贴原点 —— 让"列"这个放置参数有唯一含义。</summary>
    private static (int Row, int Col)[] Normalise((int Row, int Col)[] cells)
    {
        var minRow = cells.Min(c => c.Row);
        var minCol = cells.Min(c => c.Col);
        return [.. cells.Select(c => (c.Row - minRow, c.Col - minCol))
            .OrderBy(c => c.Item1).ThenBy(c => c.Item2)];
    }
}
