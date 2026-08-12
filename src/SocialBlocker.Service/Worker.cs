using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialBlocker.Core;

namespace SocialBlocker.Service;

/// <summary>
/// Runs continuously as a Windows Service (LocalSystem). Every 10 seconds it
/// checks the persisted config: if a session is active and not yet expired,
/// it re-applies the hosts-file block (self-healing if something reverted
/// it); if the session has expired, it lifts the block automatically.
/// Because this reads from disk on every tick rather than caching state in
/// memory, a reboot doesn't lose anything — the first tick after startup
/// picks the session back up exactly where it left off.
/// </summary>
public class Worker : BackgroundService
{
    private static readonly TimeSpan ReapplyInterval = TimeSpan.FromSeconds(10);

    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SocialBlocker enforcement service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during enforcement tick.");
            }

            try
            {
                await Task.Delay(ReapplyInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private void Tick()
    {
        var config = ConfigStore.Load();

        if (!config.Active)
        {
            return;
        }

        if (DateTime.UtcNow >= config.EndTimeUtc)
        {
            _logger.LogInformation("Block session expired — lifting blocks.");
            HostsFileManager.RemoveBlocks();
            config.Active = false;
            ConfigStore.Save(config);
            return;
        }

        HostsFileManager.ApplyBlocks(config.BlockedDomains);

        // Phase 4 hooks in here:
        // FirewallManager.ApplyRules(config.BlockedProcessNames);
        // ProcessWatchdog.KillBlocked(config.BlockedProcessNames);
    }
}
