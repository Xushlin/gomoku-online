using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace Gewu.Api.Hubs;

/// <summary>
/// 把一条 SignalR 连接映射到用户 id,供 <c>Clients.User(...)</c> 定向推送。
/// <para>
/// <b>没有它,<c>Clients.User(...)</c> 会静默地发给零个人。</b> SignalR 默认的
/// <c>DefaultUserIdProvider</c> 读的是 <c>ClaimTypes.NameIdentifier</c>,而本项目在
/// <c>Program.cs</c> 第一行就写了 <c>JwtSecurityTokenHandler.DefaultMapInboundClaims = false</c>
/// —— 于是 token 里的 <c>sub</c> 保持原名,<c>NameIdentifier</c> 这条 claim <b>根本不存在</b>。
/// <c>NameClaimType = sub</c> 只设了 <c>Identity.Name</c>,不是同一条 claim。
/// </para>
/// <para>
/// <b>症状是没有症状。</b> hub 方法正常返回、命令正常执行、领域状态正常改变,推送寄给一个
/// 没人登记的地址,两端都不报错。催促(<c>UrgeReceived</c>)是平台上唯一走定向推送的事件,
/// 于是它自上线起就从未送达过任何人 —— web 端订阅了它、音效包里有它的声音,而它一次都没响过。
/// </para>
/// <para>
/// 这是被 Flutter 端的 <c>test/room_social_probe_test.dart</c> 量出来的:那个探针在写任何
/// 界面之前先证明传输,而它的写法是「正面断言 + 负面断言配对」—— 「只有被催方收到」这句话里的
/// 「只有」,在**谁都没收到**时也是成立的。**一条负面断言必须有一条证明机制活着的正面断言作
/// 前提**,否则它在功能完全消失时最绿。
/// </para>
/// </summary>
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    /// <summary>返回这条连接对应的用户 id(JWT 的 <c>sub</c>),未认证时返回 null。</summary>
    /// <param name="connection">SignalR 连接上下文。</param>
    /// <returns>用户 id 字符串,或 null。</returns>
    public string? GetUserId(HubConnectionContext connection) => GetUserIdFrom(connection.User);

    /// <summary>
    /// 从一个 principal 读用户 id。
    /// <para>
    /// 与上面那个方法分开,**因为值得测的是「读哪一条 claim」,不是「怎么拿到 principal」**。
    /// <see cref="HubConnectionContext"/> 在单元测试里几乎构造不出来,而把两件事绑在一起的
    /// 后果不是「测不了」,是「不测了」—— 这个缺陷此前正是没有任何测试项目够得着它。
    /// </para>
    /// </summary>
    /// <param name="user">连接上的身份,可能为 null。</param>
    /// <returns>用户 id 字符串,或 null。</returns>
    public string? GetUserIdFrom(System.Security.Claims.ClaimsPrincipal? user) =>
        user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
