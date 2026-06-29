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
            var intervalMinutes = NormalizeInterval(_options.ScheduleIntervalMinutes, fallback: 5);
            if (endHour < startHour)
            {
                logger.LogWarning(
                    "Gold rate schedule end hour {EndHour} is before start hour {StartHour}. Using 10:00-20:00 IST.",
                    _options.ScheduleEndHour,
                    _options.ScheduleStartHour);
                startHour = 10;
                endHour = 20;
            }

            var delay = GetDelayUntilNextRun(startHour, endHour, intervalMinutes);
            logger.LogInformation(
                "Next gold rate fetch scheduled in {Delay}. Active window: {StartHour}:00-{EndHour}:00 IST, interval: {IntervalMinutes} minutes.",
                delay,
                startHour,
                endHour,
                intervalMinutes);
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

    private static TimeSpan GetDelayUntilNextRun(int startHour, int endHour, int intervalMinutes)
    {
        var indiaTimeZone = GetIndiaTimeZone();
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);

        if (now.DayOfWeek == DayOfWeek.Sunday)
        {
            var nextRun = GetNextValidRunTime(now.AddDays(1), startHour);
            return nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
        }

        if (now.Hour < startHour)
        {
            var nextRun = GetNextValidRunTime(now, startHour);
            return nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
        }

        if (now.Hour > endHour)
        {
            var nextRun = GetNextValidRunTime(now.AddDays(1), startHour);
            return nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
        }

        var minuteBlock = now.Minute / intervalMinutes;
        var nextMinute = (minuteBlock + 1) * intervalMinutes;
        var candidateHour = now.Hour;

        if (nextMinute >= 60)
        {
            candidateHour++;
            nextMinute = 0;
        }

        if (candidateHour > endHour)
        {
            var nextRun = GetNextValidRunTime(now.AddDays(1), startHour);
            return nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
        }

        var nextRunCandidate = new DateTimeOffset(now.Year, now.Month, now.Day, candidateHour, nextMinute, 0, now.Offset);
        if (nextRunCandidate <= now)
        {
            nextRunCandidate = nextRunCandidate.AddMinutes(intervalMinutes);
        }

        if (nextRunCandidate.Hour > endHour || (nextRunCandidate.Hour == endHour && nextRunCandidate.Minute > 0))
        {
            var nextRun = GetNextValidRunTime(now.AddDays(1), startHour);
            return nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
        }

        return nextRunCandidate.ToUniversalTime() - DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset GetNextValidRunTime(DateTimeOffset date, int startHour)
    {
        var candidate = new DateTimeOffset(date.Year, date.Month, date.Day, startHour, 0, 0, date.Offset);
        if (candidate.DayOfWeek == DayOfWeek.Sunday)
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    private static int NormalizeHour(int hour, int fallback) =>
        hour is >= 0 and <= 23 ? hour : fallback;

    private static int NormalizeInterval(int intervalMinutes, int fallback) =>
        intervalMinutes is >= 1 and <= 60 ? intervalMinutes : fallback;

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