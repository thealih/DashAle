using PriceTracker.Application.Features.Monitoring;

namespace PriceTracker.Api.BackgroundServices;

public sealed class MonitoringHostedService(IServiceScopeFactory scopes, ILogger<MonitoringHostedService> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        RunAsync("prices", TimeSpan.FromSeconds(10), (worker, ct) => worker.RunPricesAsync(ct), stoppingToken),
        RunAsync("discovery", TimeSpan.FromSeconds(10), (worker, ct) => worker.RunDiscoveryAsync(ct), stoppingToken),
        RunAsync("maintenance", TimeSpan.FromMinutes(1), (worker, ct) => worker.RunMaintenanceAsync(ct), stoppingToken));

    private async Task RunAsync(string name, TimeSpan interval, Func<MonitoringWorker, CancellationToken, Task> run,
        CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await run(scope.ServiceProvider.GetRequiredService<MonitoringWorker>(), ct);
                }
                catch (Exception error) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(error, "Monitoring loop {Loop} failed; will retry on the next cycle.", name);
                }
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
