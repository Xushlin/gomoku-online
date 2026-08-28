using FluentAssertions;
using Gewu.Application.Abstractions;

namespace Gewu.Application.Tests.Common;

/// <summary>
/// **生产环境覆盖签名密钥的那个环境变量,名字必须是运行时真的会读的那个。**
/// <para>
/// 这条守的缺陷不是措辞:启动异常此前写着「Set environment variable
/// <c>GOMOKU_JWT__SIGNINGKEY</c>」,而 <c>Program.cs</c> **从未**调
/// <c>AddEnvironmentVariables("GOMOKU_")</c>。**实测过,两个方向都测:**
/// </para>
/// <list type="bullet">
/// <item>Production 下只设 <c>GOMOKU_JWT__SIGNINGKEY</c> → **抛出同一条异常**;</item>
/// <item>只设 <c>Jwt__SigningKey</c> → 正常启动,<c>Now listening on: …</c>。</item>
/// </list>
/// <para>
/// 所以照着那条消息做,得到的是**一模一样的失败**,而运维手上没有任何线索说明为什么。
/// 这与 <c>api-ops</c> 那条要求为 CORS 量出来的表是同一件事的第二半,而那条要求写的是
/// <b>MUST NOT 用 <c>GOMOKU_</c> 前缀</b> —— 于是这不是新决定,是把代码对齐到已有的规格。
/// </para>
/// <para>
/// <b>为什么断言在这里而不在 Api 层:</b> <c>Gewu.Api</c> 没有测试项目,那是刻意的
/// (见 <c>CLAUDE.md</c>)。所以名字从 <c>Program.cs</c> 里的一句字面量搬到了
/// <see cref="JwtOptions"/> 上的一个常量 —— **一句没有测试项目能碰到的字符串,
/// 就是一句没有任何东西守着的字符串。**
/// </para>
/// </summary>
public class JwtSigningKeyEnvVarTests
{
    /// <summary>
    /// **断言的是名字与配置路径的关系,不是把同一个字符串再抄一遍。**
    /// <para>
    /// 双下划线是 .NET 的层级分隔符,所以「这个环境变量能覆盖 <c>Jwt:SigningKey</c>」
    /// 等价于「把 <c>__</c> 换成 <c>:</c> 之后正好等于那条配置路径」。一条
    /// <c>Should().Be("Jwt__SigningKey")</c> 只会在有人改名时红,而它证明不了改成的
    /// 那个名字是**对的**。
    /// </para>
    /// </summary>
    [Fact]
    public void The_env_var_maps_onto_the_config_path_it_claims_to_override()
    {
        var configPath = JwtOptions.SigningKeyEnvironmentVariable.Replace("__", ":");

        configPath.Should().Be(
            $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}",
            "双下划线是 .NET 的层级分隔符 —— 换成冒号之后就该是它声称要覆盖的那条配置路径");
    }

    /// <summary>
    /// **没有前缀。** <c>Program.cs</c> 加的是默认的无前缀 provider,所以任何带前缀的名字
    /// 都会被静默忽略 —— 而「静默」正是这个缺陷活下来的原因。
    /// </summary>
    [Fact]
    public void The_env_var_carries_no_prefix()
    {
        JwtOptions.SigningKeyEnvironmentVariable.Should().StartWith(
            JwtOptions.SectionName,
            "带前缀的变量运行时读不到 —— api-ops 那条要求写着 MUST NOT 用 GOMOKU_ 前缀");

        JwtOptions.SigningKeyEnvironmentVariable.Should().NotContain(
            "GOMOKU", "实测:Production 下只设它,启动仍抛同一条异常");
    }
}
