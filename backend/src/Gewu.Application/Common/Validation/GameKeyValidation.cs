using FluentValidation;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Common.Validation;

/// <summary>
/// "棋种键必须已登记"这条校验规则的**唯一**定义处。
/// <para>
/// 两条建房路径(真人房 / AI 房)需要同一条规则。它被抽在这里而不是各写一遍,原因与
/// 规则本身要求走注册表、不许内联白名单是同一个:任何一份被复制的清单,迟早会与另一份
/// 不一致,而不一致的那一天不会有人发现。
/// </para>
/// </summary>
public static class GameKeyValidation
{
    /// <summary>
    /// 要求该字段是一个能在 <see cref="IGameRulesRegistry"/> 中解析出规则的棋种键。
    /// <para>
    /// 校验失败映射为 HTTP **400** 而不是 404:此刻房间还不存在,调用方送来的是一个本平台
    /// 没有的棋种,那是请求本身不合法。(对比落子路径的 404 —— 那里房间**确实存在**,
    /// 只是它的 <c>GameKey</c> 指向一个本构建不认识的棋种。)
    /// </para>
    /// <para>
    /// 必须在聚合被构造之前拦住:一个 <c>GameKey</c> 无人认识的 <c>Room</c> 一旦落库,
    /// 就再也玩不了了 —— 加入、落子、读状态全部解析不出规则,只能靠手工改数据修复。
    /// </para>
    /// </summary>
    /// <typeparam name="T">被校验的命令类型。</typeparam>
    /// <param name="rule">规则构造器。</param>
    /// <param name="registry">棋种规则注册表 —— 唯一的真源。</param>
    public static IRuleBuilderOptions<T, string> MustBeARegisteredGameKey<T>(
        this IRuleBuilder<T, string> rule, IGameRulesRegistry registry)
        => rule
            .NotEmpty().WithMessage("Game key is required.")
            .Must(key => !string.IsNullOrWhiteSpace(key) && registry.For(key) is not null)
            .WithMessage("'{PropertyValue}' is not a game on this platform.");
}
