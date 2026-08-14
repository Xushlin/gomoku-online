
namespace Gewu.Domain.Games.IdiomCrossword;

/// <summary>审计结论。</summary>
/// <param name="Passed">是否通过。</param>
/// <param name="Failures">未通过的原因,通过时为空。</param>
public sealed record AuditResult(bool Passed, IReadOnlyList<string> Failures)
{
    /// <summary>通过。</summary>
    public static AuditResult Ok() => new(true, Array.Empty<string>());
}

/// <summary>
/// 对每个产出关卡的独立审计。
/// <para>
/// 生成器在摆放时已经校验过相邻不变式,这里再验一遍**是刻意的冗余**:那条性质最容易被
/// 一个看起来没问题的实现悄悄破坏,而它一旦被破坏,产出的谜题是"能解但读起来是乱码"
/// —— 这种坏法不会让任何测试自然失败,只会让玩家困惑。
/// </para>
/// <para>
/// 审计不通过的关卡 MUST NOT 被写入产物。
/// </para>
/// </summary>
public static class CrosswordAudit
{
    /// <summary>审计一个关卡。</summary>
    /// <param name="level">待审关卡。</param>
    /// <param name="dictionary">合法成语集合 —— 每个词槽必须落在其中。</param>
    public static AuditResult Check(GeneratedLevel level, ISet<string> dictionary)
    {
        var failures = new List<string>();
        var layout = level.Layout;
        var solution = level.Solution;

        var layoutCells = layout.Cells.ToHashSet();

        // 1. 布局与答案的格位集合必须完全一致 —— 任何一边多出一格都是 bug。
        if (layoutCells.Count != solution.Cells.Count)
        {
            failures.Add(
                $"layout has {layoutCells.Count} cells but solution has {solution.Cells.Count}");
        }

        foreach (var cell in layoutCells)
        {
            if (solution.CharAt(cell) is null)
            {
                failures.Add($"cell ({cell.Row},{cell.Col}) has no solution character");
            }
        }

        // 2. 每个词槽必须落在词典里,且逐字与答案一致。
        foreach (var slot in layout.Slots)
        {
            var word = solution.Words.FirstOrDefault(w => w.Index == slot.Index);
            if (word is null)
            {
                failures.Add($"slot {slot.Index} has no solution word");
                continue;
            }

            if (!dictionary.Contains(word.Word))
            {
                failures.Add($"slot {slot.Index} word '{word.Word}' is not in the dictionary");
            }

            if (word.Word.Length != slot.Length)
            {
                failures.Add(
                    $"slot {slot.Index} declares length {slot.Length} but word '{word.Word}' has {word.Word.Length}");
            }

            var spelled = string.Concat(slot.Cells().Select(c => solution.CharAt(c) ?? "?"));
            if (spelled != word.Word)
            {
                failures.Add(
                    $"slot {slot.Index} spells '{spelled}' from the grid but claims '{word.Word}'");
            }

            if (string.IsNullOrWhiteSpace(word.Explanation))
            {
                failures.Add($"slot {slot.Index} word '{word.Word}' has no explanation to show");
            }
        }

        // 3. 字盘必须够填满所有非预填格 —— 按**多重集合**比较,不是按去重后的集合:
        //    「一心一意」需要两个「一」,字盘只给一个就无解。
        var givenKeys = layout.Given
            .Select(g => CrosswordSolution.Key(g.Row, g.Col))
            .ToHashSet(StringComparer.Ordinal);

        var required = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, ch) in solution.Cells)
        {
            if (givenKeys.Contains(key))
            {
                continue;
            }
            required[ch] = required.GetValueOrDefault(ch) + 1;
        }

        var available = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ch in layout.Tray)
        {
            available[ch] = available.GetValueOrDefault(ch) + 1;
        }

        foreach (var (ch, count) in required)
        {
            if (available.GetValueOrDefault(ch) < count)
            {
                failures.Add(
                    $"tray provides {available.GetValueOrDefault(ch)}×'{ch}' but the grid needs {count}");
            }
        }

        // 4. 预填格必须公开正确的字。
        foreach (var g in layout.Given)
        {
            var expected = solution.CharAt(new CrosswordCell(g.Row, g.Col));
            if (expected != g.Char)
            {
                failures.Add(
                    $"given cell ({g.Row},{g.Col}) shows '{g.Char}' but the solution says '{expected}'");
            }
        }

        // 5. 相邻不变式:每个非交叉格都不得有已占用的正交邻格。
        var slotCells = layout.Slots.ToDictionary(s => s.Index, s => s.Cells().ToHashSet());
        foreach (var slot in layout.Slots)
        {
            var own = slotCells[slot.Index];
            foreach (var cell in own)
            {
                var isIntersection = layout.Slots
                    .Count(s => slotCells[s.Index].Contains(cell)) > 1;
                if (isIntersection)
                {
                    continue;
                }

                foreach (var neighbour in Neighbours(cell))
                {
                    if (own.Contains(neighbour))
                    {
                        continue;
                    }
                    if (layoutCells.Contains(neighbour))
                    {
                        failures.Add(
                            $"cell ({cell.Row},{cell.Col}) of slot {slot.Index} is orthogonally adjacent to occupied ({neighbour.Row},{neighbour.Col})");
                    }
                }
            }
        }

        // 6. 至少要有两条成语才成"纵横" —— 一条孤立的成语不是这个游戏。
        if (layout.Slots.Count < 2)
        {
            failures.Add($"level has only {layout.Slots.Count} slot(s); a crossword needs at least 2");
        }

        return failures.Count == 0 ? AuditResult.Ok() : new AuditResult(false, failures);
    }

    private static IEnumerable<CrosswordCell> Neighbours(CrosswordCell cell)
    {
        yield return new CrosswordCell(cell.Row - 1, cell.Col);
        yield return new CrosswordCell(cell.Row + 1, cell.Col);
        yield return new CrosswordCell(cell.Row, cell.Col - 1);
        yield return new CrosswordCell(cell.Row, cell.Col + 1);
    }
}
