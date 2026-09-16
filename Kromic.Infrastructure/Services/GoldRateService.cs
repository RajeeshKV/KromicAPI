using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Kromic.Infrastructure.Services;

public sealed class GoldRateService(
    HttpClient httpClient,
    KromicDbContext dbContext,
    ITransactionalEmailService emailService,
    ITelegramService telegramService,
    ITelegramUserService telegramUserService,
    IGoldRateEmailSubscriptionService emailSubscriptionService,
    IUserSettingsService userSettingsService,
    ILocalizationService localizationService,
    IOptions<GoldRateOptions> options,
    ILogger<GoldRateService> logger) : IGoldRateService
{
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly GoldRateOptions _options = options.Value;

    public async Task<GoldRateFetchResponse> FetchAndStoreAsync(
        bool sendRegularEmail,
        bool sendLowestAlert,
        CancellationToken cancellationToken)
    {
        var source = await FetchLatestGoldRateAsync(cancellationToken);
        var data = source?.ReadData();
        if (source?.Success != true || data is null)
        {
            throw new InvalidOperationException(source?.Message ?? "Gold rate endpoint did not return a successful response with usable Data.");
        }

        // If the source provides a date and it's not today, skip — prevents stale data
        // from being saved and triggering notifications before the rate is published.
        if (data.LastUpdated is not null && !IsSourceDateTodayInIndia(data.LastUpdated))
        {
            logger.LogInformation(
                "Skipping save — source data is not dated today (source date: {SourceDate}). Rate not yet published.",
                data.LastUpdated);

            var current = await GetCurrentAsync(cancellationToken);
            return new GoldRateFetchResponse(current, RegularEmailSent: false, LowestAlertSent: false, RateChanged: false);
        }

        return await StoreFetchedGoldRateAsync(data, sendRegularEmail, sendLowestAlert, cancellationToken);
    }

    public async Task<GoldRateFetchResponse?> FetchAkgsmaTodayAndStoreAsync(
        bool sendRegularEmail,
        bool sendLowestAlert,
        CancellationToken cancellationToken)
    {
        if (!IsAkgsmaEndpoint(_options.Endpoint))
        {
            logger.LogDebug("Skipping AKGSMA pre-market fetch because the configured gold rate endpoint is {Endpoint}.", _options.Endpoint);
            return null;
        }

        GoldRateApiResponse? source = null;

        // Try AKGSMA first; fall back to goodreturns.in if blocked
        try
        {
            source = await FetchAkgsmaGoldRateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AKGSMA pre-market fetch failed, falling back to goodreturns.in.");
        }

        if (source is null)
        {
            source = await FetchGoodReturnsKeralaGoldRateAsync(cancellationToken);
        }

        var data = source?.ReadData();
        if (source?.Success != true || data is null)
        {
            throw new InvalidOperationException(source?.Message ?? "Neither AKGSMA nor goodreturns.in returned usable data.");
        }

        if (!IsSourceDateTodayInIndia(data.LastUpdated))
        {
            logger.LogInformation(
                "Pre-market page is not dated today yet. Source date: {SourceDate}.",
                string.IsNullOrWhiteSpace(data.LastUpdated) ? "unavailable" : data.LastUpdated);
            return null;
        }

        return await StoreFetchedGoldRateAsync(data, sendRegularEmail, sendLowestAlert, cancellationToken);
    }

    private async Task<GoldRateFetchResponse> StoreFetchedGoldRateAsync(
        GoldRateApiData data,
        bool sendRegularEmail,
        bool sendLowestAlert,
        CancellationToken cancellationToken)
    {
        var fetchedAt = DateTimeOffset.UtcNow;
        var todayRange = GetIndiaDayRange(fetchedAt);
        var latestToday = await dbContext.GoldRateSnapshots
            .Where(x => x.FetchedAt >= todayRange.StartUtc && x.FetchedAt < todayRange.EndUtc)
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestToday is not null && latestToday.R22KT == data.R22KT)
        {
            logger.LogInformation(
                "Gold rate unchanged for {Date}. Latest stored 22K rate: {Rate}. Skipping save and notifications.",
                TimeZoneInfo.ConvertTime(fetchedAt, GetIndiaTimeZone()).Date,
                data.R22KT);
            return new GoldRateFetchResponse(ToResponse(latestToday), RegularEmailSent: false, LowestAlertSent: false, RateChanged: false);
        }

        var previousLowest = await dbContext.GoldRateSnapshots
            .AsNoTracking()
            .MinAsync(x => (decimal?)x.R22KT, cancellationToken);

        var isLowest = previousLowest.HasValue && data.R22KT < previousLowest.Value;
        var snapshot = new GoldRateSnapshot
        {
            SourceId = data.Id,
            R22KT = data.R22KT,
            R22KTShow = data.R22KTShow,
            R18KT = data.R18KT,
            R24KT = data.R24KT,
            SourceLastUpdatedAt = ParseIndiaDateTime(data.LastUpdated),
            FetchedAt = fetchedAt,
            IsLowestAtFetch = isLowest
        };

        dbContext.GoldRateSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Sync any new Telegram users from configured environment
        await SyncConfiguredTelegramUsersAsync(cancellationToken);

        if (sendRegularEmail)
        {
            snapshot.RegularEmailMessageId = await SendRateEmailAsync(snapshot, isLowestAlert: false, cancellationToken);
            await SendRateTelegramAsync(snapshot, isLowestAlert: false, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (sendLowestAlert && isLowest)
        {
            snapshot.LowestAlertMessageId = await SendRateEmailAsync(snapshot, isLowestAlert: true, cancellationToken);
            await SendRateTelegramAsync(snapshot, isLowestAlert: true, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new GoldRateFetchResponse(ToResponse(snapshot), sendRegularEmail, sendLowestAlert && isLowest, RateChanged: true);
    }
    private async Task<GoldRateApiResponse?> FetchLatestGoldRateAsync(CancellationToken cancellationToken)
    {
        if (IsAkgsmaEndpoint(_options.Endpoint))
        {
            // Try AKGSMA first; fall back to goodreturns.in if blocked
            try
            {
                var result = await FetchAkgsmaGoldRateAsync(cancellationToken);
                if (result is not null) return result;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AKGSMA fetch failed, falling back to goodreturns.in.");
            }

            return await FetchGoodReturnsKeralaGoldRateAsync(cancellationToken);
        }

        return await FetchJsonGoldRateAsync(cancellationToken);
    }

    private async Task<GoldRateApiResponse?> FetchAkgsmaGoldRateAsync(CancellationToken cancellationToken)
    {
        // Litespeed bot protection sets a session cookie on first visit.
        // We do a warm-up GET first so the cookie jar is seeded, then
        // the real request carries that cookie and passes the bot check.
        var browserHeaders = new (string Name, string Value)[]
        {
            ("User-Agent",               "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36"),
            ("Accept",                   "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8"),
            ("Accept-Language",          "en-US,en;q=0.9"),
            ("Connection",               "keep-alive"),
            ("Upgrade-Insecure-Requests","1"),
            ("Cache-Control",            "no-cache"),
            ("Sec-Fetch-Dest",           "document"),
            ("Sec-Fetch-Mode",           "navigate"),
            ("Sec-Fetch-Site",           "none"),
            ("Sec-Fetch-User",           "?1"),
        };

        // --- Warm-up request (seeds cookies) ---
        try
        {
            using var warmup = new HttpRequestMessage(HttpMethod.Get, _options.Endpoint);
            foreach (var (name, value) in browserHeaders)
                warmup.Headers.TryAddWithoutValidation(name, value);

            using var warmupResponse = await httpClient.SendAsync(warmup, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            logger.LogDebug("AKGSMA warm-up responded with {Status}", (int)warmupResponse.StatusCode);

            // Small delay to mimic a real browser pause between requests
            await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AKGSMA warm-up request failed; proceeding anyway.");
        }

        // --- Real request (carries cookies from warm-up) ---
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.Endpoint);
        foreach (var (name, value) in browserHeaders)
            request.Headers.TryAddWithoutValidation(name, value);

        // Referer makes it look like we navigated from the same site
        request.Headers.TryAddWithoutValidation("Referer", _options.Endpoint);
        request.Headers.TryAddWithoutValidation("Cache-Control", "max-age=0");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AKGSMA gold rate page failed with {(int)response.StatusCode} {response.ReasonPhrase}. Response: {html}");
        }

        return ParseAkgsmaHtml(html);
    }

    private async Task<GoldRateApiResponse?> FetchGoodReturnsKeralaGoldRateAsync(CancellationToken cancellationToken)
    {
        const string url = "https://www.goodreturns.in/gold-rates/kerala.html";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"goodreturns.in Kerala gold rate page failed with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return ParseGoodReturnsHtml(html);
    }

    private static GoldRateApiResponse ParseGoodReturnsHtml(string html)
    {
        // Matches: 22K Gold /g ... ₹14,065
        var rateMatch = Regex.Match(
            html,
            @"22K\s+Gold\s*/g[\s\S]*?₹\s*([\d,]+(?:\.\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!rateMatch.Success)
        {
            throw new InvalidOperationException("goodreturns.in page did not contain a parseable 22K gold rate.");
        }

        var rateText = rateMatch.Groups[1].Value.Replace(",", string.Empty);
        if (!decimal.TryParse(rateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var r22Kt))
        {
            throw new InvalidOperationException($"goodreturns.in 22K rate could not be parsed: {rateText}");
        }

        // Try to extract the date from the page title e.g. "16 September 2026"
        var dateMatch = Regex.Match(
            html,
            @"(\d{1,2}\s+(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        string? dateString = dateMatch.Success ? dateMatch.Groups[1].Value : null;

        // Convert "16 September 2026" → "16/09/2026" for ParseIndiaDateTime compatibility
        string? parsedDate = null;
        if (dateString is not null &&
            DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDt))
        {
            parsedDate = parsedDt.ToString("dd/MM/yyyy");
        }

        var data = new GoldRateApiData
        {
            Id = 0,
            R22KT = r22Kt,
            R22KTShow = true,
            LastUpdated = parsedDate
        };

        return GoldRateApiResponse.FromData("goodreturns.in rate parsed successfully.", data);
    }

    private async Task<GoldRateApiResponse?> FetchJsonGoldRateAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gold rate endpoint failed with {(int)response.StatusCode} {response.ReasonPhrase}. Response: {responseBody}");
        }

        try
        {
            return JsonSerializer.Deserialize<GoldRateApiResponse>(responseBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Gold rate endpoint returned unexpected JSON. Response: {responseBody}", ex);
        }
    }

    private static GoldRateApiResponse ParseAkgsmaHtml(string html)
    {
        var decodedHtml = WebUtility.HtmlDecode(html);
        var dateMatch = Regex.Match(
            decodedHtml,
            @"Today['�]s\s+Rate\s*\((?<date>\d{2}/\d{2}/\d{4})\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var rateMatch = Regex.Match(
            decodedHtml,
            @"22K916\s*\(\s*1\s*gm\s*\)\s*-\s*[^\d]*(?<rate>[\d,]+(?:\.\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!rateMatch.Success)
        {
            throw new InvalidOperationException("AKGSMA gold rate page did not contain a 22K916 (1gm) rate.");
        }

        var rateText = rateMatch.Groups["rate"].Value.Replace(",", string.Empty);
        if (!decimal.TryParse(rateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var r22Kt))
        {
            throw new InvalidOperationException($"AKGSMA 22K916 rate could not be parsed: {rateText}");
        }

        var data = new GoldRateApiData
        {
            Id = 0,
            R22KT = r22Kt,
            R22KTShow = true,
            LastUpdated = dateMatch.Success ? dateMatch.Groups["date"].Value : null
        };

        return GoldRateApiResponse.FromData("AKGSMA rate parsed successfully.", data);
    }
    private static bool IsAkgsmaEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
        uri.Host.Contains("akgsma.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsSourceDateTodayInIndia(string? sourceDate)
    {
        var sourceUpdatedAt = ParseIndiaDateTime(sourceDate);
        if (!sourceUpdatedAt.HasValue)
        {
            return false;
        }

        var indiaTimeZone = GetIndiaTimeZone();
        var sourceIndiaDate = TimeZoneInfo.ConvertTime(sourceUpdatedAt.Value, indiaTimeZone).Date;
        var todayIndiaDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, indiaTimeZone).Date;
        return sourceIndiaDate == todayIndiaDate;
    }
    public async Task<GoldRateSnapshotResponse?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.GoldRateSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return snapshot is null ? null : ToResponse(snapshot);
    }

    public async Task<GoldRateHistoryResponse> GetHistoryAsync(
        string? range,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.GoldRateSnapshots.AsNoTracking();
        var effectiveFrom = from ?? ResolveRangeStart(range);

        if (effectiveFrom.HasValue)
        {
            query = query.Where(x => x.FetchedAt >= effectiveFrom.Value.ToUniversalTime());
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.FetchedAt <= to.Value.ToUniversalTime());
        }

        var items = await query
            .OrderByDescending(x => x.FetchedAt)
            .ToListAsync(cancellationToken);

        var current = await GetCurrentAsync(cancellationToken);
        var lowest = await dbContext.GoldRateSnapshots
            .AsNoTracking()
            .OrderBy(x => x.R22KT)
            .ThenBy(x => x.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new GoldRateHistoryResponse(
            current,
            lowest is null ? null : ToResponse(lowest),
            items.Select(ToResponse).ToList());
    }

    private async Task<GoldRateSnapshot?> GetPreviousSnapshotAsync(
        DateTimeOffset anchor,
        CancellationToken cancellationToken)
    {
        return await dbContext.GoldRateSnapshots
            .AsNoTracking()
            .Where(x => x.FetchedAt < anchor)
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> SendRateEmailAsync(
        GoldRateSnapshot snapshot,
        bool isLowestAlert,
        CancellationToken cancellationToken)
    {
        var istFetchedAt = TimeZoneInfo.ConvertTime(snapshot.FetchedAt, GetIndiaTimeZone());
        var subject = isLowestAlert
            ? $"Lowest gold rate found: 22K {snapshot.R22KT:N2}"
            : $"Today's gold rate: 22K {snapshot.R22KT:N2}";
        var heading = isLowestAlert
            ? "This is the lowest saved 22K rate. Buy gold now."
            : "Today's 22K gold rate";
        var summary = isLowestAlert ? "Lowest saved rate" : "Daily gold rate update";
        var note = isLowestAlert
            ? "This is the lowest saved 22K rate. Buy gold now."
            : "Today's 22K gold rate snapshot.";
        var eightGramRate = snapshot.R22KT * 8;

        var previousSnapshot = await GetPreviousSnapshotAsync(snapshot.FetchedAt, cancellationToken);

        var rate1gStr = $"Rs. {snapshot.R22KT:N2}";
        var rate8gStr = $"Rs. {eightGramRate:N2}";
        var change = string.Empty;
        var change8g = string.Empty;
        var changeClass = "rate-change-stable";

        if (previousSnapshot != null)
        {
            var diff = snapshot.R22KT - previousSnapshot.R22KT;
            var diff8g = diff * 8;
            
            if (diff > 0)
            {
                change = $"🔺+{diff:N2}";
                change8g = $"🔺+{diff8g:N2}";
                changeClass = "rate-change-up";
            }
            else if (diff < 0)
            {
                change = $"🔻{diff:N2}";
                change8g = $"🔻{diff8g:N2}";
                changeClass = "rate-change-down";
            }
            else
            {
                change = "➡️ 0.00";
                change8g = "➡️ 0.00";
            }
        }

        var fetchedAtStr = $"{istFetchedAt:dd MMM yyyy, hh:mm tt}";

        var messageIds = new List<string>();
        var recipients = ResolveRecipients();
        foreach (var recipient in recipients)
        {
            var recipientName = string.IsNullOrWhiteSpace(_options.RecipientName) ? recipient : _options.RecipientName;
            var templateParams = new GoldRateEmailTemplateParams(
                recipientName,
                recipient,
                subject,
                heading,
                summary,
                note,
                rate1gStr,
                change,
                rate8gStr,
                change8g,
                changeClass,
                fetchedAtStr,
                false);

            var messageId = await emailService.SendGoldRateEmailAsync(
                recipient,
                recipientName,
                templateParams,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(messageId))
            {
                messageIds.Add(messageId);
            }
        }

        var subscribers = await emailSubscriptionService.GetActiveSubscribersAsync(cancellationToken);
        foreach (var subscriber in subscribers)
        {
            var unsubscribeUrl = BuildUnsubscribeUrl(subscriber.UnsubscribeToken);
            var templateParams = new GoldRateEmailTemplateParams(
                subscriber.Email,
                subscriber.Email,
                subject,
                heading,
                summary,
                note,
                rate1gStr,
                change,
                rate8gStr,
                change8g,
                changeClass,
                fetchedAtStr,
                true,
                unsubscribeUrl is null ? null : "Unsubscribe",
                unsubscribeUrl);

            var messageId = await emailService.SendGoldRateEmailAsync(
                subscriber.Email,
                subscriber.Email,
                templateParams,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(messageId))
            {
                messageIds.Add(messageId);
            }
        }

        return messageIds.Count == 0 ? null : string.Join(",", messageIds);
    }

    private string? BuildUnsubscribeUrl(string token)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/api/gold-rate-email-alerts/unsubscribe?token={Uri.EscapeDataString(token)}";
    }

    private async Task SendRateTelegramAsync(
        GoldRateSnapshot snapshot,
        bool isLowestAlert,
        CancellationToken cancellationToken)
    {
        var istFetchedAt = TimeZoneInfo.ConvertTime(snapshot.FetchedAt, GetIndiaTimeZone());
        var eightGramRate = snapshot.R22KT * 8;

        var previousSnapshot = await GetPreviousSnapshotAsync(snapshot.FetchedAt, cancellationToken);

        var rate1gChange = string.Empty;
        var rate8gChange = string.Empty;

        if (previousSnapshot != null)
        {
            var diff = snapshot.R22KT - previousSnapshot.R22KT;
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

        // Get all active chat IDs
        var chatIds = await telegramUserService.GetActiveChatIdsAsync(cancellationToken);
        
        // Also include configured chat IDs from environment variables
        var configuredChatIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chatId in _options.TelegramChatIds)
        {
            var trimmed = chatId?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                configuredChatIds.Add(trimmed);
            }
        }
        foreach (var chatId in _options.TelegramChatIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = chatId?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                configuredChatIds.Add(trimmed);
            }
        }
        
        // Combine and deduplicate
        var allChatIds = new HashSet<string>(chatIds);
        foreach (var id in configuredChatIds)
        {
            allChatIds.Add(id);
        }

        if (allChatIds.Count == 0)
        {
            logger.LogWarning("No Telegram chat IDs found for sending rate notifications.");
            return;
        }

        // Send personalized message to each user based on their language preference
        foreach (var chatId in allChatIds)
        {
            try
            {
                var userSettings = await userSettingsService.GetByChatIdAsync(chatId, cancellationToken);
                var language = userSettings?.Language ?? "en";
                
                var title = isLowestAlert 
                    ? localizationService.GetString("commands.lowest_gold_rate", language) 
                    : localizationService.GetString("commands.current_rate", language);
                
                // Fallback if localization returns the key itself
                if (title.StartsWith("commands."))
                {
                    title = isLowestAlert ? "Lowest Gold Rate Found" : "Today's Gold Rate";
                }

                var message = $"<b>{title}</b>\n\n" +
                    "<b>22K Gold Rate</b>\n" +
                    $"1g: Rs. {snapshot.R22KT:N2}{rate1gChange}\n" +
                    $"8g: Rs. {eightGramRate:N2}{rate8gChange}\n" +
                    $"<i>Fetched at: {istFetchedAt:dd MMM yyyy, hh:mm tt} IST</i>";

                await telegramService.SendMessageToChatIdAsync(chatId, message, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send Telegram rate notification to chat ID: {ChatId}", chatId);
            }
        }
    }
    private async Task SyncConfiguredTelegramUsersAsync(CancellationToken cancellationToken)
    {
        // Add configured chat IDs to database if they're not already there
        var configuredChatIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chatId in _options.TelegramChatIds)
        {
            var trimmed = chatId?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                configuredChatIds.Add(trimmed);
            }
        }

        foreach (var chatId in _options.TelegramChatIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = chatId?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                configuredChatIds.Add(trimmed);
            }
        }

        // Ensure all configured chat IDs are in the database
        foreach (var chatId in configuredChatIds)
        {
            await telegramUserService.AddOrUpdateUserAsync(
                chatId,
                "Configured User",
                null,
                null,
                cancellationToken,
                updateLastInteractedAt: false);
        }
    }

    private List<string> ResolveRecipients()
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRecipient(_options.RecipientEmail);
        foreach (var email in _options.RecipientEmails)
        {
            AddRecipient(email);
        }

        foreach (var email in _options.RecipientEmailsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddRecipient(email);
        }

        return recipients.OrderBy(x => x).ToList();

        void AddRecipient(string? email)
        {
            var trimmed = email?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                recipients.Add(trimmed);
            }
        }
    }

    private static DateTimeOffset? ParseIndiaDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hasOffset = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
            value.Contains('+') ||
            value.LastIndexOf('-') > value.IndexOf('T');
        if (hasOffset && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offsetValue))
        {
            return offsetValue.ToUniversalTime();
        }

        if (DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayFirstDate))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dayFirstDate, DateTimeKind.Unspecified), IndiaOffset).ToUniversalTime();
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), IndiaOffset).ToUniversalTime();
        }

        return null;
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetIndiaDayRange(DateTimeOffset value)
    {
        var indiaTimeZone = GetIndiaTimeZone();
        var indiaValue = TimeZoneInfo.ConvertTime(value, indiaTimeZone);
        var start = new DateTimeOffset(indiaValue.Year, indiaValue.Month, indiaValue.Day, 0, 0, 0, indiaValue.Offset).ToUniversalTime();
        return (start, start.AddDays(1));
    }
    private static DateTimeOffset? ResolveRangeStart(string? range)
    {
        var now = DateTimeOffset.UtcNow;
        return range?.Trim().ToUpperInvariant() switch
        {
            "1D" => now.AddDays(-1),
            "7D" => now.AddDays(-7),
            "1M" => now.AddMonths(-1),
            "3M" => now.AddMonths(-3),
            "6M" => now.AddMonths(-6),
            "1Y" => now.AddYears(-1),
            _ => null
        };
    }

    private static GoldRateSnapshotResponse ToResponse(GoldRateSnapshot snapshot) =>
        new(
            snapshot.Id,
            snapshot.R22KT,
            snapshot.R22KTShow,
            snapshot.R18KT,
            snapshot.R24KT,
            snapshot.SourceLastUpdatedAt,
            snapshot.FetchedAt,
            snapshot.IsLowestAtFetch,
            snapshot.RegularEmailMessageId,
            snapshot.LowestAlertMessageId);

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

    private sealed class GoldRateApiResponse
    {
        public string? Message { get; set; }
        public bool Success { get; set; }
        public JsonElement Data { get; set; }
        private GoldRateApiData? ParsedData { get; set; }

        public static GoldRateApiResponse FromData(string message, GoldRateApiData data) =>
            new()
            {
                Message = message,
                Success = true,
                ParsedData = data
            };

        public GoldRateApiData? ReadData()
        {
            if (ParsedData is not null)
            {
                return ParsedData;
            }

            return Data.ValueKind switch
            {
                JsonValueKind.Object => DeserializeDataElement(Data),
                JsonValueKind.Array => ReadFirstArrayItem(Data),
                JsonValueKind.String => ReadStringData(Data.GetString()),
                _ => null
            };
        }

        private static GoldRateApiData? ReadFirstArrayItem(JsonElement array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    return DeserializeDataElement(item);
                }
            }

            return null;
        }

        private static GoldRateApiData? ReadStringData(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.Object => DeserializeDataElement(root),
                JsonValueKind.Array => ReadFirstArrayItem(root),
                _ => null
            };
        }

        private static GoldRateApiData? DeserializeDataElement(JsonElement element) =>
            JsonSerializer.Deserialize<GoldRateApiData>(element.GetRawText(), JsonOptions);
    }

    private sealed class GoldRateApiData
    {
        public int Id { get; set; }
        public decimal R18KT { get; set; }
        public decimal R22KT { get; set; }
        public bool R22KTShow { get; set; }
        public decimal R24KT { get; set; }
        public string? LastUpdated { get; set; }
    }
}

