namespace Gomoku.Domain.Enums;

/// <summary>
/// 对局结束原因。底层整数值 MUST 保持稳定,以便序列化 / 数据库 / 配置文件的兼容性。
/// 未来若需增加(如 <c>Disconnected = 3</c> / <c>Surrendered = 4</c>),仅**追加**,不得重排现有值。
/// </summary>
public enum GameEndReason
{
    /// <summary>某方连成 5 子及以上(或双方下满而平局,亦归此类)。</summary>
    Connected5 = 0,

    /// <summary>某方通过 <c>POST /api/rooms/{id}/resign</c> 主动认输。</summary>
    Resigned = 1,

    /// <summary>当前回合玩家超过 <c>TurnTimeoutSeconds</c> 未落子,由 TurnTimeoutWorker 判负。</summary>
    TurnTimeout = 2,
}
