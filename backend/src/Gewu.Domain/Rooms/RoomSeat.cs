using Gewu.Domain.Users;

namespace Gewu.Domain.Rooms;

/// <summary>
/// 房间里的一个座位:谁坐在第几号。
/// <para>
/// 在它之前,座位是 <c>Room</c> 上两个字段(<c>BlackPlayerId</c> 非空、<c>WhitePlayerId</c> 可空)。
/// 那是 <c>generalize-match-domain</c> 明确押过的注(「这两个字段**就是**两个座位」),
/// 而斗地主要三个 —— 于是那注到期。
/// </para>
/// <para>
/// 一张表而不是"再加一列":写死的上限披着通用化的外衣,四人局又要再加一列;而且三处
/// LINQ 都要按"这人是不是这房间的玩家"过滤,存成 JSON 就查不动
/// (<c>generalize-match-payload</c> 为同一条理由拒过一次 JSON 列)。
/// </para>
/// </summary>
public sealed class RoomSeat
{
    /// <summary>所属房间。</summary>
    public RoomId RoomId { get; private set; }

    /// <summary>座位号,<c>0</c> 到 <c>SeatCount - 1</c>。<c>0</c> 是先手。</summary>
    public int Index { get; private set; }

    /// <summary>坐在这个座位上的用户。空座位**不存行** —— 见类型注释。</summary>
    public UserId UserId { get; private set; }

    // EF 物化用。
    private RoomSeat() { }

    internal RoomSeat(RoomId roomId, int index, UserId userId)
    {
        RoomId = roomId;
        Index = index;
        UserId = userId;
    }
}
