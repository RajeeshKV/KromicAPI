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

            var akgsmaPreMarketStartHour = ResolveAkgsmaPreMarketStartHour(startHour);
            var nextRun = GetNextRun(startHour, endHour, intervalMinutes, akgsmaPreMarketStartHour);
            if (nextRun.IsAkgsmaPreMarket)
            {
                logger.LogInformation(
                    "Next AKGSMA pre-market gold rate fetch scheduled in {Delay}. Pre-market window: {PreMarketStartHour}:00-{StartHour}:00 IST, interval: {IntervalMinutes} minutes.",
                    nextRun.Delay,
                    akgsmaPreMarketStartHour,
                    startHour,
                    intervalMinutes);
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
                if (nextRun.IsAkgsmaPreMarket)
                {
                    await goldRateService.FetchAkgsmaTodayAndStoreAsync(
                        sendRegularEmail: true,
                        sendLowestAlert: true,
                        stoppingToken);
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
                logger.LogError(ex, nextRun.IsAkgsmaPreMarket
                    ? "Scheduled AKGSMA pre-market gold rate fetch failed."
                    : "Scheduled gold rate fetch failed.");
            }
        }
    }

    private int? ResolveAkgsmaPreMarketStartHour(int scheduleStartHour)
    {
        if (!_options.AkgsmaPreMarketFetchEnabled || !IsAkgsmaEndpoint(_options.Endpoint))
        {
            return null;
        }

        var preMarketStartHour = NormalizeHour(_options.AkgsmaPreMarketStartHour, fallback: 6);
        return preMarketStartHour < scheduleStartHour ? preMarketStartHour : null;
    }

    private static ScheduledRun GetNextRun(
        int startHour,
        int endHour,
        int intervalMinutes,
        int? akgsmaPreMarketStartHour)
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

            if (akgsmaPreMarketStartHour.HasValue)
            {
                var preMarketRun = GetNextRunInWindow(
                    now,
                    date,
                    akgsmaPreMarketStartHour.Value,
                    startHour,
                    intervalMinutes,
                    includeEndHour: false);
                if (preMarketRun.HasValue)
                {
                    return new ScheduledRun(ToDelay(preMarketRun.Value), IsAkgsmaPreMarket: true);
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
                return new ScheduledRun(ToDelay(regularRun.Value), IsAkgsmaPreMarket: false);
            }
        }

        var fallback = GetNextValidRunTime(now.AddDays(1), startHour);
        return new ScheduledRun(ToDelay(fallback), IsAkgsmaPreMarket: false);
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

    private static bool IsAkgsmaEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
        uri.Host.Contains("akgsma.com", StringComparison.OrdinalIgnoreCase);

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

    private sealed record ScheduledRun(TimeSpan Delay, bool IsAkgsmaPreMarket);
}