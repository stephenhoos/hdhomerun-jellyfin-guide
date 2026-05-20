using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Jellyfin.Plugin.HDHomeRunGuide.Services;

/// <summary>
/// Writes Jellyfin-compatible XMLTV and M3U files.
/// </summary>
public sealed class GuideWriter
{
    /// <summary>
    /// Writes the XMLTV and M3U files.
    /// </summary>
    /// <param name="guide">Guide channels.</param>
    /// <param name="lineup">Lineup entries.</param>
    /// <param name="outputDirectory">Output directory.</param>
    /// <param name="skipDrm">Whether to skip DRM channels in M3U.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refresh result.</returns>
    public async Task<RefreshResult> WriteAsync(
        IReadOnlyList<GuideChannel> guide,
        IReadOnlyList<LineupItem> lineup,
        string outputDirectory,
        bool skipDrm,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        PurgeOldGuideArtifacts(outputDirectory, TimeSpan.FromHours(24));

        var guidePath = Path.Combine(outputDirectory, "hdhomerun-guide.xml");
        var m3uPath = Path.Combine(outputDirectory, "hdhomerun-lineup.m3u");

        var lineupByNumber = lineup
            .Where(item => !string.IsNullOrWhiteSpace(item.GuideNumber))
            .GroupBy(item => item.GuideNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var settings = new XmlWriterSettings
        {
            Async = true,
            Encoding = new System.Text.UTF8Encoding(false),
            Indent = true
        };

        var programmeCount = 0;
        await using (var stream = File.Create(guidePath))
        using (var writer = XmlWriter.Create(stream, settings))
        {
            await writer.WriteStartDocumentAsync().ConfigureAwait(false);
            await writer.WriteStartElementAsync(null, "tv", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "generator-info-name", null, "jellyfin-plugin-hdhomerun-guide").ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "source-info-name", null, "SiliconDust HDHomeRun").ConfigureAwait(false);

            foreach (var channel in guide)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = ChannelId(channel);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var guideName = !string.IsNullOrWhiteSpace(channel.GuideName)
                    ? channel.GuideName
                    : lineupByNumber.GetValueOrDefault(id)?.GuideName ?? id;

                await writer.WriteStartElementAsync(null, "channel", null).ConfigureAwait(false);
                await writer.WriteAttributeStringAsync(null, "id", null, id).ConfigureAwait(false);
                await WriteTextElementAsync(writer, "display-name", id).ConfigureAwait(false);
                if (!string.Equals(guideName, id, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteTextElementAsync(writer, "display-name", guideName).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(channel.Affiliate)
                    && !string.Equals(channel.Affiliate, id, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(channel.Affiliate, guideName, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteTextElementAsync(writer, "display-name", channel.Affiliate).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(channel.ImageUrl))
                {
                    await writer.WriteStartElementAsync(null, "icon", null).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "src", null, channel.ImageUrl).ConfigureAwait(false);
                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            foreach (var channel in guide)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = ChannelId(channel);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                foreach (var program in channel.Guide ?? [])
                {
                    if (program.StartTime <= 0 || program.EndTime <= 0 || string.IsNullOrWhiteSpace(program.Title))
                    {
                        continue;
                    }

                    await writer.WriteStartElementAsync(null, "programme", null).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "start", null, XmlTvTime(program.StartTime)).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "stop", null, XmlTvTime(program.EndTime)).ConfigureAwait(false);
                    await writer.WriteAttributeStringAsync(null, "channel", null, id).ConfigureAwait(false);
                    await WriteTextElementAsync(writer, "title", program.Title, "en").ConfigureAwait(false);
                    await WriteTextElementAsync(writer, "sub-title", program.EpisodeTitle, "en").ConfigureAwait(false);
                    await WriteTextElementAsync(writer, "desc", program.Synopsis, "en").ConfigureAwait(false);
                    await WriteTextElementAsync(writer, "category", program.Category, "en").ConfigureAwait(false);
                    await WriteTextElementAsync(writer, "date", XmlTvDate(program.OriginalAirdate)).ConfigureAwait(false);
                    await WriteEpisodeNumberAsync(writer, program.EpisodeNumber, "onscreen").ConfigureAwait(false);
                    await WriteEpisodeNumberAsync(writer, program.ProgramId, "dd_progid").ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(program.ImageUrl))
                    {
                        await writer.WriteStartElementAsync(null, "icon", null).ConfigureAwait(false);
                        await writer.WriteAttributeStringAsync(null, "src", null, program.ImageUrl).ConfigureAwait(false);
                        await writer.WriteEndElementAsync().ConfigureAwait(false);
                    }

                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                    programmeCount++;
                }
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);
            await writer.WriteEndDocumentAsync().ConfigureAwait(false);
        }

