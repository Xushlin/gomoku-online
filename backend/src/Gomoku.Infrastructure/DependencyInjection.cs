using Gomoku.Application.Abstractions;
using Gomoku.Infrastructure.Authentication;
using Gomoku.Infrastructure.Common;
using Gomoku.Infrastructure.Persistence;
using Gomoku.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gomoku.Infrastructure;

/// <summary>Infrastructure 层 DI 注册入口。</summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 <c>GomokuDbContext</c>(SQLite)、仓储、UnitOfWork、密码哈希、JWT 服务、时钟。
    /// 绑定 <see cref="JwtOptions"/> 到配置节 <c>"Jwt"</c>。
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.");

        services.AddDbContext<GomokuDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<RoomsOptions>(configuration.GetSection("Rooms"));

        return services;
    }
}
