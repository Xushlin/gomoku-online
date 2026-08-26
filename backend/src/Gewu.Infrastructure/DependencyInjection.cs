using Gewu.Application.Abstractions;
using Gewu.Domain.Ai;
using Gewu.Domain.Games.IdiomCrossword;
using Gewu.Domain.Games.Klotski;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Idioms;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.TicTacToe;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Infrastructure.Games;
using Gewu.Infrastructure.Idioms;
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
using Microsoft.Extensions.Logging;

namespace Gewu.Infrastructure;

/// <summary>Infrastructure 层 DI 注册入口。</summary>
public static class DependencyInjection
{
    /// <summary>成语纵横的 seeder 键。</summary>
    public const string IdiomCrosswordKey = "idiom-crossword";

    /// <summary>华容道的 seeder 键。</summary>
    public const string KlotskiKey = "klotski";

    /// <summary>《梅花谱》古谱键。</summary>
    public const string MeihuapuKey = "meihuapu";

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
        services.AddScoped<IXiangqiManualRepository, XiangqiManualRepository>();
        services.AddScoped<IScoreRunRepository, ScoreRunRepository>();

        // puzzle-core 刻意不注册任何 IPuzzleRules —— 关卡类游戏各自注册自己的规则。
        // 在 成语纵横 落地前,注册表对任何 gameKey 都返回 null,handler 映射为 404。
        services.AddSingleton<IPuzzleRulesRegistry, PuzzleRulesRegistry>();

        // 棋盘对抗棋种。加一个连 N 子棋种就是下面再来一行 —— 连规则类都不用写。
        // 一字棋是这句话的第一次兑现:它整个棋种就是 (3, 3, 3) 这三个数。
        // 从 BuiltInGameRules.All(...) 注册 —— 那是**唯一**的一份内置棋种清单。
        // 在这里逐个写 AddSingleton 会造出第二份,而两份清单迟早不一致。
        //
        // 清单现在是个函数:成语接龙的规则要一本词典,而词典要读库。整份注册表因此
        // 由一个工厂构造,词典在第一次需要规则时载入一次(约 3 万行单列读取),
        // 之后落子路径上零 I/O。
        services.AddSingleton<IIdiomLexicon>(DbIdiomLexiconFactory.Create);
        services.AddSingleton<IGameRulesRegistry>(sp =>
            new GameRulesRegistry(BuiltInGameRules.All(sp.GetRequiredService<IIdiomLexicon>())));

        // 棋种 AI。与规则分开注册,因为注册单位不同:规则是"怎么判胜",AI 是"怎么思考"
        // —— 一个棋种可以先有规则(人人对战)、后有 AI。成语接龙**故意**停在前一半。
        //
        // 清单在 BuiltInGameAis.All,与规则那份同理:这里逐个 AddSingleton、测试夹具再手写一份,
        // 是这个仓库已经修过两次的缺陷,而它第二次复发就在隔壁的 GomokuRules.AiRegistry 里。
        foreach (var factory in BuiltInGameAis.All)
        {
            services.AddSingleton(factory);
        }
        services.AddSingleton<IGameAiRegistry, GameAiRegistry>();

        // 关卡类游戏。加一个游戏就是这两行:一个 IPuzzleRules 实现 + 一处注册。
        // 关卡产物再各配一个 seeder —— seeder 是通用的,游戏键与路径是它的构造参数。
        services.AddSingleton<IPuzzleRules, IdiomCrosswordRules>();
        services.AddSingleton<IPuzzleRules, KlotskiRules>();

        services.AddKeyedScoped(IdiomCrosswordKey, (sp, _) => new PuzzleLevelSeeder(
            IdiomCrosswordKey,
            PuzzleLevelSeeder.IdiomCrosswordPath,
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ILogger<PuzzleLevelSeeder>>()));

        services.AddKeyedScoped(KlotskiKey, (sp, _) => new PuzzleLevelSeeder(
            KlotskiKey,
            PuzzleLevelSeeder.KlotskiPath,
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ILogger<PuzzleLevelSeeder>>()));

        // 古谱是**只读资料**,不是关卡也不是对局:没有 IPuzzleRules,也没有聚合根。
        // 它唯一的"规则"发生在播种那一次 —— 逐手过 XiangqiRules,不合法就拒绝整条线路。
        services.AddKeyedScoped(MeihuapuKey, (sp, _) => new XiangqiManualSeeder(
            MeihuapuKey,
            XiangqiManualSeeder.MeihuapuPath,
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ILogger<XiangqiManualSeeder>>()));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IAiRandomProvider, AiRandomProvider>();
        services.AddSingleton<ISeedProvider, SystemSeedProvider>();
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
