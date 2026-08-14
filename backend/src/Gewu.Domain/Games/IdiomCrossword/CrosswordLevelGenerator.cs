
namespace Gewu.Domain.Games.IdiomCrossword;

/// <summary>一个生成好的关卡:布局(可下发)+ 答案(服务端专用)。</summary>
/// <param name="Layout">下发给客户端的布局。</param>
/// <param name="Solution">服务端答案。</param>
/// <param name="Difficulty">难度档位。</param>
public sealed record GeneratedLevel(
    CrosswordLayout Layout,
    CrosswordSolution Solution,
    int Difficulty);

/// <summary>
/// 关卡生成器。
/// <para>
/// **全部随机选择都来自构造时传入的种子**,没有 <c>Random.Shared</c>、没有时钟、没有任何
/// 环境随机源。候选列表在抽取前一律按成语排序,好让"同种子同词典 ⇒ 同产物"成立到逐字节
/// —— 这条让关卡集成为可追溯、可复现、可 diff 的产物,而不是某台机器上发生过一次的事件。
/// </para>
/// </summary>
public sealed class CrosswordLevelGenerator
{
    /// <summary>每次摆放的候选尝试上限 —— 用尽就少放一条,绝不空转。</summary>
    private const int PlacementAttemptBudget = 400;

    private readonly List<SourceIdiom> _corpus;
    private readonly Dictionary<(char Char, int Position), List<SourceIdiom>> _index;
    private readonly List<char> _distractorPool;
    private readonly Random _rng;

    /// <summary>用四字成语语料建索引。</summary>
    /// <param name="corpus">可出题的四字成语(调用方已按层级过滤)。</param>
    /// <param name="seed">随机种子 —— 决定整批产物。</param>
    public CrosswordLevelGenerator(IEnumerable<SourceIdiom> corpus, int seed)
    {
        // 排序后固化:字典顺序是确定的,所以索引里每个桶的顺序也是确定的。
        _corpus = corpus.OrderBy(i => i.Word, StringComparer.Ordinal).ToList();
        _rng = new Random(seed);

        _index = new Dictionary<(char, int), List<SourceIdiom>>();
        foreach (var idiom in _corpus)
        {
            for (var p = 0; p < idiom.Word.Length; p++)
            {
                var key = (idiom.Word[p], p);
                if (!_index.TryGetValue(key, out var bucket))
                {
                    bucket = new List<SourceIdiom>();
                    _index[key] = bucket;
                }
                bucket.Add(idiom);
            }
        }

        // 干扰字取自语料里最常见的那批字:太生僻的干扰字一眼就能排除,不构成干扰。
        _distractorPool = _corpus
            .SelectMany(i => i.Word)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(300)
            .Select(g => g.Key)
            .ToList();
    }

    /// <summary>
    /// 生成一个关卡。达不到目标条数时按实际条数产出并返回,由调用方决定是否接受
    /// —— 少放一条是可以的,空转不行。
    /// </summary>
    /// <param name="dial">难度旋钮。</param>
    /// <param name="difficulty">写入关卡的难度档位。</param>
    public GeneratedLevel Generate(DifficultyDial dial, int difficulty)
    {
        var builder = new CrosswordGrid();
        builder.PlaceSeed(Pick(_corpus));

        var attempts = 0;
        while (builder.Words.Count < dial.IdiomCount && attempts < PlacementAttemptBudget)
        {
            attempts++;
            if (TryPlaceOne(builder))
            {
                attempts = 0; // 成功一次就重置预算,预算是防"连续失败"而非防"总次数"
            }
        }

        return Emit(builder, dial, difficulty);
    }

