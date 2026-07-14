namespace Kromic.Application.DTOs;

public sealed record GoldRateSnapshotResponse(
    Guid Id,
    decimal R22KT,
    bool R22KTShow,
    decimal? R18KT,
    decimal? R24KT,
    DateTimeOffset? SourceLastUpdatedAt,
    DateTimeOffset FetchedAt,
    bool IsLowestAtFetch,
    string? RegularEmailMessageId,
    string? LowestAlertMessageId);

public sealed record GoldRateFetchResponse(
    GoldRateSnapshotResponse Snapshot,
    bool RegularEmailSent,
    bool LowestAlertSent,
    bool RateChanged);

public sealed record GoldRateHistoryResponse(
    GoldRateSnapshotResponse? Current,
    GoldRateSnapshotResponse? Lowest,
    IReadOnlyList<GoldRateSnapshotResponse> Items);

public sealed record GoldRateEmailTemplateParams(
    string Name,
    string Email,
    string Subject,
    string Heading,
    string Summary,
    string Note,
    string Rate1g,
    string Change1g,
    string Rate8g,
    string Change8g,
    string ChangeClass,
    string FetchedAt,
    bool IsLowestAlert,
    string? CallToActionText = null,
    string? CallToActionUrl = null);
