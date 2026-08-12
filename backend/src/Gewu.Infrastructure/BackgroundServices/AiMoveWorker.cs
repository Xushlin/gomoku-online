using Gomoku.Application.Abstractions;
using Gomoku.Application.Features.Bots.ExecuteBotMove;
using Gomoku.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gomoku.Infrastructure.BackgroundServices;

/// <summary>
/// 后台服务:循环轮询所有"状态 Playing 且当前回合玩家是 bot"的房间,
/// 为每个命中房间 dispatch 一条 <see cref="ExecuteBotMoveCommand"/>(再由其嵌套
/// <c>MakeMoveCommand</c>,走完整落子链路 —— 持久化 / ELO 更新 / SignalR 广播)。
/// <para>
/// 设计要点:
/// <list type="bullet">
/// <item>每轮 <see cref="IServiceScopeFactory.CreateScope"/> 取一个新的 <c>DbContext</c> scope,
///     避免跨轮次共享 EF tracking;</item>
/// <item>异常除 <see cref="OperationCanceledException"/> 外全部吞(日志记 Error),
///     让单次故障不会把 worker 整体拉倒;</item>
/// <item>"思考时间" <c>MinThinkTimeMs</c>:对手刚落子后太短时间内不让 bot 回应,
///     给人"bot 在思考"的观感。</item>
/// </list>
/// </para>
/// </summary>
public sealed class AiMoveWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AiOptions> _options;
    private readonly ILogger<AiMoveWorker> _logger;

    /// <inheritdoc />
    public AiMoveWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AiOptions> options,
        ILogger<AiMoveWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        _logger.LogInformation(
            "AiMoveWorker starting. PollIntervalMs={Poll} MinThinkTimeMs={Think}",
            opts.PollIntervalMs, opts.MinThinkTimeMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(opts.PollIntervalMs, stoppingToken);
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AiMoveWorker poll iteration failed; continuing.");
            }
        }

        _logger.LogInformation("AiMoveWorker stopping.");
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var opts = _options.Value;

        var pendingIds = await users.GetRoomsNeedingBotMoveAsync(ct);
        if (pendingIds.Count == 0)
        {
            return;
        }

        foreach (var roomId in pendingIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var room = await rooms.FindByIdAsync(roomId, ct);
                if (room is null || room.Status != Gomoku.Domain.Rooms.RoomStatus.Playing || room.Game is null)
                {
                    continue;
                }

                // 找出当前回合对应的 bot UserId
                var botId = room.Game.CurrentTurn == Stone.Black
                    ? room.BlackPlayerId
                    : room.WhitePlayerId!.Value;

                // 思考时间:距上一步不足 MinThinkTimeMs 则本轮跳过
                var lastMoveAt = room.Game.Moves.Count > 0
                    ? room.Game.Moves.OrderBy(m => m.Ply).Last().PlayedAt
                    : room.Game.StartedAt;
                var since = (clock.UtcNow - lastMoveAt).TotalMilliseconds;
                if (since < opts.MinThinkTimeMs)
                {
                    continue;
                }

                await sender.Send(new ExecuteBotMoveCommand(botId, roomId), ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 单个房间失败不应拖垮其它房间;下一轮自然重试。
                _logger.LogError(ex, "AiMoveWorker failed to process room {RoomId}", roomId.Value);
            }
        }
    }
}
