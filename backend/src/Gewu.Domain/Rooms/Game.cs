using Gewu.Domain.Entities;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Enums;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Rooms;

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
    /// <see cref="Room.PlayMove"/> 规则判出结果 → <see cref="GameEndReason.Decided"/>、
    /// <see cref="Room.Resign"/> → <see cref="GameEndReason.Resigned"/>、
    /// <see cref="Room.TimeOutCurrentTurn"/> → <see cref="GameEndReason.TurnTimeout"/>。
    /// </summary>
    public GameEndReason? EndReason { get; private set; }

    /// <summary>
    /// 当前该出手的**座位号**,<c>0</c> 到 <c>SeatCount - 1</c>;初始为 <c>0</c>(先手座位)。
    /// <para>
    /// 此前是 <c>Stone</c>,而轮转是 <c>stone == Black ? White : Black</c> —— 那一行就是整个
    /// 两人假设。现在是 <c>(seat + 1) % seatCount</c>,而座位数由规则说。
    /// </para>
    /// </summary>
    public int CurrentTurn { get; private set; }

    /// <summary>
    /// 本局的**服务端侧对局设置**;不需要设置的棋种为 <c>null</c>。
    /// <para>
    /// **内核从不解释它。** 不读内容、不校验格式、不依赖长度 —— 它由规则造
    /// (<c>IDealtGameRules.CreateSetup</c>)、由规则读,对本类而言只是一段随本局存下来的字节。
    /// </para>
    /// <para>
    /// **它 MUST NOT 出现在任何 DTO 上。** 斗地主的设置就是三家的底牌 —— 与成语纵横
    /// 「答案不出服务端」是同一条平台规则:*客户端算不出来的东西,客户端就骗不了*。
    /// 将来每个座位各自收到自己那一份是另一件事;整份设置永远不出服务端。这一条由一条
    /// 反射断言强制(DTO 命名空间下不得有名字含 Setup 的成员),而不是靠记性。
    /// </para>
    /// <para>
    /// 不需要设置时是 <c>null</c> 而 MUST NOT 是 <c>""</c>:空字符串会让"这个棋种没有设置"
    /// 与"设置是空的"看起来一样。
    /// </para>
    /// </summary>
    public string? Setup { get; private set; }

    /// <summary>
    /// 乐观并发令牌。SQLite 没有原生 rowversion,由 Domain 在每次状态变更后手动更新;
    /// EF 以 <c>IsConcurrencyToken</c> 形式使用,冲突时抛 <c>DbUpdateConcurrencyException</c>。
    /// </summary>
    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    /// <summary>按 Ply 排序的历史 Moves(只读视图 —— 外部 MUST NOT 修改)。</summary>
    public IReadOnlyCollection<Move> Moves => _moves;

    /// <summary>先手座位号。五子棋的"黑先"、象棋的"红先"都是这一个座位的显示读法。</summary>
    public static readonly int FirstSeat = 0;

    // EF 物化用。
    private Game() { }

    internal Game(RoomId roomId, DateTime startedAt, string? setup)
    {
        Id = Guid.NewGuid();
        RoomId = roomId;
        StartedAt = startedAt;
        Setup = setup;
        EndedAt = null;
        Result = null;
        WinnerUserId = null;
        EndReason = null;
        CurrentTurn = FirstSeat;
        RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void TouchRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();

    /// <summary>
    /// 本局已走的全部步,按 Ply 升序,还原成规则看得懂的形状。
    /// <para>
    /// 这是 <c>IGameRules.Apply</c> 的入参。此前这里是 <c>ReplayBoard(rules)</c>,直接返回一块
    /// <c>Board</c> —— 那让本子实体知道了「盘面长什么样」,而象棋的盘面塞不进 <c>Board</c>。
    /// 现在它只交出**发生过什么**,盘面怎么重建是规则的私事。
    /// </para>
    /// </summary>
    public IReadOnlyList<PlayedMove> History()
        => _moves.OrderBy(x => x.Ply).Select(m => m.ToPlayedMove()).ToList();

    /// <summary>
    /// 在对局内记录一步棋(仅由 <see cref="Room.PlayMove"/> 调用)。更新 <see cref="CurrentTurn"/>。
    /// </summary>
    /// <param name="intent">这一步怎么走。</param>
    /// <param name="seat">走这一步的座位号。</param>
    /// <param name="seatCount">本棋种的座位数 —— 按环轮转要用。</param>
    /// <param name="nextSeat">
    /// 规则指定的下一手座位;<c>null</c> 表示按环轮转。绝大多数棋种、以及牌类棋种的绝大多数
    /// 手数,答案都是"轮转",所以那是默认。
    /// </param>
    /// <param name="playedAt">走这一步的时间(UTC)。</param>
    internal Move RecordMove(
        MoveIntent intent, int seat, int seatCount, int? nextSeat, DateTime playedAt)
    {
        var nextPly = _moves.Count + 1;
        var move = new Move(Id, nextPly, intent, seat, playedAt);
        _moves.Add(move);
        CurrentTurn = nextSeat ?? (seat + 1) % seatCount;
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
