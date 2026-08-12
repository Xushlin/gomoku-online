namespace Gomoku.Application.Abstractions;

/// <summary>
/// Room / gameplay / chat 的可调参数。在 <c>appsettings.json</c> 的 <c>"Rooms"</c> 节绑定,
/// 通过 <c>IOptions&lt;RoomsOptions&gt;</c> 注入到 Handler。Domain 层不直接依赖,而是通过
/// 接收纯常量参数(例如 <c>Room.UrgeOpponent(..., int cooldownSeconds)</c>)使用。
/// </summary>
public sealed class RoomsOptions
{
    /// <summary>房间名最大长度(与 Domain 中的常量协同)。</summary>
    public int MaxRoomNameLength { get; set; } = 50;

    /// <summary>催促冷却秒数。</summary>
    public int UrgeCooldownSeconds { get; set; } = 30;

    /// <summary>聊天内容最大长度。</summary>
    public int MaxChatContentLength { get; set; } = 500;

    /// <summary>终局房间保留分钟数(供未来清理作业使用)。</summary>
    public int FinishedRoomRetentionMinutes { get; set; } = 30;
}
