using Kromic.Application.DTOs;

namespace Kromic.Application.Interfaces;

public interface IGoldRateService
{
    Task<GoldRateFetchResponse> FetchAndStoreAsync(
        bool sendRegularEmail,
        bool sendLowestAlert,
        CancellationToken cancellationToken);

    Task<GoldRateSnapshotResponse?> GetCurrentAsync(CancellationToken cancellationToken);

    Task<GoldRateHistoryResponse> GetHistoryAsync(
        string? range,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);
}
