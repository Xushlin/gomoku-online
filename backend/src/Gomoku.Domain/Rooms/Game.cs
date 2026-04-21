using Gomoku.Domain.Entities;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Users;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Rooms;

/// <summary>
/// <see cref="Room"/> 聚合内的对局子实体,承载回合、Moves 列表、开始/结束时间、结果与胜方。
/// 盘面**不冗余存盘**:需要当前 <see cref="Board"/> 时由 <see cref="ReplayBoard"/> 从
/// <see cref="Moves"/> 按 Ply 升序 replay 得到。外部 MUST 通过 <see cref="Room"/> 的领域方法
/// 间接操作 <see cref="Game"/>。
/// </summary>
public sealed class Game
{
    private readonly List<Move> _moves = new();

    /// <summary>Game 子实体主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>所属房间 Id。</summary>
    public RoomId RoomId { get; private set; }

    /// <summary>开始时间(UTC)。</summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>结束时间(UTC);进行中为 <c>null</c>。</summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>对局结果;进行中为 <c>null</c>。</summary>
    public GameResult? Result { get; private set; }

    /// <summary>胜方用户 Id;进行中或平局时为 <c>null</c>。</summary>
    public UserId? WinnerUserId { get; private set; }

    /// <summary>
    /// 对局结束原因。进行中 <c>null</c>;结束后非 <c>null</c>,取值对应触发路径:
    /// <see cref="Room.PlayMove"/> 的连五 → <see cref="GameEndReason.Connected5"/>、
    /// <see cref="Room.Resign"/> → <see cref="GameEndReason.Resigned"/>、
    /// <see cref="Room.TimeOutCurrentTurn"/> → <see cref="GameEndReason.TurnTimeout"/>。
    /// </summary>
    public GameEndReason? EndReason { get; private set; }

    /// <summary>当前回合应该下的棋色。初始为 <see cref="Stone.Black"/>。</summary>
    public Stone CurrentTurn { get; private set; }

    /// <summary>
    /// 乐观并发令牌。SQLite 没有原生 rowversion,由 Domain 在每次状态变更后手动更新;
    /// EF 以 <c>IsConcurrencyToken</c> 形式使用,冲突时抛 <c>DbUpdateConcurrencyException</c>。
    /// </summary>
    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    /// <summary>按 Ply 排序的历史 Moves(只读视图 —— 外部 MUST NOT 修改)。</summary>
    public IReadOnlyCollection<Move> Moves => _moves;

    // EF 物化用。
    private Game() { }

    internal Game(RoomId roomId, DateTime startedAt)
    {
        Id = Guid.NewGuid();
        RoomId = roomId;
        StartedAt = startedAt;
        EndedAt = null;
        Result = null;
        WinnerUserId = null;
        EndReason = null;
        CurrentTurn = Stone.Black;
        RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void TouchRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();

    /// <summary>
    /// 从 <see cref="Moves"/> 按 Ply 升序 replay 得到当前 <see cref="Board"/>。
    /// 最多 225 步,复杂度 O(n),耗时亚毫秒级。
    /// </summary>
    public Board ReplayBoard()
    {
        var board = new Board();
        foreach (var m in _moves.OrderBy(x => x.Ply))
        {
            board.PlaceStone(new ValueObjects.Move(m.ToPosition(), m.Stone));
        }
        return board;
    }

    /// <summary>
    /// 在对局内记录一步棋(仅由 <see cref="Room.PlayMove"/> 调用)。更新 <see cref="CurrentTurn"/>。
    /// </summary>
    internal Move RecordMove(Position position, Stone stone, DateTime playedAt)
    {
        var nextPly = _moves.Count + 1;
        var move = new Move(Id, nextPly, position, stone, playedAt);
        _moves.Add(move);
        CurrentTurn = stone == Stone.Black ? Stone.White : Stone.Black;
        TouchRowVersion();
        return move;
    }

    /// <summary>
    /// 标记对局结束(仅由 <see cref="Room"/> 聚合的结束路径调用:
    /// <see cref="Room.PlayMove"/> 连五 / <see cref="Room.Resign"/> / <see cref="Room.TimeOutCurrentTurn"/>)。
    /// 调用方 MUST 显式传入 <paramref name="reason"/>,杜绝默认值意外出现。
    /// </summary>
    internal void FinishWith(GameResult result, UserId? winnerUserId, GameEndReason reason, DateTime endedAt)
    {
        Result = result;
        WinnerUserId = winnerUserId;
        EndReason = reason;
        EndedAt = endedAt;
        TouchRowVersion();
    }
}
