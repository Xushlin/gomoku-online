using Gomoku.Application.Abstractions;
using Gomoku.Application.Features.Rooms.TurnTimeout;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gomoku.Infrastructure.BackgroundServices;

/// <summary>
/// 后台服务:循环轮询所有"当前回合超过 <c>TurnTimeoutSeconds</c> 的 Playing 房间",
/// 为每个命中房间 dispatch 一条 <see cref="TurnTimeoutCommand"/>。handler 内部
/// 由 <c>Room.TimeOutCurrentTurn</c> 做最终超时校验(防 worker 与玩家落子的竞态),
/// 若 <see cref="Gomoku.Domain.Exceptions.TurnNotTimedOutException"/> 被抛则 worker 吞掉日志,下轮正常继续。
/// <para>
/// 与 <c>AiMoveWorker</c> 独立:两者 poll 频率、查询条件、处理逻辑不同。
/// </para>
/// </summary>
public sealed class TurnTimeoutWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<GameOptions> _options;
    private readonly ILogger<TurnTimeoutWorker> _logger;

    /// <inheritdoc />
    public TurnTimeoutWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<GameOptions> options,
        ILogger<TurnTimeoutWorker> logger)
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
            "TurnTimeoutWorker starting. TurnTimeoutSeconds={Sec} TimeoutPollIntervalMs={Poll}",
            opts.TurnTimeoutSeconds, opts.TimeoutPollIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(opts.TimeoutPollIntervalMs, stoppingToken);
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TurnTimeoutWorker poll iteration failed; continuing.");
            }
        }

        _logger.LogInformation("TurnTimeoutWorker stopping.");
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var opts = _options.Value;

        var pendingIds = await rooms.GetRoomsWithExpiredTurnsAsync(
            clock.UtcNow, opts.TurnTimeoutSeconds, ct);
        if (pendingIds.Count == 0)
        {
            return;
        }

        foreach (var roomId in pendingIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await sender.Send(new TurnTimeoutCommand(roomId), ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 单个房间失败不影响下一个;常见无害异常:对手刚落子推新了 lastActivity 导致
                // TurnNotTimedOutException(下轮查询不会再命中该房间)。
                _logger.LogInformation(
                    "TurnTimeoutWorker skipped room {RoomId}: {Message}",
                    roomId.Value, ex.Message);
            }
        }
    }
}
