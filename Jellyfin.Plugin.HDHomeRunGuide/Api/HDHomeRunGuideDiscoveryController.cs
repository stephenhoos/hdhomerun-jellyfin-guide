using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HDHomeRunGuide.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.HDHomeRunGuide.Api;

/// <summary>
/// Server-side API endpoints for HDHomeRun tuner discovery.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("HDHomeRunGuide")]
public sealed class HDHomeRunGuideDiscoveryController : HDHomeRunGuideControllerBase
{
    private readonly HDHomeRunGuideService _guideService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HDHomeRunGuideDiscoveryController"/> class.
    /// </summary>
    /// <param name="guideService">Guide service.</param>
    public HDHomeRunGuideDiscoveryController(HDHomeRunGuideService guideService)
    {
        _guideService = guideService;
    }

    /// <summary>
    /// Tests a tuner address.
    /// </summary>
    /// <param name="address">Tuner address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered tuner information.</returns>
    [HttpGet("Test")]
    public async Task<ActionResult<DiscoveredTuner>> Test([FromQuery] string address, CancellationToken cancellationToken)
    {
        return await RunSafelyAsync(() => _guideService.TestTunerAsync(address, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Scans a subnet for tuners.
    /// </summary>
    /// <param name="subnet">CIDR subnet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered tuner list.</returns>
    [HttpGet("Discover")]
    public async Task<ActionResult<IReadOnlyList<DiscoveredTuner>>> Discover([FromQuery] string? subnet, CancellationToken cancellationToken)
    {
        return await RunSafelyAsync(() => _guideService.DiscoverTunersAsync(subnet, cancellationToken)).ConfigureAwait(false);
    }
}
