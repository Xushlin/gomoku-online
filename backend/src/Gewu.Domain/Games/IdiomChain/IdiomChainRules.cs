using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Idioms;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.IdiomChain;

/// <summary>
/// 成语接龙 —— 平台第一个**不在盘面上进行**的对战棋种。
/// <para>
/// 一步棋是一个成语。它合法当且仅当三条同时成立:词典里有、首字接上一个成语的末字、
/// 本局没人说过。开局第一步不受第二条约束。
/// </para>
/// <para>
/// **规则永远不判出胜负。** 接龙没有终局局面 —— 一方答不上来才结束,而"答不上来"在
/// 时间上,不在盘面上。它由内核既有的两条非规则路径承接:认输,以及
/// <c>Room.TimeOutCurrentTurn</c>。这一点一行内核都不用改:<see cref="MoveApplication"/>
/// 当初就刻意没有 <c>EndReason</c>,理由正是「怎么结束的」有三类而规则只可能是其中一类。
/// </para>
/// <para>
/// 它**不实现** <see cref="IBoardGameRules"/>:没有行列可言。
/// </para>
/// <para>
/// 实例无状态。判定所需的一切都从 <c>Apply</c> 收到的历史里读出来 —— 词典是构造时注入的
/// 不可变数据,不是对局状态。
/// </para>
/// </summary>
public sealed class IdiomChainRules : IGameRules
{
    private readonly IIdiomLexicon _lexicon;

    /// <summary>构造成语接龙规则。</summary>
    /// <param name="lexicon">成语词典 —— 不可变、线程安全,见 <see cref="IIdiomLexicon"/>。</param>
    public IdiomChainRules(IIdiomLexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        _lexicon = lexicon;
    }

    /// <inheritdoc />
    public string GameKey => GameKeys.IdiomChain;

    /// <summary>
    /// 有人人对战 —— 这是平台加这个游戏的**理由**:它需要人类对手。
    /// </summary>
    /// <summary>
    /// 两个座位。成语接龙没有棋盘,所以它 MUST NOT 引用 <c>BoardSeats</c> —— 那是棋盘家族的词汇。
    /// </summary>
    public int SeatCount => 2;

    public bool SupportsHumanVsHuman => true;

    /// <summary>
    /// 计分。
    /// <para>
    /// 与前三个棋种不同,这一条是**判断**而不是不变量的推论,所以它需要一个写下来的理由:
    /// 这个棋种有真实的人类对手池,而胜负取决于词汇量,那是一种棋力。不变量
    /// <c>IsRated ⇒ SupportsHumanVsHuman</c> 允许它计分,但从未**要求**它计分。
    /// </para>
    /// <para>
    /// 它**没有 AI**,而这与计分是同一件事的两面:查词典就能写出一个近乎不可战胜的机器人,
    /// 而机器人对局是计分的 —— 阶梯排出来的就会是"谁刷机器人刷得多",一字棋正是因此不计分。
    /// 没有机器人可刷,本字段才立得住。它将来获得 AI 那天,这条理由要重新过一遍。
    /// </para>
    /// </summary>
    public bool IsRated => true;

    /// <inheritdoc />
    public MoveApplication Apply(
        MatchState state, MoveIntent intent, int seat)
    {
        // 成语接龙不看是谁出的手 —— 一个成语能不能接上,只取决于历史里的最后一个字。
        // 座位号在这里只是被接受、不被使用,而这正是"内核不该替规则决定这件事"的样子。
        _ = seat;

        // 位置类载荷在这里被挡下 —— 本棋种不在盘面上进行。
        // 这一条保持缺省的 invalid-move：它不是三条规则之一，而是送错了形状。
        var word = intent.RequireText();

        if (!_lexicon.Contains(word))
        {
            throw InvalidMoveException.IdiomNotFound(
                $"'{word}' is not an idiom in the dictionary.");
        }

        // 只按**字**接,不按读音。见本类下方的说明。
        if (LastWord(state.History) is { } previous && !LinksOnto(previous, word))
        {
            throw InvalidMoveException.IdiomDoesNotLink(
                $"'{word}' must start with '{LastCharOf(previous)}', the last character of '{previous}'.");
        }

        foreach (var played in state.History)
        {
            if (string.Equals(played.Text, word, StringComparison.Ordinal))
            {
                throw InvalidMoveException.IdiomAlreadyUsed(
                    $"'{word}' has already been played this game.");
            }
        }

        return MoveApplication.Ongoing();
    }

    /// <summary>上一个成语;开局为 <c>null</c>。</summary>
    /// <param name="history">本局已走的全部步。</param>
    private static string? LastWord(IReadOnlyList<PlayedMove> history)
        => history.Count == 0 ? null : history[^1].Text;

    /// <summary>
    /// <paramref name="word"/> 是否接得上 <paramref name="previous"/>。
    /// <para>
    /// **同音不算接上。** 按读音接(说 shuō → 硕 shuò)是常见家规,但它把分支因子翻倍、
    /// 把判定权从"两边都看得见的字"移到"客户端根本拿不到的音"上,而多音字意味着一条成语
    /// 可以有好几个"末音"。字是双方从文本本身就能核对的东西。将来要改,改的就是这一处比较。
    /// </para>
    /// </summary>
    /// <param name="previous">上一个成语。</param>
    /// <param name="word">这一步的成语。</param>
    private static bool LinksOnto(string previous, string word)
        => word.Length > 0 && previous.Length > 0 && word[0] == LastCharOf(previous);

    /// <summary>成语的末字。</summary>
    /// <param name="word">成语。</param>
    private static char LastCharOf(string word) => word[^1];
}
