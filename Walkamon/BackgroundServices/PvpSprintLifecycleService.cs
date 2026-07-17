using BLL.Interfaces;

namespace Walkamon.BackgroundServices;

public sealed class PvpSprintLifecycleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PvpSprintLifecycleService> _logger;
    public PvpSprintLifecycleService(IServiceScopeFactory scopeFactory, ILogger<PvpSprintLifecycleService> logger) { _scopeFactory = scopeFactory; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var scope = _scopeFactory.CreateScope(); await scope.ServiceProvider.GetRequiredService<IPvpSprintService>().ProcessDueWorkAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogError(ex, "PvP Sprint lifecycle processing failed."); }
        }
    }
}
