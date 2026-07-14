using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Kromic.Infrastructure.Services;

public sealed class WeeklySummaryWorker(
    ITransactionalEmailService emailService,
    IOptions<BrevoOptions> brevoOptions,
    IOptions<GoldRateOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<WeeklySummaryWorker> logger) : BackgroundService
{
    private readonly GoldRateOptions _options = options.Value;
    private readonly BrevoOptions _brevoOptions = brevoOptions.Value;
    private static readonly TimeSpan WeeklySummaryStartTime = TimeSpan.FromHours(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var indiaTime = TimeZoneInfo.ConvertTime(now, GetIndiaTimeZone());
                
                if (indiaTime.DayOfWeek == DayOfWeek.Sunday && indiaTime.TimeOfDay >= WeeklySummaryStartTime)
                {
                    var sentKey = indiaTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (!await IsWeeklySummaryAlreadySentAsync(sentKey, stoppingToken))
                    {
                        logger.LogInformation("Starting weekly summary for {Date}", indiaTime.ToString("yyyy-MM-dd"));
                        var sent = await SendWeeklySummaryAsync(stoppingToken);
                        if (sent)
                        {
                            await MarkWeeklySummarySentAsync(sentKey, stoppingToken);
                        }
                    }

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

    private async Task<bool> SendWeeklySummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var goldRateService = scope.ServiceProvider.GetRequiredService<IGoldRateService>();
            var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramService>();
            var telegramUserService = scope.ServiceProvider.GetRequiredService<ITelegramUserService>();
            var emailSubscriptionService = scope.ServiceProvider.GetRequiredService<IGoldRateEmailSubscriptionService>();

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
                return false;
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
            var recipients = await ResolveWeeklyEmailRecipientsAsync(emailSubscriptionService, cancellationToken);
            if (recipients.Count == 0)
            {
                logger.LogWarning("Weekly summary skipped because no email recipients are configured.");
                return false;
            }

            foreach (var recipient in recipients)
            {
                try
                {
                    var subject = $"Weekly Gold Rate Summary - {now:dd MMM yyyy}";
                    var trendClass = trend.Contains("Up") ? "trend-up" : trend.Contains("Down") ? "trend-down" : "trend-stable";

                    await emailService.SendWeeklySummaryEmailStructuredAsync(
                        recipient.Email,
                        recipient.Name,
                        subject,
                        "Weekly Gold Rate Summary",
                        $"{sevenDaysAgo:dd MMM yyyy}",
                        $"{now:dd MMM yyyy}",
                        $"Rs. {avgRate:N2}",
                        $"Rs. {maxRate:N2}",
                        $"{maxIstDate:dd MMM yyyy}",
                        $"Rs. {minRate:N2}",
                        $"{minIstDate:dd MMM yyyy}",
                        trend,
                        $"Rs. {trendAmount:N2}",
                        trendClass,
                        $"Rs. {lastRate:N2}",
                        cancellationToken,
                        recipient.UnsubscribeUrl is null ? null : "Unsubscribe",
                        recipient.UnsubscribeUrl);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send weekly summary email to {Email}", recipient.Email);
                }
            }

            logger.LogInformation("Weekly summary sent successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending weekly summary");
            return false;
        }
    }

    private async Task<List<WeeklyRecipient>> ResolveWeeklyEmailRecipientsAsync(
        IGoldRateEmailSubscriptionService emailSubscriptionService,
        CancellationToken cancellationToken)
    {
        var recipients = new Dictionary<string, WeeklyRecipient>(StringComparer.OrdinalIgnoreCase);

        AddRecipient(recipients, _brevoOptions.OwnerEmail, _brevoOptions.OwnerName);
        AddRecipient(recipients, _options.RecipientEmail, _options.RecipientName);

        foreach (var email in _options.RecipientEmails)
        {
            AddRecipient(recipients, email, _options.RecipientName);
        }

        foreach (var email in _options.RecipientEmailsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddRecipient(recipients, email, _options.RecipientName);
        }

        var subscribers = await emailSubscriptionService.GetActiveSubscribersAsync(cancellationToken);
        foreach (var subscriber in subscribers)
        {
            AddRecipient(recipients, subscriber.Email, subscriber.Email, BuildUnsubscribeUrl(subscriber.UnsubscribeToken));
        }

        return recipients.Values.ToList();
    }

    private void AddRecipient(IDictionary<string, WeeklyRecipient> recipients, string? email, string? name, string? unsubscribeUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        recipients.TryAdd(normalizedEmail, new WeeklyRecipient(normalizedEmail, string.IsNullOrWhiteSpace(name) ? normalizedEmail : name.Trim(), unsubscribeUrl));
    }

    private async Task<bool> IsWeeklySummaryAlreadySentAsync(string sentKey, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KromicDbContext>();
        var setting = await dbContext.ApplicationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == LastWeeklySummarySentKey, cancellationToken);

        return string.Equals(setting?.Value, sentKey, StringComparison.OrdinalIgnoreCase);
    }

    private async Task MarkWeeklySummarySentAsync(string sentKey, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KromicDbContext>();
        var setting = await dbContext.ApplicationSettings
            .FirstOrDefaultAsync(x => x.Key == LastWeeklySummarySentKey, cancellationToken);

        if (setting == null)
        {
            setting = new Domain.Entities.ApplicationSettings
            {
                Key = LastWeeklySummarySentKey,
                Value = sentKey,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.ApplicationSettings.Add(setting);
        }
        else
        {
            setting.Value = sentKey;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string? BuildUnsubscribeUrl(string token)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/api/gold-rate-email-alerts/unsubscribe?token={Uri.EscapeDataString(token)}";
    }

    private const string LastWeeklySummarySentKey = "WeeklySummaryLastSentIstDate";

    private sealed record WeeklyRecipient(string Email, string Name, string? UnsubscribeUrl);

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
