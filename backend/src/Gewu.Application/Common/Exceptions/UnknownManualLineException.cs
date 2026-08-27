using Gewu.Domain.Exceptions;

namespace Gewu.Application.Common.Exceptions;

/// <summary>
/// 建房时带的古谱线路 id 在库里没有对应记录。全局中间件映射为 HTTP **400**。
/// <para>
/// **400 而不是 404**,理由与 <c>MustBeARegisteredGameKey</c> 那条一字不差:此刻房间还不
/// 存在,一个 <c>POST /api/rooms</c> 回 404 说的是「/api/rooms 这个东西没有」,而实际情况是
/// 调用方递来的**请求体**里有一个本平台没有的东西。(对照:<c>GET</c> 一条不存在的线路
/// 确实是 404 —— 那里被请求的资源就是那条线路。)
/// </para>
/// <para>
/// 它存在的理由不是「validator 会漏」,而是**一个拒绝必须发生在房间造出来之前**:
/// 一条不存在的线路若被静静忽略,落地的是一局标准开局的象棋残局房 —— 而那和一局
/// 正常的棋在界面上完全一样,没有任何断言会红。
/// </para>
/// </summary>
public sealed class UnknownManualLineException : DomainException
{
    /// <inheritdoc />
    public UnknownManualLineException(string message) : base("unknown-manual-line", message)
    {
    }
}
