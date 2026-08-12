namespace Gomoku.Domain.Rooms;

/// <summary>
/// 聊天频道。<see cref="Room"/> 频道对房间内所有人可见(玩家 + 围观者);
/// <see cref="Spectator"/> 频道仅围观者互见 —— 玩家看不到围观者的吐槽。
/// 底层值固定,用于库迁移稳定性。
/// </summary>
public enum ChatChannel
{
    /// <summary>房间频道,玩家与围观者都可发、都可收。</summary>
    Room = 0,

    /// <summary>围观者频道,仅围观者可发,仅围观者可收。</summary>
    Spectator = 1,
}
