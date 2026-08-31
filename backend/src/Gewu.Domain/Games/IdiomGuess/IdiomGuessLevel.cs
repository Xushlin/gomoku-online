namespace Gewu.Domain.Games.IdiomGuess;

/// <summary>
/// 出题用的一条成语。
/// <para>
/// 与成语纵横的 <c>SourceIdiom</c> 分开,是因为本游戏多要一样东西:<see cref="Derivation"/>
/// —— 答对之后回传的那张纸条。合成一个共用类型会让纵横多带一个它从不读的字段。
/// </para>
/// </summary>
/// <param name="Word">成语本身,四字。</param>
/// <param name="Explanation">释义 —— 本游戏的**题面**。</param>
/// <param name="Derivation">出处;**可能没有**,全量里 9,615 条可用条目中有 252 条为空。</param>
public sealed record GuessSourceIdiom(string Word, string Explanation, string? Derivation);

/// <summary>
/// 一个难度档位的旋钮。
/// </summary>
/// <param name="PuzzleCount">本关出几条。</param>
/// <param name="BlankCount">每条挖几个字。</param>
/// <param name="MaxTier">取到第几层为止(1 = 只用最常用的那批)。</param>
public sealed record GuessDifficultyDial(int PuzzleCount, int BlankCount, int MaxTier);

/// <summary>
/// 下发给客户端的一道题。**不含答案** —— 被挖的位置在 <see cref="Chars"/> 里是 <c>null</c>。
/// </summary>
/// <param name="Index">本关内的序号。</param>
/// <param name="Explanation">释义。</param>
/// <param name="Chars">
/// 四个位置上的字;被挖的位置为 <c>null</c>。
/// <para>
/// 空位用 <c>null</c> 而不是空串:**一个合法值不得用来表示「不适用」**。空串在 JSON 里
/// 和"这一格的字碰巧是空字符串"长得一样,而 <c>null</c> 说的是实话。
/// </para>
/// </param>
public sealed record IdiomGuessPuzzle(int Index, string Explanation, IReadOnlyList<string?> Chars);

/// <summary>一关的布局 —— 可下发的那一半。</summary>
/// <param name="Puzzles">本关的题目。</param>
public sealed record IdiomGuessLayout(IReadOnlyList<IdiomGuessPuzzle> Puzzles);

/// <summary>
/// 一道题的答案。
/// <para>
/// <b>只存整条成语,不另存"被挖那几个字"。</b> 后者由 <c>Word</c> 与布局里的空位位置完全
/// 决定,单独存一份就是第二份真源 —— 而两份不一致时,表现是"答案对了却判错",没有任何
/// 断言会红。
/// </para>
/// </summary>
/// <param name="Index">与布局中的题目对应。</param>
/// <param name="Word">整条成语。</param>
/// <param name="Derivation">出处;没有则为 <c>null</c>。</param>
public sealed record IdiomGuessAnswer(int Index, string Word, string? Derivation);

/// <summary>一关的答案 —— 永不出服务端的那一半。</summary>
/// <param name="Puzzles">逐题答案。</param>
public sealed record IdiomGuessSolution(IReadOnlyList<IdiomGuessAnswer> Puzzles);

/// <summary>一个生成好的关卡。</summary>
/// <param name="Layout">下发给客户端的布局。</param>
/// <param name="Solution">服务端答案。</param>
/// <param name="Difficulty">难度档位。</param>
public sealed record GeneratedGuessLevel(
    IdiomGuessLayout Layout,
    IdiomGuessSolution Solution,
    int Difficulty);

/// <summary>玩家对某一道题的作答 —— 整条成语。</summary>
/// <param name="PuzzleIndex">题号。</param>
/// <param name="Word">玩家拼出来的整条成语。</param>
public sealed record IdiomGuessPartialSubmission(int PuzzleIndex, string? Word);

/// <summary>玩家的整关提交:题号 → 整条成语。</summary>
/// <param name="Words">逐题作答。</param>
public sealed record IdiomGuessSubmission(IReadOnlyDictionary<string, string>? Words);

/// <summary>答对一题时回传的载荷。</summary>
/// <param name="Index">题号。</param>
/// <param name="Word">这条成语。</param>
/// <param name="Derivation">出处;**没有就是 <c>null</c>**,客户端据此不画那张纸条。</param>
public sealed record IdiomGuessSolved(int Index, string Word, string? Derivation);

/// <summary>一次提示揭示的内容:某题某位置上的那一个字。</summary>
/// <param name="PuzzleIndex">题号。</param>
/// <param name="Position">位置 0–3。</param>
/// <param name="Char">那一个字。</param>
public sealed record IdiomGuessRevealed(int PuzzleIndex, int Position, string Char);

/// <summary>客户端上报的盘面状态,用来决定揭哪一格。</summary>
/// <param name="Selected">光标所在的空位,形如 <c>"2:1"</c>(题号:位置);可为 <c>null</c>。</param>
/// <param name="Filled">玩家已填的空位键,同样是 <c>"题号:位置"</c>。</param>
public sealed record IdiomGuessHintState(string? Selected, IReadOnlyList<string>? Filled);
