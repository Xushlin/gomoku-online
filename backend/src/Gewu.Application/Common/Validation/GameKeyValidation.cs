using FluentValidation;
using Gewu.Domain.Ai;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Common.Validation;

/// <summary>
/// 棋种键校验规则的**唯一**定义处。
/// <para>
/// 两条建房路径(真人房 / AI 房)共享"必须已登记"这一条;"必须支持人人对战"只挂在真人房
/// 那条路径上。规则被抽在这里而不是各写一遍,原因与规则本身要求走注册表、不许内联白名单是
/// 同一个:任何一份被复制的清单,迟早会与另一份不一致,而不一致的那一天不会有人发现。
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

    /// <summary>
    /// 要求该棋种为人人对战开放,即其规则的 <see cref="IGameRules.SupportsHumanVsHuman"/> 为 true。
    /// <para>
    /// 这条规则存在的理由是那个字段的措辞:它的定义是「平台是否提供人人对战入口」。只要
    /// <c>POST /api/rooms</c> 接受某个棋种,平台就**确实**提供了一个入口 —— 客户端隐不隐藏
    /// 那个按钮无关紧要,谁都能直接调 API。声明与行为不一致时,不一致的是行为。
    /// </para>
    /// <para>
    /// 更要紧的是这个字段被当作**结构性事实**在承重:<c>IsRated ⇒ SupportsHumanVsHuman</c>
    /// 这条不变量之所以能把"一字棋不计分"从判断变成推论,靠的正是"它唯一的对手是机器人"。
    /// 没人强制的结构性事实只是另一个判断,而判断会过期且不报错。
    /// </para>
    /// <para>
    /// 只挂在真人房路径上。人机恰恰是这些棋种支持的玩法,在那边拦住等于把它们逐出平台。
    /// </para>
    /// <para>
    /// 键解析不出规则时本条**静默通过** —— 那种情况由 <see cref="MustBeARegisteredGameKey"/>
    /// 报出。同一个字段为同一件事报两条错误,只会让调用方以为要改两处。
    /// </para>
    /// </summary>
    /// <typeparam name="T">被校验的命令类型。</typeparam>
    /// <param name="rule">规则构造器。</param>
    /// <param name="registry">棋种规则注册表 —— 唯一的真源。</param>
    public static IRuleBuilderOptions<T, string> MustSupportHumanVsHuman<T>(
        this IRuleBuilder<T, string> rule, IGameRulesRegistry registry)
        => rule
            .Must(key => key is null || registry.For(key) is not { SupportsHumanVsHuman: false })
            .WithMessage("'{PropertyValue}' has no human-vs-human mode on this platform.");

    /// <summary>
    /// 要求该棋种真的有 AI,即 <see cref="IGameAiRegistry.For"/> 能解析出一个工厂。
    /// <para>
    /// 判据取自**注册表本身**,而不是 <see cref="IGameRules"/> 上一个手写的 <c>SupportsAi</c>
    /// 布尔。理由与 <c>IsRated</c> 当初被约束成不变量是同一条:一个复述结构性事实的手写布尔
    /// 是一个**判断**,而判断会过期且不报错。注册表就是那个事实 —— 哪天给某个棋种登记了 AI,
    /// 本条自动放行,没有第二处要记得改。
    /// </para>
    /// <para>
    /// <b>这条规则此前不存在,后果是一个计分漏洞而不只是一个多余的房间。</b> 实测:
    /// <c>POST /api/rooms/ai { gameKey: "idiom-chain", humanSide: White }</c> 返回 201,房间进入
    /// <c>Playing</c> 且轮到一个不存在的机器人;<c>AiMoveWorker</c> 每 1500 ms 抛一次
    /// <c>RoomNotFoundException</c>;60 秒后 <c>TurnTimeoutWorker</c> 判那个走不了的一方超时告负。
    /// 成语接龙计分,于是真人凭**零手棋**拿到一场胜利与约 +46 ELO,可无限重复。
    /// </para>
    /// <para>
    /// 只挂在 <b>AI 房</b>路径上。真人房不受约束 —— 成语接龙开放人人对战,那正是它该有的玩法。
    /// </para>
    /// <para>
    /// 键解析不出规则时本条**静默通过** —— 那种情况由 <see cref="MustBeARegisteredGameKey"/>
    /// 报出。同一个字段为同一件事报两条错误,只会让调用方以为要改两处。
    /// </para>
    /// </summary>
    /// <typeparam name="T">被校验的命令类型。</typeparam>
    /// <param name="rules">棋种规则注册表 —— 用来分辨"没这个棋"与"这个棋没有 AI"。</param>
    /// <param name="ai">AI 工厂注册表 —— "这个棋种会不会思考"的唯一真源。</param>
    public static IRuleBuilderOptions<T, string> MustHaveAnAi<T>(
        this IRuleBuilder<T, string> rule, IGameRulesRegistry rules, IGameAiRegistry ai)
        => rule
            .Must(key => key is null || rules.For(key) is null || ai.For(key) is not null)
            .WithMessage("'{PropertyValue}' has no computer opponent on this platform.");
}
