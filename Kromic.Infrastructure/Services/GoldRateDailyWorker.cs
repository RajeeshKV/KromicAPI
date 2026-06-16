using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class GoldRateDailyWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<GoldRateOptions> options,
    ILogger<GoldRateDailyWorker> logger) : BackgroundService
{
    private readonly GoldRateOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DailyJobEnabled)
        {
            logger.LogInformation("Gold rate daily job is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            logger.LogInformation("Next gold rate fetch scheduled in {Delay}.", delay);
            await Task.Delay(delay, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var goldRateService = scope.ServiceProvider.GetRequiredService<IGoldRateService>();
                await goldRateService.FetchAndStoreAsync(
                    sendRegularEmail: true,
                    sendLowestAlert: true,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily gold rate fetch failed.");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextRun()
    {
        var indiaTimeZone = GetIndiaTimeZone();
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);
        var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, 10, 0, 0, now.Offset);

        if (now >= nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }

        // Skip Sundays (jewelry doesn't update on Sundays)
        while (nextRun.DayOfWeek == DayOfWeek.Sunday)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
    }

    private static TimeZoneInfo GetIndiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
    }
}
