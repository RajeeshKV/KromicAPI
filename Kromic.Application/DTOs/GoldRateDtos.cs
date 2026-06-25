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
