using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Jellyfin.Plugin.HDHomeRunGuide.Services;

/// <summary>
/// Writes Jellyfin-compatible XMLTV and M3U files.
/// </summary>
public static class GuideWriter
{
    /// <summary>
    /// Writes SiliconDust XMLTV and M3U files.
    /// </summary>
    /// <param name="xmlTv">XMLTV guide text.</param>
    /// <param name="lineup">Lineup entries.</param>
    /// <param name="outputDirectory">Output directory.</param>
    /// <param name="skipDrm">Whether to skip DRM channels in M3U.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refresh result.</returns>
    public static async Task<RefreshResult> WriteXmlTvAsync(
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
        if (!string.Equals(document.Root?.Name.LocalName, "tv", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("SiliconDust XMLTV response did not contain a tv root element.");
        }

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
