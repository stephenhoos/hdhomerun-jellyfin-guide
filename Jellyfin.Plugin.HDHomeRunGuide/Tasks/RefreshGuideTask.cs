using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HDHomeRunGuide.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.HDHomeRunGuide.Tasks;

/// <summary>
/// Jellyfin scheduled task that refreshes HDHomeRun guide data.
/// </summary>
public sealed class RefreshGuideTask : IScheduledTask
{
    private readonly HDHomeRunGuideService _guideService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshGuideTask"/> class.
    /// </summary>
    /// <param name="guideService">Guide service.</param>
    public RefreshGuideTask(HDHomeRunGuideService guideService)
    {
        _guideService = guideService;
    }

    /// <inheritdoc />
    public string Name => "Refresh HDHomeRun Guide";

    /// <inheritdoc />
    public string Key => "RefreshHDHomeRunGuide";

    /// <inheritdoc />
    public string Description => "Downloads SiliconDust HDHomeRun guide data and writes Jellyfin XMLTV/M3U files.";

    /// <inheritdoc />
    public string Category => "Live TV";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(5);
        await _guideService.RefreshAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var hours = Plugin.Instance?.Configuration.RefreshIntervalHours ?? 24;
        if (hours < 1)
        {
            hours = 24;
        }

        var minimumMinutes = Math.Max(30, (int)Math.Round(hours * 50.0));
        var maximumMinutes = Math.Max(minimumMinutes + 1, (int)Math.Round(hours * 70.0));
        var randomizedInterval = TimeSpan.FromMinutes(Random.Shared.Next(minimumMinutes, maximumMinutes + 1));

        return
        [
            new TaskTriggerInfo
            {
                Type = "IntervalTrigger",
                IntervalTicks = randomizedInterval.Ticks
            }
        ];
    }
}
