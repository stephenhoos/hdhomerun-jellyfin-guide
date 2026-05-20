using System;
using System.Collections.Generic;
using System.Text.Json;
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
    [JsonConverter(typeof(FlexibleBooleanJsonConverter))]
    public bool Drm { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this lineup entry is favorited.
    /// </summary>
    [JsonPropertyName("Favorite")]
    [JsonConverter(typeof(FlexibleBooleanJsonConverter))]
    public bool Favorite { get; set; }
}

/// <summary>
/// Result of a guide refresh.
/// </summary>
public sealed record RefreshResult(string GuidePath, string M3uPath, int ChannelCount, int ProgrammeCount, int M3uChannelCount, int TunerCount = 0, string GuideSource = "XMLTV");

/// <summary>
/// Discovered tuner information exposed to the setup page.
/// </summary>
public sealed record DiscoveredTuner(string Address, string DeviceId, string FriendlyName, int TunerCount);

/// <summary>
/// Converts flexible HDHomeRun boolean values such as
/// true/false, 0/1, and string equivalents.
/// </summary>
public sealed class FlexibleBooleanJsonConverter : JsonConverter<bool>
{
    /// <summary>
    /// Converts JSON values into a boolean.
    /// Supports true/false, 0/1, and string equivalents.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The target type.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>The converted boolean value.</returns>
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.TryGetInt32(out var i) && i == 1,
            JsonTokenType.String => ReadStringValue(reader.GetString()),
            JsonTokenType.Null => false,
            _ => false
        };
    }

    private static bool ReadStringValue(string? value)
    {
        return string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes a boolean value to JSON.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="options">Serializer options.</param>
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}
