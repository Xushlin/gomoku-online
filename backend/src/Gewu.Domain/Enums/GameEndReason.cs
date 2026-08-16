namespace Gewu.Domain.Enums;

/// <summary>
/// 对局结束原因。底层整数值 MUST 保持稳定,以便序列化 / 数据库 / 回放文件的兼容性。
/// 未来新增的原因(如 <c>Disconnected = 3</c> / <c>Surrendered = 4</c>),须**追加**,不得重排现有值。
/// </summary>
public enum GameEndReason
{
    /// <summary>
    /// 规则从局面判出了结果 —— 五子棋连五、一字棋三连或满盘和棋、象棋将死或困毙。
    /// <para>
    /// **本成员原名 <c>Connected5</c>。那个名字不是陈旧,是错的:** 它描述的是五子棋的胜利条件,
    /// 而本字段回答的问题是「这局怎么结束的」,答案只有三类 —— 规则判出 / 有人认输 / 时间到。
    /// 一字棋从上线第一天起就在给三连记录「Connected5」,象棋会给将死记录同一个词。
    /// </para>
    /// <para>
    /// 底层值保持 <c>0</c>,数据库存的是 int,**既有行不需要改写**;变的只有 JSON 线上的字符串。
    /// </para>
    /// </summary>
    Decided = 0,

    /// <summary>某方通过 <c>POST /api/rooms/{id}/resign</c> 主动认输。</summary>
    Resigned = 1,

    /// <summary>当前回合玩家超过 <c>TurnTimeoutSeconds</c> 未落子,由 TurnTimeoutWorker 判负。</summary>
    TurnTimeout = 2,
}
