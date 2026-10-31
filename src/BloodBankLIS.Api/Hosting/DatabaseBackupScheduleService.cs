using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Checks hourly whether a scheduled backup is due (<c>Backup:IntervalHours</c>, default 24;
/// 0 disables scheduled backups) and keeps the overdue alert in step.
/// </summary>
public sealed class DatabaseBackupScheduleService : BackgroundService
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseBackupScheduleService> _logger;
    private readonly DatabaseBackupOptions _options;

    public DatabaseBackupScheduleService(
        IServiceScopeFactory scopeFactory,
        DatabaseBackupOptions options,
        ILogger<DatabaseBackupScheduleService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Database backup schedule: every {Interval}h to {Folder}, keep {Keep}, overdue after {Overdue}h.",
            _options.IntervalHours, Path.GetFullPath(_options.Folder), _options.KeepCount, _options.OverdueHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var backups = scope.ServiceProvider.GetRequiredService<DatabaseBackupService>();
                if (await backups.IsScheduledBackupDueAsync(stoppingToken))
                {
                    var result = await backups.RunAsync(DatabaseBackupRule.ScheduledTrigger, stoppingToken);
                    if (DatabaseBackupRule.IsGood(new() { Succeeded = result.Succeeded, Verified = result.Verified }))
                    {
                        _logger.LogInformation("Scheduled backup {File} written and verified ({Bytes} bytes).", result.FileName, result.SizeBytes);
                    }
                    else
                    {
                        _logger.LogError("Scheduled backup failed: {Error}", result.Error ?? result.VerifyDetail);
                    }
                }

                await backups.SyncOverdueAlertAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup schedule check failed.");
            }

            try
            {
                await Task.Delay(CheckEvery, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
