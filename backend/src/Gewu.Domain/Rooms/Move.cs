using Gomoku.Domain.Enums;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Rooms;

/// <summary>
/// 对局中一步棋的持久化子实体。由 <see cref="Game"/> 在接受 <see cref="Room.PlayMove"/>
/// 成功后 append,外部不可直接构造。<c>Ply</c> 从 1 起,按时间严格递增。
/// </summary>
public sealed class Move
{
    /// <summary>子实体主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>所属 Game 的 Id。</summary>
    public Guid GameId { get; private set; }

    /// <summary>步数(1-based)。</summary>
    public int Ply { get; private set; }

    /// <summary>行索引(0–14)。</summary>
    public int Row { get; private set; }

    /// <summary>列索引(0–14)。</summary>
    public int Col { get; private set; }

    /// <summary>落子棋色(<see cref="Stone.Black"/> 或 <see cref="Stone.White"/>)。</summary>
    public Stone Stone { get; private set; }

    /// <summary>落子时刻(UTC)。</summary>
    public DateTime PlayedAt { get; private set; }

    // EF 物化用。
    private Move() { }

    internal Move(Guid gameId, int ply, Position position, Stone stone, DateTime playedAt)
    {
        Id = Guid.NewGuid();
        GameId = gameId;
        Ply = ply;
        Row = position.Row;
        Col = position.Col;
        Stone = stone;
        PlayedAt = playedAt;
    }

    /// <summary>返回该步的 <see cref="Position"/> 值对象(每次访问构造新实例)。</summary>
    public Position ToPosition() => new(Row, Col);
}
