using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.HDHomeRunGuide.Configuration;

/// <summary>
/// HDHomeRun guide plugin configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the HDHomeRun tuner base URL or IP address.
    /// </summary>
    public string TunerAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional subnet to scan, for example 192.168.1.0/24.
    /// </summary>
    public string ScanSubnet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scheduled refresh interval in hours.
    /// </summary>
    public int RefreshIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets a value indicating whether disabled lineup channels should be skipped.
    /// </summary>
    public bool SkipDisabledChannels { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the plugin should update Jellyfin Live TV paths after refresh.
    /// </summary>
    public bool AutoConfigureLiveTv { get; set; } = true;

    /// <summary>
    /// Gets or sets the last generated XMLTV guide path.
    /// </summary>
    public string LastGuidePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last generated M3U lineup path.
    /// </summary>
    public string LastM3uPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last successful refresh timestamp.
    /// </summary>
    public string LastRefreshUtc { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the next randomized refresh timestamp.
    /// </summary>
    public string NextRefreshUtc { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last discovered HDHomeRun tuner count.
    /// </summary>
    public int LastTunerCount { get; set; }

    /// <summary>
    /// Gets or sets the Jellyfin M3U tuner host id managed by this plugin.
    /// </summary>
    public string LiveTvTunerHostId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin XMLTV listing provider id managed by this plugin.
    /// </summary>
    public string LiveTvListingProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last refresh error.
    /// </summary>
    public string LastError { get; set; } = string.Empty;
}
