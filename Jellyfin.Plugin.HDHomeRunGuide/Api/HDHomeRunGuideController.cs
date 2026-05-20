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
    private readonly PluginLogService _pluginLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="HDHomeRunGuideController"/> class.
    /// </summary>
    /// <param name="guideService">Guide service.</param>
    /// <param name="pluginLog">Plugin diagnostic log.</param>
    public HDHomeRunGuideController(HDHomeRunGuideService guideService, PluginLogService pluginLog)
    {
        _guideService = guideService;
        _pluginLog = pluginLog;
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
    /// Gets recent plugin diagnostic logs.
    /// </summary>
    /// <returns>Recent plugin diagnostic logs.</returns>
    [HttpGet("Logs")]
    public ActionResult<IReadOnlyList<string>> Logs()
    {
        return Ok(_pluginLog.GetRecent());
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
    public async Task<ActionResult<IReadOnlyList<DiscoveredTuner>>> Discover([FromQuery] string? subnet, CancellationToken cancellationToken)
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
    /// Uses Jellyfin's built-in HDHomeRun discovery to select a tuner and configure Live TV.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refresh result.</returns>
    [HttpPost("AddMyTuners")]
    public async Task<ActionResult<RefreshResult>> AddMyTuners(CancellationToken cancellationToken)
    {
        try
        {
            return await _guideService.AddMyTunersAsync(cancellationToken).ConfigureAwait(false);
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
        return RedactQueryValue(RedactQueryValue(RedactQueryValue(value, "DeviceAuth"), "Email"), "DeviceIDs");
    }

    private static string RedactQueryValue(string value, string key)
    {
        var marker = key + "=";
        var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return value;
        }

        var end = value.IndexOf('&', index);
        return end < 0
            ? value[..index] + key + "=REDACTED"
            : value[..index] + key + "=REDACTED" + value[end..];
    }
}
