using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Gewu.Application.Common.DTOs;

namespace Gewu.Application.Tests.Common;

/// <summary>
/// 服务端**自己造**的对局设置 MUST NOT 出现在任何 DTO 上。
/// <para>
/// 斗地主的 <c>Game.Setup</c> 就是三家的底牌 —— 与成语纵横「答案不出服务端」是同一条平台规则:
/// *客户端算不出来的东西,客户端就骗不了*。将来每个座位**各自**收到自己那 17 张是另一件事;
/// 整份设置永远不出服务端。
/// </para>
/// <para>
/// <b>这条断言按自己的设计红过一次,而那次的答案是「这两件事不是同一件」。</b>
/// <c>play-from-position</c> 给 <c>RoomStateDto</c> 加了 <c>ChosenSetup</c> ——
/// 建房时**由调用方选定**的起始局面。它与上面那条规则的关系不是「例外」,是**前提不成立**:
/// </para>
/// <list type="bullet">
/// <item>发牌那份设置客户端**算不出来**,而那个「算不出来」就是安全性质本身;</item>
/// <item>选定那份设置**来自客户端自己的请求** —— 它递的是一个古谱线路 id,而
/// <c>GET /api/manuals/.../lines/{id}</c> 是 <c>[AllowAnonymous]</c> 的,同一个 90 字符盘面串
/// 匿名就能读到(研习页就是这么画的)。<b>不给它保护不了任何东西</b>,只是让客户端画不出
/// 那块残局。</item>
/// </list>
/// <para>
/// 所以修法**不是**把名字匹配放宽 —— 那会连它真正要拦的那件事一起放掉。修法是把豁免写成
/// 一份**恰好**的名单,并把它真正在乎的那条不变量搬到会咬人的地方:<c>GameSnapshotDto</c>
/// 上一个带 setup 的成员都不许有,而那正是发牌会落的地方。
/// </para>
/// <para>
/// **为什么是反射而不是行为测试。** 行为测试只能证明**今天**的投影没带上它,而一个字段会不会
/// 被序列化取决于它在不在 DTO 上 —— **一个不存在的成员没有明天。** 这与 <c>add-tetris</c>
/// 让"客户端自述的分数"无处可去用的是同一条断言:那里断言的是命令的公开成员不与一组名字相交。
/// </para>
/// </summary>
public class GameSetupStaysServerSideTests
{
    /// <summary>DTO 所在的程序集与命名空间 —— 用一个已知的 DTO 类型锚定,而不是写死字符串。</summary>
    private static readonly Assembly ApplicationAssembly = typeof(RoomStateDto).Assembly;

    private static readonly string DtoNamespace = typeof(RoomStateDto).Namespace!;

    [Fact]
    public void The_dto_namespace_is_actually_populated()
    {
        // **正控制。** 下面那条断言在"一个类型都没扫到"时同样会通过,而那种通过什么都没证明。
        // 这个仓库刚在 add-game-sounds 里付过这个账:一次 tsc 探针因为编译了零个文件而"通过",
        // 是正控制的**通过**暴露了它。
        DtoTypes().Should().HaveCountGreaterThan(10);
    }

    /// <summary>
    /// 带 setup 的 DTO 成员**恰好**是豁免名单里那些。
    /// <para>
    /// 「恰好」而不是「不含」:第二个 setup 名字出现的那天这条还会红,而那正是该问
    /// 「这一份客户端也算得出来吗」的时刻。
    /// </para>
    /// </summary>
    [Fact]
    public void The_only_setup_named_dto_member_is_the_one_the_client_already_knows()
    {
        var exposed = SetupNamedMembers(DtoTypes());

        exposed.Should().BeEquivalentTo(
            [$"{nameof(RoomStateDto)}.{nameof(RoomStateDto.ChosenSetup)}"],
            "选定式的起始局面来自客户端自己递的线路 id,而那条线路匿名就能读 —— "
                + "不给它保护不了任何东西。发牌那份走的是另一个字段,而那个字段一个 DTO 都没有。");
    }

    /// <summary>
    /// **发牌会落在 <c>Game.Setup</c> 上,所以那个 DTO 上一个 setup 都不许有。**
    /// <para>
    /// 这条与上面那条不重复:上面数的是全命名空间,而豁免名单一旦有人往里加一行,
    /// 这一条仍然会在「加的是 <c>GameSnapshotDto</c>」时红。名单会被改,不变量不会。
    /// </para>
    /// </summary>
    [Fact]
    public void The_game_snapshot_carries_no_setup_at_all()
    {
        SetupNamedMembers([typeof(GameSnapshotDto)])
            .Should().BeEmpty("一副牌就落在这里 —— 一个不存在的成员没有明天");
    }

    /// <summary>
    /// 这些类型上名字带 setup 的公开成员,写成 <c>类型.成员</c>。
    /// <para>
    /// **属性的 <c>get_</c> / <c>set_</c> 存取器排掉,普通方法不排。** 原来那条断言是
    /// <c>BeEmpty()</c>,存取器进不进来都一样;换成「恰好一份」之后,一个属性会变成三行,
    /// 而那是噪音,不是第二个口子。排的判据是 <c>IsSpecialName</c>,所以一个真的叫
    /// <c>GetSetup()</c> 的方法仍然会被数进来。
    /// </para>
    /// </summary>
    private static List<string> SetupNamedMembers(IEnumerable<Type> types) =>
        [.. types
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => new { Type = t, Member = m }))
            .Where(x => x.Member is not MethodBase { IsSpecialName: true })
            .Where(x => x.Member.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Type.Name}.{x.Member.Name}")
            .Distinct()];

    private static Type[] DtoTypes() =>
        [.. ApplicationAssembly.GetTypes().Where(t => t.Namespace == DtoNamespace)];
}
