using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Rooms;

/// <summary>
/// 对局中一步棋的持久化子实体。由 <see cref="Game"/> 在接受 <see cref="Room.PlayMove"/>
/// 成功后 append,外部不可直接构造。<c>Ply</c> 从 1 起,按时间严格递增。
/// <para>
/// 一步棋是 <c>(FromRow, FromCol) -> (Row, Col)</c>。起点可空:落子类棋种(五子棋 / 一字棋)
/// 没有起点,走子类(中国象棋)有。
/// </para>
/// <para>
/// **不用 JSON 载荷列。** 象棋的每一步都恰好是 from -> to(没有王车易位、吃过路兵、升变),
/// 两个可空列就覆盖了两类棋种,而且列**仍然可查询**、EF 原生映射、重放仍是强类型的 ——
/// 写错了是编译错误而不是运行时的 <c>JsonException</c>。真出现不规则走子时再加列。
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

    /// <summary>终点 / 落点行索引。</summary>
    public int Row { get; private set; }

    /// <summary>终点 / 落点列索引。</summary>
    public int Col { get; private set; }

    /// <summary>落子棋色(<see cref="Stone.Black"/> 或 <see cref="Stone.White"/>)。</summary>
    public Stone Stone { get; private set; }

    /// <summary>落子时刻(UTC)。</summary>
    public DateTime PlayedAt { get; private set; }

    // EF 物化用。
    private Move() { }

    internal Move(Guid gameId, int ply, MoveIntent intent, Stone stone, DateTime playedAt)
    {
        Id = Guid.NewGuid();
        GameId = gameId;
        Ply = ply;
        FromRow = intent.From?.Row;
        FromCol = intent.From?.Col;
        Row = intent.To.Row;
        Col = intent.To.Col;
        Stone = stone;
        PlayedAt = playedAt;
    }

    /// <summary>返回该步终点的 <see cref="Position"/> 值对象(每次访问构造新实例)。</summary>
    public Position ToPosition() => new(Row, Col);

    /// <summary>返回该步起点的 <see cref="Position"/>;落子类棋种为 <c>null</c>。</summary>
    public Position? FromPosition()
        => FromRow is int r && FromCol is int c ? new Position(r, c) : null;

    /// <summary>把本步还原成规则看得懂的形状,供 <c>IGameRules.Apply</c> 的历史使用。</summary>
    public PlayedMove ToPlayedMove() => new(FromPosition(), ToPosition(), Stone);
}
