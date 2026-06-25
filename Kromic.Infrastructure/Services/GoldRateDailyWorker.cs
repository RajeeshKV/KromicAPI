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
            logger.LogInformation("Gold rate scheduled job is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var startHour = NormalizeHour(_options.ScheduleStartHour, fallback: 10);
            var endHour = NormalizeHour(_options.ScheduleEndHour, fallback: 20);
            if (endHour < startHour)
            {
                logger.LogWarning(
                    "Gold rate schedule end hour {EndHour} is before start hour {StartHour}. Using 10:00-20:00 IST.",
                    _options.ScheduleEndHour,
                    _options.ScheduleStartHour);
                startHour = 10;
                endHour = 20;
            }

            var delay = GetDelayUntilNextRun(startHour, endHour);
            logger.LogInformation(
                "Next gold rate fetch scheduled in {Delay}. Active window: {StartHour}:00-{EndHour}:00 IST.",
                delay,
                startHour,
                endHour);
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
                logger.LogError(ex, "Scheduled gold rate fetch failed.");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextRun(int startHour, int endHour)
    {
        var indiaTimeZone = GetIndiaTimeZone();
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);

        for (var hour = startHour; hour <= endHour; hour++)
        {
            var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, now.Offset);
            if (candidate > now)
            {
                return candidate.ToUniversalTime() - DateTimeOffset.UtcNow;
            }
        }

        var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, startHour, 0, 0, now.Offset).AddDays(1);
        return nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
    }

    private static int NormalizeHour(int hour, int fallback) =>
        hour is >= 0 and <= 23 ? hour : fallback;

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