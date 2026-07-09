using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class WeeklySummaryWorker(
    KromicDbContext dbContext,
    IGoldRateService goldRateService,
    ITelegramService telegramService,
    ITransactionalEmailService emailService,
    ITelegramUserService telegramUserService,
    IGoldRateEmailSubscriptionService emailSubscriptionService,
    IOptions<GoldRateOptions> options,
    ILogger<WeeklySummaryWorker> logger) : BackgroundService
{
    private readonly GoldRateOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var indiaTime = TimeZoneInfo.ConvertTime(now, GetIndiaTimeZone());
                
                // Check if it's Sunday 10 AM IST
                if (indiaTime.DayOfWeek == DayOfWeek.Sunday && indiaTime.Hour == 10 && indiaTime.Minute == 0)
                {
                    logger.LogInformation("Starting weekly summary for {Date}", indiaTime.ToString("yyyy-MM-dd"));
                    await SendWeeklySummaryAsync(stoppingToken);
                    
                    // Wait until next minute to avoid running multiple times
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                
                // Check every minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in WeeklySummaryWorker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task SendWeeklySummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var indiaTimeZone = GetIndiaTimeZone();
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);
            var sevenDaysAgo = now.AddDays(-7);

            var history = await goldRateService.GetHistoryAsync(
                range: null,
                from: sevenDaysAgo,
                to: now,
                cancellationToken);

            if (history.Items == null || history.Items.Count == 0)
            {
                logger.LogInformation("No data available for weekly summary");
                return;
            }

            var rates = history.Items.Select(x => x.R22KT).ToList();
            var avgRate = rates.Average();
            var minRate = rates.Min();
            var maxRate = rates.Max();
            var firstRate = rates.Last();
            var lastRate = rates.First();
            var trend = lastRate > firstRate ? "📈 Up" : (lastRate < firstRate ? "📉 Down" : "➡️ Stable");
            var trendAmount = Math.Abs(lastRate - firstRate);

            var minRateItem = history.Items.OrderBy(x => x.R22KT).First();
            var maxRateItem = history.Items.OrderByDescending(x => x.R22KT).First();

            var minIstDate = TimeZoneInfo.ConvertTime(minRateItem.FetchedAt, indiaTimeZone);
            var maxIstDate = TimeZoneInfo.ConvertTime(maxRateItem.FetchedAt, indiaTimeZone);

            var message = $"<b>📊 Weekly Gold Rate Summary</b>\n" +
                          $"<i>{sevenDaysAgo:dd MMM yyyy} - {now:dd MMM yyyy}</i>\n\n" +
                          $"<b>Average Rate:</b> Rs. {avgRate:N2}\n" +
                          $"<b>Highest:</b> Rs. {maxRate:N2} ({maxIstDate:dd MMM})\n" +
                          $"<b>Lowest:</b> Rs. {minRate:N2} ({minIstDate:dd MMM})\n" +
                          $"<b>Weekly Trend:</b> {trend} ({trendAmount:N2})\n" +
                          $"<b>Current:</b> Rs. {lastRate:N2}";

            // Send to Telegram users
            var activeChatIds = await telegramUserService.GetActiveChatIdsAsync(cancellationToken);
            foreach (var chatId in activeChatIds)
            {
                try
                {
                    await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send weekly summary to chat {ChatId}", chatId);
                }
            }

            // Send to email subscribers
            var subscribers = await emailSubscriptionService.GetActiveSubscribersAsync(cancellationToken);
            foreach (var subscriber in subscribers)
            {
                try
                {
                    var subject = $"Weekly Gold Rate Summary - {now:dd MMM yyyy}";
                    var body = $"Weekly Gold Rate Summary\n\n" +
                               $"Period: {sevenDaysAgo:dd MMM yyyy} - {now:dd MMM yyyy}\n\n" +
                               $"Average Rate: Rs. {avgRate:N2}\n" +
                               $"Highest: Rs. {maxRate:N2} ({maxIstDate:dd MMM})\n" +
                               $"Lowest: Rs. {minRate:N2} ({minIstDate:dd MMM})\n" +
                               $"Weekly Trend: {trend} ({trendAmount:N2})\n" +
                               $"Current: Rs. {lastRate:N2}";

                    await emailService.SendWeeklySummaryEmailAsync(
                        subscriber.Email,
                        subscriber.Email,
                        subject,
                        "Weekly Gold Rate Summary",
                        body,
                        null,
                        null,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send weekly summary email to {Email}", subscriber.Email);
                }
            }

            logger.LogInformation("Weekly summary sent successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending weekly summary");
        }
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
