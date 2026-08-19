using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Gewu.Application.Common.DTOs;

namespace Gewu.Application.Tests.Common;

/// <summary>
/// 服务端侧的对局设置 MUST NOT 出现在任何 DTO 上。
/// <para>
/// 斗地主的 <c>Game.Setup</c> 就是三家的底牌 —— 与成语纵横「答案不出服务端」是同一条平台规则:
/// *客户端算不出来的东西,客户端就骗不了*。将来每个座位**各自**收到自己那 17 张是另一件事;
/// 整份设置永远不出服务端。
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

    [Fact]
    public void No_dto_exposes_anything_named_setup()
    {
        var offenders = DtoTypes()
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => new { Type = t, Member = m }))
            .Where(x => x.Member.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Type.Name}.{x.Member.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "对局设置是服务端侧的秘密;它出现在 DTO 上就等于发给了客户端");
    }

    private static Type[] DtoTypes() =>
        [.. ApplicationAssembly.GetTypes().Where(t => t.Namespace == DtoNamespace)];
}
