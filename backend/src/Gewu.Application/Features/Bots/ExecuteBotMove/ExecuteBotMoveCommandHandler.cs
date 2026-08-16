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

        // AI 层吃的是 Board,那是连 N 子专有的表示 —— 所以这里要的是窄接口。
        // 走子类棋种(象棋)自带表示,它的 AI 不走这条路。
        if (rules is not INInARowRules boardRules)
        {
            throw new RoomNotFoundException(
                $"Room '{room.Id.Value}' declares game '{room.GameKey}', whose AI does not use a Board.");
        }

        // 盘面由规则从走子历史重建 —— 聚合只交出发生过什么。
        var board = boardRules.ReplayBoard(room.Game.History());

        var ai = aiFactory.Create(difficulty, _random.Get());
        var pick = ai.SelectMove(board, botStone);

        await _sender.Send(
            new MakeMoveCommand(request.BotUserId, request.RoomId, pick.Row, pick.Col),
            cancellationToken);

        return Unit.Value;
    }
}
