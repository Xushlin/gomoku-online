namespace Gomoku.Domain.Rooms;

/// <summary>
/// 房间主键的强类型包装值对象。内部承载一个 <see cref="Guid"/>,
/// 避免把"房间 ID"与其他 ID 类型混用。不可变,基于值相等。
/// </summary>
public readonly record struct RoomId(Guid Value)
{
    /// <summary>生成一个新的 <see cref="RoomId"/>。</summary>
    public static RoomId NewId() => new(Guid.NewGuid());
}
