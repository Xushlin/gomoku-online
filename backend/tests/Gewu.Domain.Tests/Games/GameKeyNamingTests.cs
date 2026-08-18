using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;

namespace Gewu.Domain.Tests.Games;

/// <summary>
/// 五子棋的键**就是** <c>gomoku</c>,而且它 MUST NOT 跟着平台改名一起被改掉。
/// <para>
/// <c>rename-gomoku-to-gewu</c> 把平台级的 <c>gomoku</c>(localStorage 键、hub 路径、
/// 日志文件名、JWT issuer)全改成了 <c>gewu</c>,而**游戏自己的名字一个都没动**。
/// </para>
/// <para>
/// 这条断言存在,是因为那次改名的第一版机械替换**真的误伤了它** —— <c>gomoku:</c> 这个模式
/// 命中了 TypeScript 对象字面量的键 <c>gomoku: { rows: 15, cols: 15 }</c>,把五子棋的盘面
/// 尺寸改没了。误伤在提交前被 diff 拦下,但下一次未必。
/// </para>
/// <para>
/// 键是**契约**:它进房间记录、进 API 路径、进前端注册表、进已落库的每一行 <c>Room</c>。
/// 改它不是改名,是数据迁移。
/// </para>
/// </summary>
public class GameKeyNamingTests
{
    [Fact]
    public void Gomoku_keeps_its_own_name_after_the_platform_rename()
    {
        GameKeys.Gomoku.Should().Be("gomoku");
    }

    [Fact]
    public void No_registered_game_key_was_renamed_to_gewu()
    {
        // "gewu" 是平台的名字,不是任何一款游戏的名字。
        foreach (var rules in BuiltInGameRules.All(IdiomLexicons.Small))
        {
            rules.GameKey.Should().NotContain("gewu", $"{rules.GameKey} is a game, not the platform");
        }
    }
}
