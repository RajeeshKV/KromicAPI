using System.Net;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController(
    ITelegramUserService telegramUserService,
    ITelegramService telegramService,
    ITransactionalEmailService emailService,
    IGoldRateService goldRateService,
    IGoldRateEmailSubscriptionService emailSubscriptionService,
    IUserSettingsService userSettingsService,
    ILocalizationService localizationService,
    IMemoryCache memoryCache,
    IOptions<GoldRateOptions> goldRateOptions,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    private static readonly TimeSpan FeedbackCaptureWindow = TimeSpan.FromMinutes(10);
    private readonly GoldRateOptions _goldRateOptions = goldRateOptions.Value;

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook([FromBody] TelegramWebhookRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            logger.LogWarning("Received null webhook request");
            return Ok();
        }

        try
        {
            // Handle callback queries (menu button presses)
            if (request.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(request.CallbackQuery, cancellationToken);
                return Ok();
            }

            if (request.Message?.From?.Id > 0 && request.Message?.Chat?.Id > 0)
            {
                var chatId = request.Message.Chat.Id.ToString();
                var firstName = request.Message.From.FirstName;
                var lastName = request.Message.From.LastName;
                var username = request.Message.From.Username;
                var messageText = request.Message.Text ?? string.Empty;

                var isNewUser = (await telegramUserService.GetUserByChatIdAsync(chatId, cancellationToken)) == null;

                await telegramUserService.AddOrUpdateUserAsync(
                    chatId,
                    firstName,
                    lastName,
                    username,
                    cancellationToken);

                logger.LogInformation(
                    "Registered/updated Telegram user - ChatID: {ChatId}, Name: {Name}, Username: {Username}",
                    chatId,
                    $"{firstName} {lastName}".Trim(),
                    username);

                if (isNewUser)
                {
                    await SendLatestRateToUserAsync(chatId, cancellationToken, isNewUser);
                }

                if (messageText.StartsWith("/", StringComparison.Ordinal))
                {
                    await HandleCommandAsync(chatId, request.Message.From, messageText, cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(messageText))
                {
                    if (!await TryCompleteFeedbackAsync(chatId, request.Message.From, messageText, cancellationToken))
                    {
                        await TryCompleteEmailAlertsSubscriptionAsync(chatId, messageText, cancellationToken);
                    }
                }
            }

            if (request.MyChatMember?.From?.Id > 0 && request.MyChatMember?.Chat?.Id > 0)
            {
                var chatId = request.MyChatMember.Chat.Id.ToString();
                var status = request.MyChatMember.NewChatMember?.Status ?? "unknown";
                var firstName = request.MyChatMember.From.FirstName;
                var lastName = request.MyChatMember.From.LastName;
                var username = request.MyChatMember.From.Username;

                if (status == "member")
                {
                    var isNewUser = (await telegramUserService.GetUserByChatIdAsync(chatId, cancellationToken)) == null;

                    await telegramUserService.AddOrUpdateUserAsync(
                        chatId,
                        firstName,
                        lastName,
                        username,
                        cancellationToken);

                    logger.LogInformation(
                        "User joined bot - ChatID: {ChatId}, Status: {Status}",
                        chatId,
                        status);

                    await SendLatestRateToUserAsync(chatId, cancellationToken, isNewUser);
                }
                else if (status == "left" || status == "kicked")
                {
                    logger.LogInformation(
                        "User left/kicked bot - ChatID: {ChatId}, Status: {Status}",
                        chatId,
                        status);
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Telegram webhook");
            return Ok();
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var count = await telegramUserService.GetActiveChatCountAsync(cancellationToken);
        return Ok(new { activeUsers = count });
    }

    private async Task HandleCommandAsync(
        string chatId,
        Kromic.Application.DTOs.TelegramUserDTO fromUser,
        string messageText,
        CancellationToken cancellationToken)
    {
        var trimmed = messageText.Trim();
        var commandToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var command = commandToken.Split('@')[0].ToLowerInvariant();
        var payload = trimmed.Length > commandToken.Length ? trimmed[commandToken.Length..].Trim() : string.Empty;

        var settings = await userSettingsService.GetOrCreateAsync(chatId, cancellationToken);
        var language = settings.Language;

        if (command == "/start")
        {
            await SendWelcomeAsync(chatId, cancellationToken, language);
        }
        else if (command == "/currentrate")
        {
            await SendCurrentRateAsync(chatId, cancellationToken, language);
        }
        else if (command == "/lastonemonthrates")
        {
            await SendLastOneMonthRatesAsync(chatId, cancellationToken, language);
        }
        else if (command == "/emailalerts")
        {
            await StartEmailAlertsSubscriptionAsync(chatId, cancellationToken, language);
        }
        else if (command == "/feedback")
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                await StartFeedbackCaptureAsync(chatId, cancellationToken, language);
            }
            else
            {
                await SubmitFeedbackAsync(chatId, fromUser, payload, cancellationToken, language);
            }
        }
        else if (command == "/help")
        {
            await SendHelpAsync(chatId, cancellationToken, language);
        }
        else if (command == "/unsubscribeemail")
        {
            await UnsubscribeEmailAsync(chatId, cancellationToken, language);
        }
        else if (command == "/settings")
        {
            await SendSettingsAsync(chatId, cancellationToken, language);
        }
        else if (command == "/pause")
        {
            await PauseNotificationsAsync(chatId, cancellationToken, language);
        }
        else if (command == "/resume")
        {
            await ResumeNotificationsAsync(chatId, cancellationToken, language);
        }
        else if (command == "/highestlowest")
        {
            await SendHighestLowestAsync(chatId, cancellationToken, language);
        }
        else if (command == "/history")
        {
            await ShowDatePickerAsync(chatId, language, cancellationToken);
        }
    }

    private async Task HandleCallbackQueryAsync(TelegramCallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message?.Chat?.Id.ToString();
        if (string.IsNullOrWhiteSpace(chatId))
        {
            logger.LogWarning("Callback query without chat ID");
            return;
        }

        var settings = await userSettingsService.GetOrCreateAsync(chatId, cancellationToken);
        var language = settings.Language;
        var callbackData = callbackQuery.Data ?? string.Empty;

        // Acknowledge the callback
        await telegramService.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

        // Parse callback data: format "menu:level:action"
        var parts = callbackData.Split(':');
        if (parts.Length < 2)
        {
            logger.LogWarning("Invalid callback data format: {Data}", callbackData);
            return;
        }

        var action = parts[0];
        var level = parts.Length > 1 ? parts[1] : string.Empty;
        var param = parts.Length > 2 ? parts[2] : string.Empty;

        if (action == "menu")
        {
            await HandleMenuNavigationAsync(chatId, level, param, language, cancellationToken);
        }
    }

    private async Task HandleMenuNavigationAsync(string chatId, string level, string param, string language, CancellationToken cancellationToken)
    {
        var menu = new List<TelegramMenuRow>();
        string message;

        switch (level)
        {
            case "main":
                message = localizationService.GetString("commands.menu_main", language);
                menu = GetMainMenu(language);
                break;

            case "reports":
                message = localizationService.GetString("commands.menu_reports", language);
                menu = GetReportsMenu(language);
                break;

            case "settings":
                message = localizationService.GetString("commands.menu_settings", language);
                menu = GetSettingsMenu(language);
                break;

            case "email":
                await StartEmailAlertsSubscriptionAsync(chatId, cancellationToken, language);
                return;

            case "feedback":
                await StartFeedbackCaptureAsync(chatId, cancellationToken, language);
                return;

            case "currentrate":
                await SendCurrentRateAsync(chatId, cancellationToken, language);
                return;

            case "last30days":
                await SendLastOneMonthRatesAsync(chatId, cancellationToken, language);
                return;

            case "highestlowest":
                await SendHighestLowestAsync(chatId, cancellationToken, language);
                return;

            case "weeklysummary":
                // Trigger weekly summary manually (for testing)
                await SendWeeklySummaryAsync(chatId, cancellationToken, language);
                return;

            case "language":
                await ShowLanguageMenuAsync(chatId, language, cancellationToken);
                return;

            case "setlang":
                await userSettingsService.UpdateLanguageAsync(chatId, param, cancellationToken);
                var langChangedMsg = localizationService.GetString("commands.language_changed", language, 
                    param == "en" ? localizationService.GetString("commands.english", language) : localizationService.GetString("commands.malayalam", language));
                await telegramService.SendMessageToChatIdAsync(chatId, langChangedMsg, cancellationToken);
                return;

            case "toggle_telegram":
                var currentSettings = await userSettingsService.GetOrCreateAsync(chatId, cancellationToken);
                var newState = !currentSettings.TelegramNotificationsEnabled;
                await userSettingsService.SetTelegramNotificationsAsync(chatId, newState, cancellationToken);
                var toggleMsg = localizationService.GetString("commands.telegram_toggled", language, 
                    newState ? localizationService.GetString("commands.enabled", language) : localizationService.GetString("commands.disabled", language));
                await telegramService.SendMessageToChatIdAsync(chatId, toggleMsg, cancellationToken);
                return;

            case "toggle_email":
                var currentEmailSettings = await userSettingsService.GetOrCreateAsync(chatId, cancellationToken);
                var newEmailState = !currentEmailSettings.EmailNotificationsEnabled;
                await userSettingsService.SetEmailNotificationsAsync(chatId, newEmailState, cancellationToken);
                var emailToggleMsg = localizationService.GetString("commands.email_toggled", language, 
                    newEmailState ? localizationService.GetString("commands.enabled", language) : localizationService.GetString("commands.disabled", language));
                await telegramService.SendMessageToChatIdAsync(chatId, emailToggleMsg, cancellationToken);
                return;

            case "date_pick":
                await HandleDatePickAsync(chatId, param, language, cancellationToken);
                return;

            case "date_nav":
                await ShowDatePickerAsync(chatId, language, cancellationToken, param);
                return;

            default:
                message = localizationService.GetString("commands.menu_main", language);
                menu = GetMainMenu(language);
                break;
        }

        await telegramService.SendMessageWithMenuAsync(chatId, message, menu, cancellationToken);
    }

    private List<TelegramMenuRow> GetMainMenu(string language)
    {
        return new List<TelegramMenuRow>
        {
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_current_rate", language), CallbackData = "menu:currentrate" },
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_reports", language), CallbackData = "menu:reports" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_settings", language), CallbackData = "menu:settings" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_email", language), CallbackData = "menu:email" },
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_feedback", language), CallbackData = "menu:feedback" }
                }
            }
        };
    }

    private List<TelegramMenuRow> GetReportsMenu(string language)
    {
        return new List<TelegramMenuRow>
        {
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_last_30_days", language), CallbackData = "menu:last30days" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_weekly_summary", language), CallbackData = "menu:weeklysummary" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_highest_lowest", language), CallbackData = "menu:highestlowest" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_back", language), CallbackData = "menu:main" }
                }
            }
        };
    }

    private List<TelegramMenuRow> GetSettingsMenu(string language)
    {
        return new List<TelegramMenuRow>
        {
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_telegram_notifications", language), CallbackData = "menu:toggle_telegram" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_email_alerts", language), CallbackData = "menu:toggle_email" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_language", language), CallbackData = "menu:language" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_back", language), CallbackData = "menu:main" }
                }
            }
        };
    }

    private async Task ShowLanguageMenuAsync(string chatId, string language, CancellationToken cancellationToken)
    {
        var menu = new List<TelegramMenuRow>
        {
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.english", language), CallbackData = "menu:setlang:en" },
                    new TelegramMenuButton { Text = localizationService.GetString("commands.malayalam", language), CallbackData = "menu:setlang:ml" }
                }
            },
            new TelegramMenuRow
            {
                Buttons = new List<TelegramMenuButton>
                {
                    new TelegramMenuButton { Text = localizationService.GetString("commands.menu_back", language), CallbackData = "menu:settings" }
                }
            }
        };

        await telegramService.SendMessageWithMenuAsync(chatId, "Select Language", menu, cancellationToken);
    }

    private async Task SendWeeklySummaryAsync(string chatId, CancellationToken cancellationToken, string language = "en")
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
                var noDataMessage = $"<b>{localizationService.GetString("commands.weekly_summary", language)}</b>\n\n" +
                                    $"<i>{localizationService.GetString("commands.no_data_30_days", language)}</i>";
                await telegramService.SendMessageToChatIdAsync(chatId, noDataMessage, cancellationToken);
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

            await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending weekly summary to user {ChatId}", chatId);
            var errorMessage = "<b>Error</b>\n\nFailed to generate report. Please try again later.";
            await telegramService.SendMessageToChatIdAsync(chatId, errorMessage, cancellationToken);
        }
    }

    private async Task ShowDatePickerAsync(string chatId, string language, CancellationToken cancellationToken, string? monthOffset = null)
    {
        var indiaTimeZone = GetIndiaTimeZone();
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);
        
        var offset = 0;
        if (!string.IsNullOrWhiteSpace(monthOffset) && int.TryParse(monthOffset, out var parsedOffset))
        {
            offset = parsedOffset;
        }
        
        var targetDate = now.AddMonths(offset);
        var year = targetDate.Year;
        var month = targetDate.Month;
        
        var firstDayOfMonth = new DateTime(year, month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        var startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
        
        var menu = new List<TelegramMenuRow>();
        
        // Month navigation row
        var navRow = new TelegramMenuRow
        {
            Buttons = new List<TelegramMenuButton>
            {
                new TelegramMenuButton { Text = "◀️", CallbackData = $"menu:date_nav:{offset - 1}" },
                new TelegramMenuButton { Text = $"{targetDate:MMM yyyy}", CallbackData = $"menu:date_nav:{offset}" },
                new TelegramMenuButton { Text = "▶️", CallbackData = $"menu:date_nav:{offset + 1}" }
            }
        };
        menu.Add(navRow);
        
        // Day of week headers
        var daysRow = new TelegramMenuRow
        {
            Buttons = new List<TelegramMenuButton>
            {
                new TelegramMenuButton { Text = "Su", CallbackData = "ignore" },
                new TelegramMenuButton { Text = "Mo", CallbackData = "ignore" },
                new TelegramMenuButton { Text = "Tu", CallbackData = "ignore" },
                new TelegramMenuButton { Text = "We", CallbackData = "ignore" },
                new TelegramMenuButton { Text = "Th", CallbackData = "ignore" },
                new TelegramMenuButton { Text = "Fr", CallbackData = "ignore" },
                new TelegramMenuButton { Text = "Sa", CallbackData = "ignore" }
            }
        };
        menu.Add(daysRow);
        
        // Calendar days
        var currentDay = 1;
        var weekRow = new TelegramMenuRow { Buttons = new List<TelegramMenuButton>() };
        
        // Add empty cells for days before the 1st of the month
        for (int i = 0; i < startDayOfWeek; i++)
        {
            weekRow.Buttons.Add(new TelegramMenuButton { Text = " ", CallbackData = "ignore" });
        }
        
        // Add days of the month
        while (currentDay <= lastDayOfMonth.Day)
        {
            weekRow.Buttons.Add(new TelegramMenuButton 
            { 
                Text = currentDay.ToString(), 
                CallbackData = $"menu:date_pick:{year}-{month:D2}-{currentDay:D2}" 
            });
            
            if (weekRow.Buttons.Count == 7)
            {
                menu.Add(weekRow);
                weekRow = new TelegramMenuRow { Buttons = new List<TelegramMenuButton>() };
            }
            
            currentDay++;
        }
        
        // Add remaining empty cells to complete the last week
        while (weekRow.Buttons.Count < 7)
        {
            weekRow.Buttons.Add(new TelegramMenuButton { Text = " ", CallbackData = "ignore" });
        }
        
        if (weekRow.Buttons.Count > 0)
        {
            menu.Add(weekRow);
        }
        
        var message = localizationService.GetString("commands.select_date", language);
        await telegramService.SendMessageWithMenuAsync(chatId, message, menu, cancellationToken);
    }

    private async Task HandleDatePickAsync(string chatId, string dateStr, string language, CancellationToken cancellationToken)
    {
        if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var selectedDate))
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "Invalid date format.", cancellationToken);
            return;
        }

        var indiaTimeZone = GetIndiaTimeZone();
        var selectedDateStart = new DateTimeOffset(selectedDate, indiaTimeZone.GetUtcOffset(selectedDate));
        var selectedDateEnd = selectedDateStart.AddDays(1);

        var history = await goldRateService.GetHistoryAsync(
            range: null,
            from: selectedDateStart,
            to: selectedDateEnd,
            cancellationToken);

        if (history.Items == null || history.Items.Count == 0)
        {
            var notFoundMsg = localizationService.GetString("commands.rate_not_found", language, selectedDate.ToString("dd MMM yyyy"));
            await telegramService.SendMessageToChatIdAsync(chatId, notFoundMsg, cancellationToken);
            return;
        }

        var rate = history.Items.First();
        var istFetchedAt = TimeZoneInfo.ConvertTime(rate.FetchedAt, indiaTimeZone);
        var eightGramRate = rate.R22KT * 8;

        // Calculate rate difference from previous day
        var yesterday = GetIndiaDayRange(selectedDateStart.AddDays(-1));
        var yesterdayRateHistory = await goldRateService.GetHistoryAsync(
            range: null,
            from: yesterday.StartUtc,
            to: yesterday.EndUtc,
            cancellationToken);

        var rate1gChange = string.Empty;
        var rate8gChange = string.Empty;
        
        if (yesterdayRateHistory.Items != null && yesterdayRateHistory.Items.Count > 0)
        {
            var yesterdayRateValue = yesterdayRateHistory.Items.OrderByDescending(x => x.FetchedAt).First();
            var diff = rate.R22KT - yesterdayRateValue.R22KT;
            var diff8g = diff * 8;
            var emoji = diff > 0 ? "🔺" : (diff < 0 ? "🔻" : "➡️");
            
            if (diff != 0)
            {
                rate1gChange = $" ({emoji} {Math.Abs(diff):N2})";
                rate8gChange = $" ({emoji} {Math.Abs(diff8g):N2})";
            }
            else
            {
                rate1gChange = " (➡️ 0.00)";
                rate8gChange = " (➡️ 0.00)";
            }
        }

        var message = $"<b>{localizationService.GetString("commands.current_rate", language)}</b>\n" +
                      $"<i>{istFetchedAt:dd MMM yyyy, hh:mm tt} IST</i>\n\n" +
                      "<b>22K Gold Rate</b>\n" +
                      $"1g: Rs. {rate.R22KT:N2}{rate1gChange}\n" +
                      $"8g: Rs. {eightGramRate:N2}{rate8gChange}";

        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
    }

    private async Task StartEmailAlertsSubscriptionAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        await emailSubscriptionService.StartEmailCaptureAsync(chatId, cancellationToken);

        var message = localizationService.GetString("commands.emailalerts", language);
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
        logger.LogInformation("Started email alert subscription capture for Telegram chat {ChatId}", chatId);
    }

    private async Task StartFeedbackCaptureAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        memoryCache.Set(GetFeedbackCacheKey(chatId), true, FeedbackCaptureWindow);
        var message = localizationService.GetString("commands.feedback", language);
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
        logger.LogInformation("Started feedback capture for Telegram chat {ChatId}", chatId);
    }

    private async Task<bool> TryCompleteFeedbackAsync(
        string chatId,
        Kromic.Application.DTOs.TelegramUserDTO fromUser,
        string messageText,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetFeedbackCacheKey(chatId);
        if (!memoryCache.TryGetValue<bool>(cacheKey, out _))
        {
            return false;
        }

        memoryCache.Remove(cacheKey);
        await SubmitFeedbackAsync(chatId, fromUser, messageText, cancellationToken);
        return true;
    }

    private async Task SubmitFeedbackAsync(
        string chatId,
        Kromic.Application.DTOs.TelegramUserDTO fromUser,
        string messageText,
        CancellationToken cancellationToken,
        string language = "en")
    {
        var feedbackText = messageText.Trim();
        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "Feedback cannot be empty. Send /feedback to try again.", cancellationToken);
            return;
        }

        var feedback = new TelegramFeedbackNotification(
            chatId,
            fromUser.FirstName,
            fromUser.LastName,
            fromUser.Username,
            feedbackText,
            DateTimeOffset.UtcNow);

        var mailSent = false;
        var telegramSent = false;

        try
        {
            await emailService.SendTelegramFeedbackAsync(feedback, cancellationToken);
            mailSent = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram feedback email for chat {ChatId}", chatId);
        }

        try
        {
            telegramSent = await telegramService.SendMessageToChatIdAsync(
                _goldRateOptions.FeedbackTelegramChatId,
                BuildFeedbackTelegramMessage(feedback),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward Telegram feedback to owner chat for user chat {ChatId}", chatId);
        }

        if (mailSent || telegramSent)
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "Thanks, I received your feedback and forwarded it.", cancellationToken);
        }
        else
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "Sorry, I could not forward the feedback right now. Please try again later.", cancellationToken);
        }
    }

    private async Task TryCompleteEmailAlertsSubscriptionAsync(string chatId, string messageText, CancellationToken cancellationToken)
    {
        var result = await emailSubscriptionService.TryCompleteEmailCaptureAsync(chatId, messageText, cancellationToken);
        var response = result.Status switch
        {
            EmailAlertSubscriptionStatus.Subscribed => $"Email alerts enabled for {result.Email}. You can unsubscribe from any future email.",
            EmailAlertSubscriptionStatus.InvalidEmail => "That does not look like a valid email address. Please send a valid email within 1 minute.",
            EmailAlertSubscriptionStatus.Expired => "The email alert request expired. Send /emailalerts again to subscribe.",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(response))
        {
            await telegramService.SendMessageToChatIdAsync(chatId, response, cancellationToken);
        }
    }

    private async Task SendLatestRateToUserAsync(string chatId, CancellationToken cancellationToken, bool isNewUser = false, string language = "en")
    {
        try
        {
            var currentRate = await goldRateService.GetCurrentAsync(cancellationToken);
            if (currentRate == null)
            {
                logger.LogInformation("No gold rate available to send to user {ChatId}", chatId);
                return;
            }

            var istFetchedAt = TimeZoneInfo.ConvertTime(currentRate.FetchedAt, GetIndiaTimeZone());
            var eightGramRate = currentRate.R22KT * 8;

            // Calculate rate difference from yesterday
            var indiaTimeZone = GetIndiaTimeZone();
            var yesterday = GetIndiaDayRange(currentRate.FetchedAt.AddDays(-1));
            var yesterdayRate = await goldRateService.GetHistoryAsync(
                range: null,
                from: yesterday.StartUtc,
                to: yesterday.EndUtc,
                cancellationToken);

            var rate1gChange = string.Empty;
            var rate8gChange = string.Empty;
            
            if (yesterdayRate.Items != null && yesterdayRate.Items.Count > 0)
            {
                var yesterdayRateValue = yesterdayRate.Items.OrderByDescending(x => x.FetchedAt).First();
                var diff = currentRate.R22KT - yesterdayRateValue.R22KT;
                var diff8g = diff * 8;
                var emoji = diff > 0 ? "🔺" : (diff < 0 ? "🔻" : "➡️");
                
                if (diff != 0)
                {
                    rate1gChange = $" ({emoji} {Math.Abs(diff):N2})";
                    rate8gChange = $" ({emoji} {Math.Abs(diff8g):N2})";
                }
                else
                {
                    rate1gChange = " (➡️ 0.00)";
                    rate8gChange = " (➡️ 0.00)";
                }
            }

            var title = isNewUser ? localizationService.GetString("commands.welcome_new", language) : localizationService.GetString("commands.current_rate", language);
            var message = $"<b>{title}</b>\n\n" +
                          "<b>22K Gold Rate</b>\n" +
                          $"1g: Rs. {currentRate.R22KT:N2}{rate1gChange}\n" +
                          $"8g: Rs. {eightGramRate:N2}{rate8gChange}\n" +
                          $"<i>Fetched at: {istFetchedAt:dd MMM yyyy, hh:mm tt} IST</i>";

            if (isNewUser)
            {
                message += "\n\n<i>" + localizationService.GetString("commands.youll_receive_updates", language) + "</i>";
            }

            await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
            logger.LogInformation("Sent gold rate message to user {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending rate to user {ChatId}", chatId);
        }
    }

    private async Task SendWelcomeAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        var welcomeMessage = localizationService.GetString("commands.welcome", language);
        await telegramService.SendMessageToChatIdAsync(chatId, welcomeMessage, cancellationToken);
        await SendHelpAsync(chatId, cancellationToken, language);
    }

    private async Task SendCurrentRateAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        await SendLatestRateToUserAsync(chatId, cancellationToken, isNewUser: false, language);
    }

    private async Task SendLastOneMonthRatesAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        try
        {
            var indiaTimeZone = GetIndiaTimeZone();
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);
            var thirtyDaysAgo = now.AddDays(-30);

            var history = await goldRateService.GetHistoryAsync(
                range: null,
                from: thirtyDaysAgo,
                to: now,
                cancellationToken);

            if (history.Items == null || history.Items.Count == 0)
            {
                var noDataMessage = $"<b>{localizationService.GetString("commands.last_30_days", language)}</b>\n\n" +
                                    $"<i>{localizationService.GetString("commands.no_data_30_days", language)}</i>";
                await telegramService.SendMessageToChatIdAsync(chatId, noDataMessage, cancellationToken);
                logger.LogInformation("Sent no-data report to user {ChatId}", chatId);
                return;
            }

            var dateGrouped = new Dictionary<string, decimal>();
            foreach (var rate in history.Items)
            {
                var istDate = TimeZoneInfo.ConvertTime(rate.FetchedAt, indiaTimeZone);
                var dateKey = istDate.ToString("dd MMM yyyy");
                if (!dateGrouped.ContainsKey(dateKey))
                {
                    dateGrouped[dateKey] = rate.R22KT;
                }
            }

            var sortedDates = dateGrouped.Keys.ToList();
            sortedDates.Sort();

            var tableHeader = $"<b>{localizationService.GetString("commands.last_30_days", language)}</b>\n\n";
            tableHeader += "<code>Date          | 1g 22K | 8g 22K\n";
            tableHeader += "--------------------------------------\n";

            var tableLines = new List<string> { tableHeader };
            var currentTableMessage = tableHeader;

            foreach (var dateKey in sortedDates)
            {
                var oneGramRate = dateGrouped[dateKey];
                var eightGramRate = oneGramRate * 8;
                var line = $"{dateKey,-13} | {oneGramRate,7:N0} | {eightGramRate,7:N0}\n";

                if ((currentTableMessage + line + "</code>").Length > 4000)
                {
                    currentTableMessage += "</code>";
                    tableLines.Add(currentTableMessage);
                    currentTableMessage = "<code>" + line;
                }
                else
                {
                    currentTableMessage += line;
                }
            }

            if (!currentTableMessage.EndsWith("</code>"))
            {
                currentTableMessage += "</code>";
            }
            tableLines.Add(currentTableMessage);

            tableLines = tableLines.Where(m => !string.IsNullOrWhiteSpace(m) && m != "<code></code>").ToList();

            foreach (var msg in tableLines)
            {
                await telegramService.SendMessageToChatIdAsync(chatId, msg, cancellationToken);
            }

            logger.LogInformation("Sent last-month-rates report with {Count} unique dates to user {ChatId}", dateGrouped.Count, chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending last-month-rates to user {ChatId}", chatId);
            var errorMessage = "<b>Error</b>\n\nFailed to generate report. Please try again later.";
            await telegramService.SendMessageToChatIdAsync(chatId, errorMessage, cancellationToken);
        }
    }

    private static string BuildFeedbackTelegramMessage(TelegramFeedbackNotification feedback)
    {
        var displayName = string.Join(" ", new[] { feedback.FirstName, feedback.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)))
            .Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = string.IsNullOrWhiteSpace(feedback.Username) ? feedback.ChatId : feedback.Username;
        }

        var username = string.IsNullOrWhiteSpace(feedback.Username) ? "-" : $"@{feedback.Username}";
        return "<b>Telegram feedback received</b>\n\n" +
               $"<b>From:</b> {Html(displayName)} ({Html(username)})\n" +
               $"<b>Chat ID:</b> {Html(feedback.ChatId)}\n" +
               $"<b>Received:</b> {feedback.ReceivedAt:dd MMM yyyy, hh:mm tt} UTC\n\n" +
               "<b>Message:</b>\n" +
               Html(Truncate(feedback.Message, 3200));
    }

    private static string GetFeedbackCacheKey(string chatId) => $"telegram-feedback:{chatId}";

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

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

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetIndiaDayRange(DateTimeOffset value)
    {
        var indiaTimeZone = GetIndiaTimeZone();
        var indiaValue = TimeZoneInfo.ConvertTime(value, indiaTimeZone);
        var start = new DateTimeOffset(indiaValue.Year, indiaValue.Month, indiaValue.Day, 0, 0, 0, indiaValue.Offset).ToUniversalTime();
        return (start, start.AddDays(1));
    }

    private async Task SendHelpAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        var message = localizationService.GetString("commands.help", language);
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
    }

    private async Task UnsubscribeEmailAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        var subscription = await emailSubscriptionService.GetByChatIdAsync(chatId, cancellationToken);
        if (subscription == null)
        {
            await telegramService.SendMessageToChatIdAsync(chatId, "No email subscription found for your account.", cancellationToken);
            return;
        }

        await emailSubscriptionService.UnsubscribeAsync(subscription.UnsubscribeToken, cancellationToken);
        var message = localizationService.GetString("commands.unsubscribeemail", language);
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
    }

    private async Task SendSettingsAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        var settings = await userSettingsService.GetOrCreateAsync(chatId, cancellationToken);
        
        var telegramStatus = settings.TelegramNotificationsEnabled && !settings.IsPaused 
            ? localizationService.GetString("commands.enabled", language) 
            : localizationService.GetString("commands.disabled", language);
        
        var emailStatus = settings.EmailNotificationsEnabled 
            ? localizationService.GetString("commands.enabled", language) 
            : localizationService.GetString("commands.disabled", language);
        
        var langDisplay = settings.Language == "en" 
            ? localizationService.GetString("commands.english", language) 
            : localizationService.GetString("commands.malayalam", language);

        var message = localizationService.GetString("commands.settings", language, telegramStatus, emailStatus, langDisplay);
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
    }

    private async Task PauseNotificationsAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        await userSettingsService.SetPausedAsync(chatId, true, cancellationToken);
        var message = localizationService.GetString("commands.pause", language);
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
    }

    private async Task ResumeNotificationsAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        await userSettingsService.SetPausedAsync(chatId, false, cancellationToken);
        var message = localizationService.GetString("commands.resume", language);
        await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
    }

    private async Task SendHighestLowestAsync(string chatId, CancellationToken cancellationToken, string language = "en")
    {
        try
        {
            var indiaTimeZone = GetIndiaTimeZone();
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone);
            var thirtyDaysAgo = now.AddDays(-30);

            var history = await goldRateService.GetHistoryAsync(
                range: null,
                from: thirtyDaysAgo,
                to: now,
                cancellationToken);

            if (history.Items == null || history.Items.Count == 0)
            {
                var noDataMessage = $"<b>{localizationService.GetString("commands.highest_lowest", language)}</b>\n\n" +
                                    $"<i>{localizationService.GetString("commands.no_data_30_days", language)}</i>";
                await telegramService.SendMessageToChatIdAsync(chatId, noDataMessage, cancellationToken);
                logger.LogInformation("Sent no-data highest/lowest report to user {ChatId}", chatId);
                return;
            }

            var highest = history.Items.OrderByDescending(x => x.R22KT).First();
            var lowest = history.Items.OrderBy(x => x.R22KT).First();

            var highestIstDate = TimeZoneInfo.ConvertTime(highest.FetchedAt, indiaTimeZone);
            var lowestIstDate = TimeZoneInfo.ConvertTime(lowest.FetchedAt, indiaTimeZone);

            var highestEightGram = highest.R22KT * 8;
            var lowestEightGram = lowest.R22KT * 8;

            var message = $"<b>{localizationService.GetString("commands.highest_lowest", language)}</b>\n\n" +
                          $"<b>📈 Highest Rate (30 Days)</b>\n" +
                          $"1g: Rs. {highest.R22KT:N2}\n" +
                          $"8g: Rs. {highestEightGram:N2}\n" +
                          $"<i>Date: {highestIstDate:dd MMM yyyy, hh:mm tt} IST</i>\n\n" +
                          $"<b>📉 Lowest Rate (30 Days)</b>\n" +
                          $"1g: Rs. {lowest.R22KT:N2}\n" +
                          $"8g: Rs. {lowestEightGram:N2}\n" +
                          $"<i>Date: {lowestIstDate:dd MMM yyyy, hh:mm tt} IST</i>";

            await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
            logger.LogInformation("Sent highest/lowest report to user {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending highest/lowest to user {ChatId}", chatId);
            var errorMessage = "<b>Error</b>\n\nFailed to generate report. Please try again later.";
            await telegramService.SendMessageToChatIdAsync(chatId, errorMessage, cancellationToken);
        }
    }
}