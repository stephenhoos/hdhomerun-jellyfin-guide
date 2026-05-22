using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.HDHomeRunGuide.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.HDHomeRunGuide.Api;

/// <summary>
/// Common API helpers for HDHomeRun Guide controllers.
/// </summary>
public abstract class HDHomeRunGuideControllerBase : ControllerBase
{
    /// <summary>
    /// Runs an API action and redacts secrets from problem responses.
    /// </summary>
    /// <typeparam name="T">Response type.</typeparam>
    /// <param name="action">API action.</param>
    /// <returns>Action result.</returns>
    protected async Task<ActionResult<T>> RunSafelyAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Problem(PluginLogService.RedactSecrets(ex.Message));
        }
    }
}
