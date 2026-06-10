using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/gold-rates")]
public sealed class GoldRatesController(IGoldRateService goldRateService) : ControllerBase
{
    [HttpPost("fetch")]
    public Task<GoldRateFetchResponse> Fetch(CancellationToken cancellationToken) =>
        goldRateService.FetchAndStoreAsync(
            sendRegularEmail: true,
            sendLowestAlert: true,
            cancellationToken);

    [HttpGet("current")]
    public async Task<GoldRateSnapshotResponse?> Current(
        [FromQuery] bool refresh,
        CancellationToken cancellationToken)
    {
        if (!refresh)
        {
            return await goldRateService.GetCurrentAsync(cancellationToken);
        }

        var result = await goldRateService.FetchAndStoreAsync(
            sendRegularEmail: false,
            sendLowestAlert: true,
            cancellationToken);

        return result.Snapshot;
    }

    [HttpGet]
    public Task<GoldRateHistoryResponse> History(
        [FromQuery] string? range,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        goldRateService.GetHistoryAsync(range, from, to, cancellationToken);
}
