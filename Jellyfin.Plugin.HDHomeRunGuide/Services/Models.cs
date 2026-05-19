using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HDHomeRunGuide.Services;

/// <summary>
/// HDHomeRun discover.json response.
/// </summary>
public sealed class DiscoverInfo
{
    /// <summary>
    /// Gets or sets the device id.
    /// </summary>
    [JsonPropertyName("DeviceID")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device auth token.
    /// </summary>
    [JsonPropertyName("DeviceAuth")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string DeviceAuth { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lineup URL.
    /// </summary>
    [JsonPropertyName("LineupURL")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string LineupUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tuner count.
    /// </summary>
    [JsonPropertyName("TunerCount")]
    public int TunerCount { get; set; }

    /// <summary>
    /// Gets or sets the device friendly name.
    /// </summary>
    [JsonPropertyName("FriendlyName")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string FriendlyName { get; set; } = string.Empty;
}

/// <summary>
/// HDHomeRun lineup entry.
/// </summary>
public sealed class LineupItem
{
    /// <summary>
    /// Gets or sets the guide number.
    /// </summary>
    [JsonPropertyName("GuideNumber")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string GuideNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guide name.
    /// </summary>
    [JsonPropertyName("GuideName")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string GuideName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream URL.
    /// </summary>
    [JsonPropertyName("URL")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this lineup entry is disabled.
    /// </summary>
    [JsonPropertyName("DRM")]
    public bool Drm { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this lineup entry is disabled.
    /// </summary>
    public bool Favorite { get; set; }
}

/// <summary>
/// Guide channel entry from SiliconDust.
/// </summary>
public sealed class GuideChannel
{
    /// <summary>
    /// Gets or sets the guide number.
    /// </summary>
    [JsonPropertyName("GuideNumber")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string GuideNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guide name.
    /// </summary>
    [JsonPropertyName("GuideName")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string GuideName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the affiliate name.
    /// </summary>
    [JsonPropertyName("Affiliate")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Affiliate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel image URL.
    /// </summary>
    [JsonPropertyName("ImageURL")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the programs.
    /// </summary>
    [JsonPropertyName("Guide")]
    public IReadOnlyList<GuideProgram> Guide { get; set; } = [];
}

/// <summary>
/// Guide program entry from SiliconDust.
/// </summary>
public sealed class GuideProgram
{
    /// <summary>
    /// Gets or sets the start time as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("StartTime")]
    public long StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("EndTime")]
    public long EndTime { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonPropertyName("Title")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    [JsonPropertyName("EpisodeTitle")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string EpisodeTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the synopsis.
    /// </summary>
    [JsonPropertyName("Synopsis")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Synopsis { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    [JsonPropertyName("Category")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original air date.
    /// </summary>
    [JsonPropertyName("OriginalAirdate")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string OriginalAirdate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the onscreen episode number.
    /// </summary>
    [JsonPropertyName("EpisodeNumber")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string EpisodeNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the program id.
    /// </summary>
    [JsonPropertyName("ProgramID")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string ProgramId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the program image URL.
    /// </summary>
    [JsonPropertyName("ImageURL")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string ImageUrl { get; set; } = string.Empty;
}

/// <summary>
/// Result of a guide refresh.
/// </summary>
public sealed record RefreshResult(string GuidePath, string M3uPath, int ChannelCount, int ProgrammeCount, int M3uChannelCount, int TunerCount = 0, string GuideSource = "Guide");

/// <summary>
/// Discovered tuner information exposed to the setup page.
/// </summary>
public sealed record DiscoveredTuner(string Address, string DeviceId, string FriendlyName, int TunerCount);
