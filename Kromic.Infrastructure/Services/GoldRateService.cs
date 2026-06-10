using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Kromic.Application.Options;
using Kromic.Domain.Entities;
using Kromic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kromic.Infrastructure.Services;

public sealed class GoldRateService(
    HttpClient httpClient,
    KromicDbContext dbContext,
    ITransactionalEmailService emailService,
    IOptions<GoldRateOptions> options) : IGoldRateService
{
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);
    private readonly GoldRateOptions _options = options.Value;

    public async Task<GoldRateFetchResponse> FetchAndStoreAsync(
        bool sendRegularEmail,
        bool sendLowestAlert,
        CancellationToken cancellationToken)
    {
        var source = await FetchLatestGoldRateAsync(cancellationToken);
        if (source?.Success != true || source.Data is null)
        {
            throw new InvalidOperationException(source?.Message ?? "Gold rate endpoint did not return a successful response.");
        }

        var previousLowest = await dbContext.GoldRateSnapshots
            .AsNoTracking()
            .MinAsync(x => (decimal?)x.R22KT, cancellationToken);

        var isLowest = previousLowest.HasValue && source.Data.R22KT < previousLowest.Value;
        var snapshot = new GoldRateSnapshot
        {
            SourceId = source.Data.Id,
            R22KT = source.Data.R22KT,
            R22KTShow = source.Data.R22KTShow,
            R18KT = source.Data.R18KT,
            R24KT = source.Data.R24KT,
            SourceLastUpdatedAt = ParseIndiaDateTime(source.Data.LastUpdated),
            FetchedAt = DateTimeOffset.UtcNow,
            IsLowestAtFetch = isLowest
        };

        dbContext.GoldRateSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (sendRegularEmail)
        {
            snapshot.RegularEmailMessageId = await SendRateEmailAsync(snapshot, isLowestAlert: false, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (sendLowestAlert && isLowest)
        {
            snapshot.LowestAlertMessageId = await SendRateEmailAsync(snapshot, isLowestAlert: true, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new GoldRateFetchResponse(ToResponse(snapshot), sendRegularEmail, sendLowestAlert && isLowest);
    }

    private async Task<GoldRateApiResponse?> FetchLatestGoldRateAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Gold rate endpoint failed with {(int)response.StatusCode} {response.ReasonPhrase}. Response: {responseBody}");
        }

        return await response.Content.ReadFromJsonAsync<GoldRateApiResponse>(cancellationToken);
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

    private async Task<string?> SendRateEmailAsync(
        GoldRateSnapshot snapshot,
        bool isLowestAlert,
        CancellationToken cancellationToken)
    {
        var istFetchedAt = TimeZoneInfo.ConvertTime(snapshot.FetchedAt, GetIndiaTimeZone());
        var subject = isLowestAlert
            ? $"Lowest gold rate found: R22KT {snapshot.R22KT:N2}"
            : $"Today's gold rate: R22KT {snapshot.R22KT:N2}";
        var heading = isLowestAlert
            ? "This is the lowest saved R22KT rate. Buy gold now."
            : "Today's R22KT gold rate";
        var body = string.Join(Environment.NewLine, [
            $"R22KT: {snapshot.R22KT:N2}",
            $"Fetched at: {istFetchedAt:dd MMM yyyy, hh:mm tt} IST",
            snapshot.SourceLastUpdatedAt.HasValue
                ? $"Source updated at: {TimeZoneInfo.ConvertTime(snapshot.SourceLastUpdatedAt.Value, GetIndiaTimeZone()):dd MMM yyyy, hh:mm tt} IST"
                : "Source updated at: unavailable"
        ]);

        var recipients = ResolveRecipients();
        if (recipients.Count == 0)
        {
            return await emailService.SendAdminNotificationAsync(subject, heading, body, cancellationToken);
        }

        var messageIds = new List<string>();
        foreach (var recipient in recipients)
        {
            var messageId = await emailService.SendCustomEmailAsync(
                recipient,
                string.IsNullOrWhiteSpace(_options.RecipientName) ? recipient : _options.RecipientName,
                subject,
                heading,
                body,
                null,
                null,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(messageId))
            {
                messageIds.Add(messageId);
            }
        }

        return messageIds.Count == 0 ? null : string.Join(",", messageIds);
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

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), IndiaOffset).ToUniversalTime();
        }

        return null;
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
        public GoldRateApiData? Data { get; set; }
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
