using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Infrastructure.Persistence;
using RetroBoard.Infrastructure.Time;

namespace RetroBoard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres not configured");
        services.AddDbContext<BoardDbContext>(opt => opt.UseNpgsql(connStr));
        services.AddScoped<IBoardDbContext>(sp => sp.GetRequiredService<BoardDbContext>());
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