        var m3uCount = await WriteM3uAsync(lineup, m3uPath, skipDrm, cancellationToken).ConfigureAwait(false);
        PurgeOldGuideArtifacts(outputDirectory, TimeSpan.FromHours(24));
        return new RefreshResult(guidePath, m3uPath, guide.Count, programmeCount, m3uCount);
    }

    /// <summary>
    /// Writes paid DVR XMLTV and M3U files.
    /// </summary>
    /// <param name="xmlTv">XMLTV guide text.</param>
    /// <param name="lineup">Lineup entries.</param>
    /// <param name="outputDirectory">Output directory.</param>
    /// <param name="skipDrm">Whether to skip DRM channels in M3U.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refresh result.</returns>
    public async Task<RefreshResult> WriteXmlTvAsync(
        string xmlTv,
        IReadOnlyList<LineupItem> lineup,
        string outputDirectory,
        bool skipDrm,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        PurgeOldGuideArtifacts(outputDirectory, TimeSpan.FromHours(24));

        var guidePath = Path.Combine(outputDirectory, "hdhomerun-guide.xml");
        var m3uPath = Path.Combine(outputDirectory, "hdhomerun-lineup.m3u");

        var document = XDocument.Parse(xmlTv, LoadOptions.PreserveWhitespace);
        var channelCount = document.Root?.Elements("channel").Count() ?? 0;
        var programmeCount = document.Root?.Elements("programme").Count() ?? 0;

        await File.WriteAllTextAsync(guidePath, xmlTv, cancellationToken).ConfigureAwait(false);
        var m3uCount = await WriteM3uAsync(lineup, m3uPath, skipDrm, cancellationToken).ConfigureAwait(false);
        PurgeOldGuideArtifacts(outputDirectory, TimeSpan.FromHours(24));

        return new RefreshResult(guidePath, m3uPath, channelCount, programmeCount, m3uCount, GuideSource: "XMLTV");
    }

    private static async Task<int> WriteM3uAsync(IReadOnlyList<LineupItem> lineup, string path, bool skipDrm, CancellationToken cancellationToken)
    {
        var count = 0;
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync("#EXTM3U").ConfigureAwait(false);

        foreach (var item in lineup)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (skipDrm && item.Drm)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.GuideNumber) || string.IsNullOrWhiteSpace(item.Url))
            {
                continue;
            }

            var guideNumber = SanitizeM3uText(item.GuideNumber);
            var name = SanitizeM3uText(string.IsNullOrWhiteSpace(item.GuideName) ? item.GuideNumber : item.GuideName);
            var url = SanitizeM3uText(item.Url);
            if (string.IsNullOrWhiteSpace(guideNumber) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            await writer.WriteLineAsync(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"#EXTINF:-1 tvg-id=\"{EscapeM3uAttribute(guideNumber)}\" tvg-name=\"{EscapeM3uAttribute(name)}\" tvg-chno=\"{EscapeM3uAttribute(guideNumber)}\",{name}")).ConfigureAwait(false);
            await writer.WriteLineAsync(url).ConfigureAwait(false);
            count++;
        }

        return count;
    }

    private static string ChannelId(GuideChannel entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.GuideNumber))
        {
            return entry.GuideNumber;
        }

        if (!string.IsNullOrWhiteSpace(entry.GuideName))
        {
            return entry.GuideName;
        }

        return entry.Affiliate;
    }

    private static string XmlTvTime(long epoch)
    {
        return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("yyyyMMddHHmmss +0000", CultureInfo.InvariantCulture);
    }

    private static string XmlTvDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch)
            && epoch > 31_536_000)
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        if (trimmed.Length >= 8
            && trimmed.Take(8).All(char.IsDigit))
        {
            return trimmed[..8];
        }

        return string.Empty;
    }

    private static async Task WriteTextElementAsync(XmlWriter writer, string name, string? value, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        await writer.WriteStartElementAsync(null, name, null).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(language))
        {
            await writer.WriteAttributeStringAsync(null, "lang", null, language).ConfigureAwait(false);
        }

        await writer.WriteStringAsync(value).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WriteEpisodeNumberAsync(XmlWriter writer, string? value, string system)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        await writer.WriteStartElementAsync(null, "episode-num", null).ConfigureAwait(false);
        await writer.WriteAttributeStringAsync(null, "system", null, system).ConfigureAwait(false);
        await writer.WriteStringAsync(value).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static string EscapeM3uAttribute(string value)
    {
        return value.Replace("\"", "'", StringComparison.Ordinal);
    }

    private static string SanitizeM3uText(string value)
    {
        return new string(value
            .Where(ch => ch is not '\r' and not '\n' && !char.IsControl(ch))
            .ToArray())
            .Trim();
    }

    private static void PurgeOldGuideArtifacts(string outputDirectory, TimeSpan maximumAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maximumAge;
        foreach (var pattern in new[] { "hdhomerun-guide*.xml", "hdhomerun-lineup*.m3u", "*.tmp" })
        {
            foreach (var path in Directory.EnumerateFiles(outputDirectory, pattern, SearchOption.TopDirectoryOnly))
            {
                var file = new FileInfo(path);
                if (file.LastWriteTimeUtc >= cutoff.UtcDateTime)
                {
                    continue;
                }

                file.Delete();
            }
        }
    }
}
