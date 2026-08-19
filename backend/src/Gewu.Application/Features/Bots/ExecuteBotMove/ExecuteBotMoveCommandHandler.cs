using Gewu.Application.Abstractions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Domain.Ai;
using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Rooms;
using Gewu.Domain.ValueObjects;
using MediatR;
using DomainMove = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Application.Features.Bots.ExecuteBotMove;

/// <summary>
/// 执行 AI 一步走子。由 <c>AiMoveWorker</c> 发,不对外暴露。Handler 做三件事:
/// <list type="number">
/// <item>防御式校验:Room 存在 / 处于 Playing / Bot 是玩家之一 / 轮到 Bot。</item>
/// <item>按 <see cref="BotAccountIds.TryGetDifficulty"/> 反推难度,经
///     <see cref="IGameAiRegistry"/> 取该房间棋种的工厂,构造 AI 实例。</item>
/// <item>从 Room.Game.Moves 的历史 replay 出当前 <see cref="Board"/>,
///     调 <see cref="IBoardGameAi.SelectMove"/>,再 <c>ISender.Send(new MakeMoveCommand(...))</c>。</item>
/// </list>
/// <para>
/// Handler 自己 **不** 调 <c>Room.PlayMove</c> 或 <c>IRoomNotifier</c>;所有副作用都走嵌套
/// <see cref="MakeMoveCommand"/> 管道(validator / handler / EF / notifier)一遍,保证路径单一。
/// </para>
/// </summary>
public sealed class ExecuteBotMoveCommandHandler : IRequestHandler<ExecuteBotMoveCommand, Unit>
{
    private readonly IRoomRepository _rooms;
    private readonly IGameRulesRegistry _rules;
    private readonly IGameAiRegistry _ai;
    private readonly IAiRandomProvider _random;
    private readonly ISender _sender;

    /// <inheritdoc />
    public ExecuteBotMoveCommandHandler(
        IRoomRepository rooms,
        IGameRulesRegistry rules,
        IGameAiRegistry ai,
        IAiRandomProvider random,
        ISender sender)
    {
        _rooms = rooms;
        _rules = rules;
        _ai = ai;
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

        var botSeat = room.SeatOf(request.BotUserId)
            ?? throw new NotAPlayerException(
                $"User {request.BotUserId.Value} is not a player in room {room.Id.Value}.");

        if (botSeat != room.Game.CurrentTurn)
        {
            throw new NotYourTurnException(
                $"Bot {request.BotUserId.Value} tried to move from seat {botSeat} but current turn is seat {room.Game.CurrentTurn}.");
        }

        // AI 这一侧仍然说棋色 —— 有 AI 的都是棋盘类棋种,而棋盘上那颗东西确实叫子。
        var botStone = BoardSeats.ToStone(botSeat);

        var difficulty = BotAccountIds.TryGetDifficulty(request.BotUserId.Value)
            ?? throw new ArgumentException(
                $"User {request.BotUserId.Value} is not a seeded bot account.",
                nameof(request));

        var rules = _rules.For(room.GameKey)
            ?? throw new RoomNotFoundException(
                $"Room '{room.Id.Value}' declares unknown game '{room.GameKey}'.");

        // AI 与规则各有一份注册表,两处都可能解析不出来,且都映射成同一个 404。
        // 分两个注册表是因为它们的注册单位不同:规则是"这个棋种怎么判胜",AI 是"这个棋种
        // 怎么思考" —— 一个棋种可以先有规则(人人对战)、后有 AI。这里的失败模式相同:
        // 房间指向一个本构建不认识的棋种。
        var aiFactory = _ai.For(room.GameKey)
            ?? throw new RoomNotFoundException(
                $"Room '{room.Id.Value}' declares game '{room.GameKey}', which has no AI.");

        // handler 不再造盘 —— 它把**历史**交给 AI,盘面怎么重建是那个棋种自己的事。
        // 此前这里要把规则 cast 成 INInARowRules 才拿得到 Board,而那条 cast 正是
        // 「AI 接缝仍然是落子类形状」的症状:象棋根本过不去。
        var ai = aiFactory.Create(difficulty, _random.Get());
        var pick = ai.SelectMove(room.Game.History(), botStone);

        await _sender.Send(
            new MakeMoveCommand(
                request.BotUserId, request.RoomId,
                // 机器人只下棋盘类棋种 —— AI 注册表按棋种解析,没有盘面的棋种解析不出工厂。
                pick.RequirePosition().Row, pick.RequirePosition().Col,
                pick.From?.Row, pick.From?.Col),
            cancellationToken);

        return Unit.Value;
    }
}