    private bool TryPlaceOne(CrosswordGrid builder)
    {
        // 只从"还不是交叉格"的格子长出新词:一格最多参与两条成语,横竖各一。
        var anchors = builder.Words
            .SelectMany(w => w.Cells().Select(c => (Word: w, Cell: c)))
            .Where(x => builder.WordCountAt(x.Cell) == 1)
            .OrderBy(x => x.Cell.Row)
            .ThenBy(x => x.Cell.Col)
            .ToList();

        if (anchors.Count == 0)
        {
            return false;
        }

        Shuffle(anchors);

        foreach (var (word, cell) in anchors)
        {
            var newDirection = word.Direction == CrosswordDirection.Horizontal
                ? CrosswordDirection.Vertical
                : CrosswordDirection.Horizontal;

            var ch = builder.Cells[cell];

            var positions = new List<int> { 0, 1, 2, 3 };
            Shuffle(positions);

            foreach (var position in positions)
            {
                if (!_index.TryGetValue((ch, position), out var bucket))
                {
                    continue;
                }

                var candidates = bucket.ToList();
                Shuffle(candidates);

                foreach (var candidate in candidates)
                {
                    // 同一条成语在一张网格里只出现一次 —— 重复会让"答对一条"的反馈含义不明。
                    if (builder.Words.Any(w => w.Idiom.Word == candidate.Word))
                    {
                        continue;
                    }

                    var (row, col) = newDirection == CrosswordDirection.Vertical
                        ? (cell.Row - position, cell.Col)
                        : (cell.Row, cell.Col - position);

                    var trial = new PlacedWord(
                        builder.Words.Count, candidate, row, col, newDirection);

                    if (builder.CanPlace(trial, cell))
                    {
                        builder.Place(candidate, row, col, newDirection);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private GeneratedLevel Emit(CrosswordGrid builder, DifficultyDial dial, int difficulty)
    {
        // 归一化坐标,让网格左上角落在 (0,0)。
        var minRow = builder.Cells.Keys.Min(c => c.Row);
        var minCol = builder.Cells.Keys.Min(c => c.Col);

        CrosswordCell Shift(CrosswordCell c) => new(c.Row - minRow, c.Col - minCol);

        var cells = builder.Cells.Keys
            .Select(Shift)
            .OrderBy(c => c.Row).ThenBy(c => c.Col)
            .ToList();

        var rows = cells.Max(c => c.Row) + 1;
        var cols = cells.Max(c => c.Col) + 1;

        // 预填格优先取交叉格 —— 与原型一致,交叉点是最有价值的立足点(一格解开两条线索)。
        var intersections = builder.Cells.Keys
            .Where(c => builder.WordCountAt(c) > 1)
            .OrderBy(c => c.Row).ThenBy(c => c.Col)
            .ToList();
        Shuffle(intersections);

        var givenSource = intersections.Take(dial.GivenCount).ToList();
        if (givenSource.Count < dial.GivenCount)
        {
            var rest = builder.Cells.Keys
                .Except(givenSource)
                .OrderBy(c => c.Row).ThenBy(c => c.Col)
                .ToList();
            Shuffle(rest);
            givenSource.AddRange(rest.Take(dial.GivenCount - givenSource.Count));
        }

        var given = givenSource
            .Select(c => new CrosswordGivenCell(
                c.Row - minRow, c.Col - minCol, builder.Cells[c].ToString()))
            .OrderBy(g => g.Row).ThenBy(g => g.Col)
            .ToList();

        var givenKeys = givenSource.ToHashSet();

        // 字盘 = 所有非预填格所需的字 + 干扰字,打乱。它揭示的是"有哪些字"而非
        // "哪个字放哪格" —— 后者才是谜题。
        var tray = builder.Cells
            .Where(kv => !givenKeys.Contains(kv.Key))
            .Select(kv => kv.Value.ToString())
            .ToList();

        var needed = tray.ToHashSet(StringComparer.Ordinal);
        var distractors = _distractorPool
            .Select(c => c.ToString())
            .Where(s => !needed.Contains(s))
            .ToList();
        Shuffle(distractors);
        tray.AddRange(distractors.Take(dial.DistractorCount));
        Shuffle(tray);

        var slots = builder.Words
            .Select(w =>
            {
                var head = Shift(w.Cells().First());
                return new CrosswordSlot(w.Index, head.Row, head.Col, w.Direction, w.Idiom.Word.Length);
            })
            .OrderBy(s => s.Index)
            .ToList();

        var layout = new CrosswordLayout(rows, cols, cells, given, tray, slots);

        var solutionCells = builder.Cells.ToDictionary(
            kv => CrosswordSolution.Key(kv.Key.Row - minRow, kv.Key.Col - minCol),
            kv => kv.Value.ToString(),
            StringComparer.Ordinal);

        var words = builder.Words
            .Select(w => new CrosswordSolvedWord(w.Index, w.Idiom.Word, w.Idiom.Explanation))
            .OrderBy(w => w.Index)
            .ToList();

        return new GeneratedLevel(
            layout, new CrosswordSolution(solutionCells, words), difficulty);
    }

    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

    private void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
