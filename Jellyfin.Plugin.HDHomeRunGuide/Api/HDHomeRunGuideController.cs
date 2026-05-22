using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HDHomeRunGuide.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.HDHomeRunGuide.Api;

/// <summary>
/// Server-side API endpoints for the HDHomeRun Guide setup page.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("HDHomeRunGuide")]
public sealed class HDHomeRunGuideController : HDHomeRunGuideControllerBase
{
    private readonly HDHomeRunGuideService _guideService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HDHomeRunGuideController"/> class.
    /// </summary>
    /// <param name="guideService">Guide service.</param>
    public HDHomeRunGuideController(HDHomeRunGuideService guideService)
    {
        _guideService = guideService;
    }

    /// <summary>
    /// Uses Jellyfin's built-in HDHomeRun discovery to select a tuner and configure Live TV.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refresh result.</returns>
    [HttpPost("AddMyTuners")]
    public async Task<ActionResult<RefreshResult>> AddMyTuners(CancellationToken cancellationToken)
    {
        return await RunSafelyAsync(() => _guideService.AddMyTunersAsync(cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs an immediate guide refresh.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refresh result.</returns>
    [HttpPost("Refresh")]
    public async Task<ActionResult<RefreshResult>> Refresh(CancellationToken cancellationToken)
    {
        return await RunSafelyAsync(() => _guideService.RefreshAsync(cancellationToken)).ConfigureAwait(false);
    }
}
