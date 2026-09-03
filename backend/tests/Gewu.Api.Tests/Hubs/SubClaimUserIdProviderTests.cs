using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Gewu.Api.Hubs;

namespace Gewu.Api.Tests.Hubs;

/// <summary>
/// `Clients.User(...)` 的收件人。
///
/// <para>
/// <b>这是本仓第一个 Api 层测试项目,而它存在的理由正是它测的这件事。</b>
/// <c>SignalRRoomNotifier.OpponentUrgedAsync</c> 用 <c>Clients.User(id)</c> 定向推送,
/// 而这条路上唯一会出错的地方 —— 「这条连接算哪个用户」 —— 住在 <c>Gewu.Api</c> 里,
/// 而在此之前没有任何测试项目引用得到它。缺陷的表现是**没有表现**:命令成功、领域状态改变、
/// 推送发给零个人、两端不报错。
/// </para>
/// <para>
/// <b>仍然欠着的那条:</b>「三条真实 SignalR 连接各自只收到自己那一份」的端到端扇出测试。
/// 它需要 <c>WebApplicationFactory</c> 起一个真 host,是另一笔账 —— 但它此前的触发条件写的是
/// 「存在一个 <c>Gewu.Api.Tests</c> 项目」,而**这个项目现在存在了**,所以那条触发条件已经
/// 触发,不能再靠「没有地方写」当理由。
/// </para>
/// </summary>
public class SubClaimUserIdProviderTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    [Fact]
    public void Reads_the_sub_claim()
    {
        var id = Guid.NewGuid().ToString();
        var provider = new SubClaimUserIdProvider();

        provider.GetUserIdFrom(PrincipalWith(new Claim(JwtRegisteredClaimNames.Sub, id)))
            .Should().Be(id);
    }

    /// <summary>
    /// 正面对照的另一半,也是这个缺陷本身的形状。
    /// <para>
    /// 平台在 <c>Program.cs</c> 第一行关掉了入站 claim 映射,所以 token 里**只有** <c>sub</c>;
    /// SignalR 默认那个 provider 读的是 <c>NameIdentifier</c>。这条测试钉住的是:只给
    /// <c>NameIdentifier</c> 而不给 <c>sub</c> 时,我们**也**认不出来 —— 换句话说,这两条 claim
    /// 是不同的东西,而这正是默认实现失效的原因。若哪天有人把映射打开,这条会红,而那是对的:
    /// 那时该重新决定读哪一条,而不是让两条都碰巧能用。
    /// </para>
    /// </summary>
    [Fact]
    public void Does_not_fall_back_to_NameIdentifier()
    {
        var provider = new SubClaimUserIdProvider();

        provider.GetUserIdFrom(
                PrincipalWith(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())))
            .Should().BeNull();
    }

    [Fact]
    public void Anonymous_connections_have_no_user_id()
    {
        new SubClaimUserIdProvider().GetUserIdFrom(null).Should().BeNull();
    }
}
