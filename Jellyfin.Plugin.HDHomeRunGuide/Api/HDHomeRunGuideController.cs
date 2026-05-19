using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HDHomeRunGuide.Configuration;
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
public sealed class HDHomeRunGuideController : ControllerBase
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
    /// Gets plugin status.
    /// </summary>
    /// <returns>Current configuration status.</returns>
    [HttpGet("Status")]
    public ActionResult<PluginConfiguration> Status()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return Problem("Plugin is not initialized.");
        }

        var config = plugin.Configuration;
        config.LastError = RedactSecrets(config.LastError);
        return config;
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
        try
        {
            return await _guideService.TestTunerAsync(address, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Problem(RedactSecrets(ex.Message));
        }
    }

    /// <summary>
    /// Scans a subnet for tuners.
    /// </summary>
    /// <param name="subnet">CIDR subnet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered tuner list.</returns>
    [HttpGet("Discover")]
    public async Task<ActionResult<IReadOnlyList<DiscoveredTuner>>> Discover([FromQuery] string subnet, CancellationToken cancellationToken)
    {
        try
        {
            var tuners = await _guideService.DiscoverTunersAsync(subnet, cancellationToken).ConfigureAwait(false);
            return Ok(tuners);
        }
        catch (Exception ex)
        {
            return Problem(RedactSecrets(ex.Message));
        }
    }

    /// <summary>
    /// Runs an immediate guide refresh.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refresh result.</returns>
    [HttpPost("Refresh")]
    public async Task<ActionResult<RefreshResult>> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            return await _guideService.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Problem(RedactSecrets(ex.Message));
        }
    }

    private static string RedactSecrets(string value)
    {
        var index = value.IndexOf("DeviceAuth=", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return value;
        }

        var end = value.IndexOf('&', index);
        return end < 0
            ? value[..index] + "DeviceAuth=REDACTED"
            : value[..index] + "DeviceAuth=REDACTED" + value[end..];
    }
}
