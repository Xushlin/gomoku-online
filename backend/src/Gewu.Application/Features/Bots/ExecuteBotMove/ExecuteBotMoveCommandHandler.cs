using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Features.Rooms.MakeMove;
using Gomoku.Domain.Ai;
using Gomoku.Domain.Entities;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Exceptions;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.ValueObjects;
using MediatR;
using DomainMove = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Application.Features.Bots.ExecuteBotMove;

/// <summary>
/// 执行 AI 一步走子。由 <c>AiMoveWorker</c> 发,不对外暴露。Handler 做三件事:
/// <list type="number">
/// <item>防御式校验:Room 存在 / 处于 Playing / Bot 是玩家之一 / 轮到 Bot。</item>
/// <item>按 <see cref="BotAccountIds.TryGetDifficulty"/> 反推难度,用 <see cref="GomokuAiFactory"/>
///     构造 AI 实例。</item>
/// <item>从 Room.Game.Moves 的历史 replay 出当前 <see cref="Board"/>,
///     调 <see cref="IGomokuAi.SelectMove"/>,再 <c>ISender.Send(new MakeMoveCommand(...))</c>。</item>
/// </list>
/// <para>
/// Handler 自己 **不** 调 <c>Room.PlayMove</c> 或 <c>IRoomNotifier</c>;所有副作用都走嵌套
/// <see cref="MakeMoveCommand"/> 管道(validator / handler / EF / notifier)一遍,保证路径单一。
/// </para>
/// </summary>
public sealed class ExecuteBotMoveCommandHandler : IRequestHandler<ExecuteBotMoveCommand, Unit>
{
    private readonly IRoomRepository _rooms;
    private readonly IAiRandomProvider _random;
    private readonly ISender _sender;

    /// <inheritdoc />
    public ExecuteBotMoveCommandHandler(
        IRoomRepository rooms,
        IAiRandomProvider random,
        ISender sender)
    {
        _rooms = rooms;
        _random = random;
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(ExecuteBotMoveCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        if (room.Status != RoomStatus.Playing || room.Game is null)
        {
            throw new RoomNotInPlayException($"Room '{room.Id.Value}' is not in play (status={room.Status}).");
        }

        Stone botStone;
        if (request.BotUserId == room.BlackPlayerId)
        {
            botStone = Stone.Black;
        }
        else if (room.WhitePlayerId is not null && request.BotUserId == room.WhitePlayerId.Value)
        {
            botStone = Stone.White;
        }
        else
        {
            throw new NotAPlayerException(
                $"User {request.BotUserId.Value} is not a player in room {room.Id.Value}.");
        }

        if (botStone != room.Game.CurrentTurn)
        {
            throw new NotYourTurnException(
                $"Bot {request.BotUserId.Value} tried to move as {botStone} but current turn is {room.Game.CurrentTurn}.");
        }

        var difficulty = BotAccountIds.TryGetDifficulty(request.BotUserId.Value)
            ?? throw new ArgumentException(
                $"User {request.BotUserId.Value} is not a seeded bot account.",
                nameof(request));

        // Replay Board from Moves 历史(与 Room.PlayMove 内部的 ReplayBoard 一致逻辑)
        var board = new Board();
        foreach (var m in room.Game.Moves.OrderBy(mv => mv.Ply))
        {
            board.PlaceStone(new DomainMove(new Position(m.Row, m.Col), m.Stone));
        }

        var ai = GomokuAiFactory.Create(difficulty, _random.Get());
        var pick = ai.SelectMove(board, botStone);

        await _sender.Send(
            new MakeMoveCommand(request.BotUserId, request.RoomId, pick.Row, pick.Col),
            cancellationToken);

        return Unit.Value;
    }
}
