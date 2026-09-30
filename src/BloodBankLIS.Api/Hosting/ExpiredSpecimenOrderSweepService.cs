using BloodBankLIS.Application.Specimens;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Unlinks expired specimens from pending crossmatch orders and cancels other
/// open orders that still use an expired specimen. Interval is
/// <c>Specimens:ExpirationSweepSeconds</c> (default 30; set 0 to disable).
/// </summary>
public sealed class ExpiredSpecimenOrderSweepService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredSpecimenOrderSweepService> _logger;
    private readonly int _intervalSeconds;

    public ExpiredSpecimenOrderSweepService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExpiredSpecimenOrderSweepService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("Specimens:ExpirationSweepSeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_intervalSeconds <= 0)
        {
            _logger.LogInformation(
                "Expired-specimen order sweep disabled (Specimens:ExpirationSweepSeconds={Interval}).",
                _intervalSeconds);
            return;
        }

        _logger.LogInformation("Expired-specimen order sweep running every {Seconds}s.", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sweep = scope.ServiceProvider.GetRequiredService<ExpiredSpecimenOrderService>();
                var result = await sweep.ProcessAsync(stoppingToken);
                if (result.SpecimensMarkedExpired > 0
                    || result.CrossmatchOrdersUnlinked > 0
                    || result.OrdersCancelled > 0
                    || result.OrdersLeftOpen > 0)
                {
                    _logger.LogInformation(
                        "Expired-specimen sweep marked {Specimens} specimen(s) expired, unlinked {Unlinked} crossmatch order(s), cancelled {Cancelled} order(s), left {LeftOpen} order(s) open because units are still reserved.",
                        result.SpecimensMarkedExpired,
                        result.CrossmatchOrdersUnlinked,
                        result.OrdersCancelled,
                        result.OrdersLeftOpen);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expired-specimen order sweep failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
