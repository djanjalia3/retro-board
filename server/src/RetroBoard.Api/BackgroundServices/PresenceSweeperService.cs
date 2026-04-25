using MediatR;
using RetroBoard.Application.Presence.Commands.SweepStalePresence;

namespace RetroBoard.Api.BackgroundServices;

public class PresenceSweeperService(IServiceScopeFactory scopeFactory, ILogger<PresenceSweeperService> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new SweepStalePresenceCommand(StaleAfter), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Presence sweep tick failed");
            }
            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
