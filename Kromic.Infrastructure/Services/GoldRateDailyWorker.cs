using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class GoldRateDailyWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<GoldRateOptions> options,
    IMemoryCache memoryCache,
    ILogger<GoldRateDailyWorker> logger) : BackgroundService
{
    private readonly GoldRateOptions _options = options.Value;
    private readonly IMemoryCache _memoryCache = memoryCache;

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

            var failoverTriggerHour = NormalizeHour(_options.FailoverTriggerHour, fallback: 9);
            var failoverTriggerMinute = NormalizeInterval(_options.FailoverTriggerMinute, fallback: 30);
            var nextRun = GetNextRun(startHour, endHour, intervalMinutes, failoverTriggerHour, failoverTriggerMinute);
            if (nextRun.IsFailover)
            {
                logger.LogInformation(
                    "Failover trigger scheduled in {Delay}. Backup providers will be queried at {Hour}:{Minute} IST.",
                    nextRun.Delay,
                    failoverTriggerHour,
                    failoverTriggerMinute);
            }
            else
            {
                logger.LogInformation(
                    "Next gold rate fetch scheduled in {Delay}. Active window: {StartHour}:00-{EndHour}:00 IST, interval: {IntervalMinutes} minutes.",
                    nextRun.Delay,
                    startHour,
                    endHour,
                    intervalMinutes);
            }

            await Task.Delay(nextRun.Delay, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var goldRateService = scope.ServiceProvider.GetRequiredService<IGoldRateService>();
                if (nextRun.IsFailover)
                {
                    var hasSnapshot = await goldRateService.HasSnapshotForTodayAsync(stoppingToken);
                    var failoverRanToday = _memoryCache.TryGetValue($"failover:ran:{DateTime.UtcNow:yyyy-MM-dd}", out _);
                    if (!hasSnapshot && !failoverRanToday)
                    {
                        await goldRateService.FetchFromBackupProvidersAsync(
                            sendRegularEmail: true,
                            sendLowestAlert: true,
                            stoppingToken);
                        _memoryCache.Set($"failover:ran:{DateTime.UtcNow:yyyy-MM-dd}", true, TimeSpan.FromDays(1));
                    }
                    else
                    {
                        logger.LogDebug("Failover skipped — today's snapshot already exists or failover already ran.");
                    }
                }
                else
                {
                    await goldRateService.FetchAndStoreAsync(
                        sendRegularEmail: true,
                        sendLowestAlert: true,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, nextRun.IsFailover
                    ? "Failover backup provider fetch failed."
                    : "Scheduled gold rate fetch failed.");
            }
        }
    }

    private static ScheduledRun GetNextRun(
        int startHour,
        int endHour,
        int intervalMinutes,
        int failoverTriggerHour,
        int failoverTriggerMinute)
    {
        var indiaTimeZone = GetIndiaTimeZone();
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);

        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var date = now.Date.AddDays(dayOffset);
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            // Failover trigger: if past 09:30 IST and no snapshot yet, schedule failover
            if (dayOffset == 0)
            {
                var failoverTime = new DateTime(date.Year, date.Month, date.Day, failoverTriggerHour, failoverTriggerMinute, 0);
                if (now >= failoverTime && now < failoverTime.AddMinutes(5))
                {
                    var failoverDelay = ToDelay(failoverTime);
                    if (failoverDelay <= TimeSpan.Zero)
                    {
                        return new ScheduledRun(TimeSpan.Zero, IsAkgsmaPreMarket: false, IsFailover: true);
                    }
                }
            }

            var regularRun = GetNextRunInWindow(
                now,
                date,
                startHour,
                endHour,
                intervalMinutes,
                includeEndHour: true);
            if (regularRun.HasValue)
            {
                return new ScheduledRun(ToDelay(regularRun.Value), IsAkgsmaPreMarket: false, IsFailover: false);
            }
        }

        var fallback = GetNextValidRunTime(now.AddDays(1), startHour);
        return new ScheduledRun(ToDelay(fallback), IsAkgsmaPreMarket: false, IsFailover: false);
    }

    private static DateTimeOffset? GetNextRunInWindow(
        DateTimeOffset now,
        DateTime date,
        int startHour,
        int endHour,
        int intervalMinutes,
        bool includeEndHour)
    {
        var windowStart = new DateTimeOffset(date.Year, date.Month, date.Day, startHour, 0, 0, now.Offset);
        var windowEnd = new DateTimeOffset(date.Year, date.Month, date.Day, endHour, 0, 0, now.Offset);
        if (!includeEndHour)
        {
            windowEnd = windowEnd.AddTicks(-1);
        }

        if (now > windowEnd)
        {
            return null;
        }

        if (now < windowStart)
        {
            return windowStart;
        }

        var minuteBlock = now.Minute / intervalMinutes;
        var nextMinute = (minuteBlock + 1) * intervalMinutes;
        var candidateHour = now.Hour;

        if (nextMinute >= 60)
        {
            candidateHour++;
            nextMinute = 0;
        }

        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, candidateHour, nextMinute, 0, now.Offset);
        if (candidate <= now)
        {
            candidate = candidate.AddMinutes(intervalMinutes);
        }

        return candidate <= windowEnd ? candidate : null;
    }

    private static TimeSpan ToDelay(DateTimeOffset nextRun)
    {
        var delay = nextRun.ToUniversalTime() - DateTimeOffset.UtcNow;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
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

    private sealed record ScheduledRun(TimeSpan Delay, bool IsAkgsmaPreMarket, bool IsFailover);
}