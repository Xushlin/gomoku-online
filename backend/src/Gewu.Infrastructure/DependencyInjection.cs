using Gewu.Application.Abstractions;
using Gewu.Domain.Games.IdiomCrossword;
using Gewu.Domain.Puzzles;
using Gewu.Infrastructure.Ai;
using Gewu.Infrastructure.Authentication;
using Gewu.Infrastructure.BackgroundServices;
using Gewu.Infrastructure.Common;
using Gewu.Infrastructure.Persistence;
using Gewu.Infrastructure.Puzzles;
using Gewu.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gewu.Infrastructure;

/// <summary>Infrastructure 层 DI 注册入口。</summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 <c>AppDbContext</c>(SQLite)、仓储、UnitOfWork、密码哈希、JWT 服务、时钟。
    /// 绑定 <see cref="JwtOptions"/> 到配置节 <c>"Jwt"</c>。
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdiomRepository, IdiomRepository>();
        services.AddScoped<IdiomSeeder>();
        services.AddScoped<IPuzzleRepository, PuzzleRepository>();

        // puzzle-core 刻意不注册任何 IPuzzleRules —— 关卡类游戏各自注册自己的规则。
        // 在 成语纵横 落地前,注册表对任何 gameKey 都返回 null,handler 映射为 404。
        services.AddSingleton<IPuzzleRulesRegistry, PuzzleRulesRegistry>();

        // 成语纵横 —— 平台的第一个关卡类游戏。加一个关卡游戏就是这两行:
        // 一个 IPuzzleRules 实现 + 一处注册。
        services.AddSingleton<IPuzzleRules, IdiomCrosswordRules>();
        services.AddScoped<CrosswordLevelSeeder>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IAiRandomProvider, AiRandomProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<RoomsOptions>(configuration.GetSection("Rooms"));
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection("Ai"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GameOptions>()
            .Bind(configuration.GetSection("Game"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<AiMoveWorker>();
        services.AddHostedService<TurnTimeoutWorker>();

        return services;
    }
}
