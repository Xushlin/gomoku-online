namespace Gewu.Domain.Tests.Enums;

public class GameResultTests
{
    [Theory]
    [InlineData(GameResult.Ongoing)]
    [InlineData(GameResult.Decided)]
    [InlineData(GameResult.Draw)]
    public void Enum_exposes_three_states(GameResult value)
    {
        Enum.IsDefined(typeof(GameResult), value).Should().BeTrue();
    }

    [Fact]
    public void Default_Is_Ongoing()
    {
        GameResult value = default;
        value.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void No_member_names_a_colour()
    {
        // **这一条防的不是打错字,是有人为了"方便"把颜色加回来。**
        //
        // 加回来的那一刻,`Board.PlaceStone` 就又能返回一个与它自己入参矛盾的值(它被告知了
        // `move.Stone`,却回答"另一方赢了"),而那种矛盾今天没有任何测试会红 —— 因为它
        // 表达不出来。这条断言守的是"表达不出来"这个性质本身。
        //
        // 顺带也是三座位的前提:一个带颜色的胜负取值只够表示两个座位。
        Enum.GetNames<GameResult>().Should().BeEquivalentTo(["Ongoing", "Decided", "Draw"]);
    }

    [Fact]
    public void Underlying_values_keep_draw_at_three()
    {
        // 底层值是**持久化格式**:`Games.Result` 存的就是这些数字。
        //
        // `Draw` 留在 3、`Decided` 复用 1,是为了让历史数据的重映射只有一条
        // (`2 → 1`,即旧的 `WhiteWin`)。改动这些数字要配一个数据迁移,所以钉住它们。
        ((int)GameResult.Ongoing).Should().Be(0);
        ((int)GameResult.Decided).Should().Be(1);
        ((int)GameResult.Draw).Should().Be(3);
    }
}
