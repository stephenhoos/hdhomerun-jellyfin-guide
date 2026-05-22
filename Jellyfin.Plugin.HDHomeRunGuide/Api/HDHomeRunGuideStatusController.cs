using System.Collections.Generic;
using Jellyfin.Plugin.HDHomeRunGuide.Configuration;
using Jellyfin.Plugin.HDHomeRunGuide.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.HDHomeRunGuide.Api;

/// <summary>
/// Server-side API endpoints for HDHomeRun Guide status and diagnostics.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("HDHomeRunGuide")]
public sealed class HDHomeRunGuideStatusController : ControllerBase
{
    private readonly PluginLogService _pluginLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="HDHomeRunGuideStatusController"/> class.
    /// </summary>
    /// <param name="pluginLog">Plugin diagnostic log.</param>
    public HDHomeRunGuideStatusController(PluginLogService pluginLog)
    {
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
        config.LastError = PluginLogService.RedactSecrets(config.LastError);
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

}
