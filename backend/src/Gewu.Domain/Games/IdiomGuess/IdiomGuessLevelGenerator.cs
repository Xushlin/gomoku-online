namespace Gewu.Domain.Games.IdiomGuess;

/// <summary>
/// 猜成语的关卡生成器。
/// <para>
/// **全部随机选择都来自构造时传入的种子** —— 没有 <c>Random.Shared</c>、没有时钟。候选在
/// 抽取前按成语排序,好让「同种子同词典 ⇒ 同产物」成立到逐字节,与成语纵横同一条纪律。
/// </para>
/// <para>
/// <b>它的主要工作不是随机,是拒绝。</b> 见 <see cref="BlankablePositions"/>。
/// </para>
/// </summary>
public sealed class IdiomGuessLevelGenerator
{
    private readonly List<GuessSourceIdiom> _corpus;
    private readonly Random _rng;

    /// <summary>用四字成语语料建库。</summary>
    /// <param name="corpus">可出题的四字成语(调用方已按层级过滤)。</param>
    /// <param name="seed">随机种子 —— 决定整批产物。</param>
    public IdiomGuessLevelGenerator(IEnumerable<GuessSourceIdiom> corpus, int seed)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        _corpus = corpus.OrderBy(i => i.Word, StringComparer.Ordinal).ToList();
        _rng = new Random(seed);
    }

    /// <summary>
    /// 这条成语里,哪些位置可以挖掉。
    /// <para>
    /// <b>判据只有一条:被挖的那个字 MUST NOT 出现在它自己的释义里。</b> 题面与答案同屏
    /// —— 释义就摆在空格旁边 —— 所以一个出现在释义里的字,等于把答案印在了题面上。
    /// </para>
    /// <para>
    /// 这不是洁癖,是量出来的:全量 12,580 条有释义的四字成语里,**2,914 条(23%)四个字
    /// 全都出现在自己的释义里**,一个都挖不了。抽三十条大概率一条都碰不上,而这个仓库
    /// 已经为「样本推出来的规则」付过两次账。
    /// </para>
    /// <para>
    /// 释义里直接含整条成语的(全量 51 条)在这里自然也被排除 —— 那种条目四个位置全落空。
    /// </para>
    /// </summary>
    /// <param name="idiom">一条成语。</param>
    /// <returns>可挖位置的下标,升序。</returns>
    public static IReadOnlyList<int> BlankablePositions(GuessSourceIdiom idiom)
    {
        ArgumentNullException.ThrowIfNull(idiom);

        var positions = new List<int>();
        for (var i = 0; i < idiom.Word.Length; i++)
        {
            if (!idiom.Explanation.Contains(idiom.Word[i]))
            {
                positions.Add(i);
            }
        }
        return positions;
    }

    /// <summary>
    /// 生成一个关卡。
    /// <para>
    /// 凑不满目标条数时按实际条数产出 —— 少一条可以,空转不行,与成语纵横同一条取舍。
    /// </para>
    /// </summary>
    /// <param name="dial">难度旋钮。</param>
    /// <param name="difficulty">写入关卡的难度档位。</param>
    /// <param name="used">跨关去重用的已出过的成语集合;调用方持有。</param>
    public GeneratedGuessLevel Generate(
        GuessDifficultyDial dial, int difficulty, ISet<string> used)
    {
        ArgumentNullException.ThrowIfNull(dial);
        ArgumentNullException.ThrowIfNull(used);

        // 够挖 dial.BlankCount 个空、且还没出过的条目。
        var candidates = _corpus
            .Where(i => !used.Contains(i.Word))
            .Where(i => BlankablePositions(i).Count >= dial.BlankCount)
            .ToList();

        var puzzles = new List<IdiomGuessPuzzle>();
        var answers = new List<IdiomGuessAnswer>();

        while (puzzles.Count < dial.PuzzleCount && candidates.Count > 0)
        {
            var pick = candidates[_rng.Next(candidates.Count)];
            candidates.Remove(pick);
            used.Add(pick.Word);

            var blankable = BlankablePositions(pick);
            var blanks = Choose(blankable, dial.BlankCount);

            var chars = new string?[pick.Word.Length];
            for (var i = 0; i < pick.Word.Length; i++)
            {
                chars[i] = blanks.Contains(i) ? null : pick.Word[i].ToString();
            }

            var index = puzzles.Count;
            puzzles.Add(new IdiomGuessPuzzle(index, pick.Explanation, chars));
            answers.Add(new IdiomGuessAnswer(index, pick.Word, pick.Derivation));
        }

        return new GeneratedGuessLevel(
            new IdiomGuessLayout(puzzles), new IdiomGuessSolution(answers), difficulty);
    }

    /// <summary>从可挖位置里随机取 n 个,升序返回。</summary>
    private HashSet<int> Choose(IReadOnlyList<int> from, int n)
    {
        var pool = from.ToList();
        var chosen = new HashSet<int>();
        while (chosen.Count < n && pool.Count > 0)
        {
            var at = _rng.Next(pool.Count);
            chosen.Add(pool[at]);
            pool.RemoveAt(at);
        }
        return chosen;
    }
}
