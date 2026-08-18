using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Rooms;

/// <summary>
/// 对局中一步棋的持久化子实体。由 <see cref="Game"/> 在接受 <see cref="Room.PlayMove"/>
/// 成功后 append,外部不可直接构造。<c>Ply</c> 从 1 起,按时间严格递增。
/// <para>
/// 载荷有两种,**恰好一种**被填充:位置类是 <c>(FromRow, FromCol) -> (Row, Col)</c>(起点可空:
/// 落子类没有,走子类有),文本类是 <see cref="Text"/>(四个坐标列全空)。
/// </para>
/// <para>
/// **仍然不用 JSON 载荷列。** 上一版的理由是「象棋的每一步都恰好是 from -> to,真出现不规则
/// 走子时再加列」—— 成语接龙就是那个"真出现",而它要的恰好也只是**一个标量**。一列装得下,
/// 于是列仍然可查询、EF 原生映射、重放仍是强类型的:写错了是编译错误而不是运行时的
/// <c>JsonException</c>。JSON 会为一个还没有人提出的扩展性付钱。
/// </para>
/// <para>
/// 坐标列因此可空。**MUST NOT 用 <c>Row = 0, Col = 0</c> 表示「这一步没有格子」** —— 那与
/// <see cref="MoveIntent"/> 上明令禁止的「用一个合法值表示没有起点」是同一件事,只是换了字段。
/// </para>
/// </summary>
public sealed class Move
{
    /// <summary>子实体主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>所属 Game 的 Id。</summary>
    public Guid GameId { get; private set; }

    /// <summary>步数(1-based)。</summary>
    public int Ply { get; private set; }

    /// <summary>
    /// 起点行索引;**落子类棋种为 <c>null</c>**(五子棋 / 一字棋只有落点)。
    /// <para>
    /// 与 <see cref="FromCol"/> MUST 同为 <c>null</c> 或同为非 <c>null</c> —— 半个坐标不是坐标。
    /// </para>
    /// </summary>
    public int? FromRow { get; private set; }

    /// <summary>起点列索引;落子类棋种为 <c>null</c>。见 <see cref="FromRow"/>。</summary>
    public int? FromCol { get; private set; }

    /// <summary>终点 / 落点行索引;**文本类棋种为 <c>null</c>**。</summary>
    public int? Row { get; private set; }

    /// <summary>终点 / 落点列索引;文本类棋种为 <c>null</c>。见 <see cref="Row"/>。</summary>
    public int? Col { get; private set; }

    /// <summary>
    /// 文本载荷(成语接龙的一个成语);**位置类棋种为 <c>null</c>**。
    /// </summary>
    public string? Text { get; private set; }

    /// <summary>
    /// 出手的座位号,<c>0</c> 到 <c>SeatCount - 1</c>。
    /// <para>
    /// 此前这里是 <c>Stone</c>。内核不再知道一个棋种有几个人,也不再知道"黑白"是什么 ——
    /// 棋盘类棋种在**自己的规则内部**把座位 0/1 映回 <c>Stone.Black</c> / <c>Stone.White</c>。
    /// </para>
    /// </summary>
    public int Seat { get; private set; }

    /// <summary>落子时刻(UTC)。</summary>
    public DateTime PlayedAt { get; private set; }

    // EF 物化用。
    private Move() { }

    internal Move(Guid gameId, int ply, MoveIntent intent, int seat, DateTime playedAt)
    {
        Id = Guid.NewGuid();
        GameId = gameId;
        Ply = ply;
        // 载荷的合法性由 MoveIntent 的构造器已经保证过一次;这里照抄,不再各判一遍 ——
        // 同一条规则的第二份实现迟早与第一份不一致。
        FromRow = intent.From?.Row;
        FromCol = intent.From?.Col;
        Row = intent.To?.Row;
        Col = intent.To?.Col;
        Text = intent.Text;
        Seat = seat;
        PlayedAt = playedAt;
    }

    /// <summary>返回该步终点的 <see cref="Position"/>;文本类棋种为 <c>null</c>。</summary>
    public Position? ToPosition()
        => Row is int r && Col is int c ? new Position(r, c) : null;

    /// <summary>返回该步起点的 <see cref="Position"/>;落子类棋种为 <c>null</c>。</summary>
    public Position? FromPosition()
        => FromRow is int r && FromCol is int c ? new Position(r, c) : null;

    /// <summary>把本步还原成规则看得懂的形状,供 <c>IGameRules.Apply</c> 的历史使用。</summary>
    public PlayedMove ToPlayedMove() => new(FromPosition(), ToPosition(), Text, Seat);
}
