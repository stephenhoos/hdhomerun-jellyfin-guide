using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jellyfin.Plugin.HDHomeRunGuide.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HDHomeRunGuide.Services;

/// <summary>
/// Keeps Jellyfin Live TV pointed at the plugin-managed guide files.
/// </summary>
public sealed class LiveTvConfigurator
{
    private const string TunerType = "hdhomerun";
    private const string LegacyTunerType = "m3u";
    private const string ListingsType = "xmltv";
    private const string FriendlyNamePrefix = "HDHomeRun Guide";
    private const string LegacyFriendlyName = "HDHomeRun M3U";
    private const string LegacyFriendlyNamePrefix = "HDHomeRun Guide M3U";
    private const string DefaultTunerHostId = "dedaf64fc6d34b51b88e64873ec088a5";
    private const string DefaultListingsProviderId = "c832ab9aa3354373aeecd9d80c483a7c";

    private readonly ITunerHostManager _tunerHostManager;
    private readonly IListingsManager _listingsManager;
    private readonly IGuideManager _guideManager;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly PluginLogService _pluginLog;
    private readonly ILogger<LiveTvConfigurator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveTvConfigurator"/> class.
    /// </summary>
    /// <param name="tunerHostManager">Tuner host manager.</param>
    /// <param name="listingsManager">Listings manager.</param>
    /// <param name="guideManager">Guide manager.</param>
    /// <param name="configurationManager">Server configuration manager.</param>
    /// <param name="pluginLog">Plugin diagnostic log.</param>
    /// <param name="logger">Logger.</param>
    public LiveTvConfigurator(
        ITunerHostManager tunerHostManager,
        IListingsManager listingsManager,
        IGuideManager guideManager,
        IServerConfigurationManager configurationManager,
        PluginLogService pluginLog,
        ILogger<LiveTvConfigurator> logger)
    {
        _tunerHostManager = tunerHostManager;
        _listingsManager = listingsManager;
        _guideManager = guideManager;
        _configurationManager = configurationManager;
        _pluginLog = pluginLog;
        _logger = logger;
    }

    /// <summary>
    /// Saves or updates Jellyfin Live TV tuner and listing provider entries.
    /// </summary>
    /// <param name="result">Guide refresh result.</param>
    /// <param name="discover">HDHomeRun discover info.</param>
    /// <param name="configuration">Plugin configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ConfigureAsync(
        RefreshResult result,
        DiscoverInfo discover,
        PluginConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var liveTvIds = FindExistingLiveTvIds(result);
        var tunerHostId = FirstNotEmpty(liveTvIds.TunerHostId, configuration.LiveTvTunerHostId, DefaultTunerHostId);
        var listingsProviderId = FirstNotEmpty(liveTvIds.ListingsProviderId, configuration.LiveTvListingProviderId, DefaultListingsProviderId);

        var tunerInfo = new TunerHostInfo
        {
            Id = tunerHostId,
            Type = TunerType,
            Url = HDHomeRunClient.NormalizeBaseUri(configuration.TunerAddress).ToString(),
            FriendlyName = BuildFriendlyName(discover.TunerCount),
            ImportFavoritesOnly = false,
            TunerCount = discover.TunerCount > 0 ? discover.TunerCount : 1,
            IgnoreDts = true
        };

        var listingInfo = new ListingsProviderInfo
        {
            Id = listingsProviderId,
            Type = ListingsType,
            Path = result.GuidePath,
            EnableAllTuners = true,
            ChannelMappings = BuildChannelMappings(result.M3uPath),
            NewsCategories = ["news", "journalism", "documentary", "current affairs"],
            SportsCategories = ["sports", "basketball", "baseball", "football"],
            KidsCategories = ["kids", "family", "children", "childrens", "disney"],
            MovieCategories = ["movie"]
        };

        await _tunerHostManager.SaveTunerHost(tunerInfo, true).ConfigureAwait(false);
        await _listingsManager.SaveListingProvider(listingInfo, false, false).ConfigureAwait(false);

        configuration.LiveTvTunerHostId = tunerHostId;
        configuration.LiveTvListingProviderId = listingsProviderId;

        _logger.LogInformation(
            "Updated Jellyfin Live TV HDHomeRun tuner {TunerHostId} and XMLTV provider {ListingsProviderId}",
            tunerHostId,
            listingsProviderId);

        PurgeXmlTvCache(listingsProviderId);
        await _guideManager.RefreshGuide(new Progress<double>(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Uses Jellyfin's built-in tuner discovery to find HDHomeRun devices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Built-in HDHomeRun tuner host discoveries.</returns>
    public async Task<IReadOnlyList<TunerHostInfo>> DiscoverHdhomerunTunersAsync(CancellationToken cancellationToken)
    {
        var results = new List<TunerHostInfo>();
        var inspectedCount = 0;

        await foreach (var tuner in _tunerHostManager.DiscoverTuners(false).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            inspectedCount++;
            _pluginLog.Info(
                "Jellyfin discovery candidate: Type="
                + EmptyForLog(tuner.Type)
                + ", FriendlyName="
                + EmptyForLog(tuner.FriendlyName)
                + ", Url="
                + EmptyForLog(tuner.Url)
                + ", DeviceId="
                + EmptyForLog(tuner.DeviceId)
                + ", TunerCount="
                + tuner.TunerCount);

            if (!IsHdhomerunTuner(tuner))
            {
                continue;
            }

            results.Add(tuner);
        }

        _pluginLog.Info($"Jellyfin discovery inspected {inspectedCount} candidates and accepted {results.Count} HDHomeRun candidates.");
        return results;
    }

    private (string TunerHostId, string ListingsProviderId) FindExistingLiveTvIds(RefreshResult result)
    {
        var configPath = Path.Combine(_configurationManager.ApplicationPaths.ConfigurationDirectoryPath, "livetv.xml");
        if (!File.Exists(configPath))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            var document = XDocument.Load(configPath);
            var tunerHost = document
                .Descendants("TunerHostInfo")
                .FirstOrDefault(item =>
                    IsManagedTunerType((string?)item.Element("Type"))
                    && (ContainsHdhomerunGuidePath((string?)item.Element("Url"))
                        || IsManagedFriendlyName((string?)item.Element("FriendlyName"))
                        || string.Equals((string?)item.Element("FriendlyName"), LegacyFriendlyName, StringComparison.OrdinalIgnoreCase)));

            var listingsProvider = document
                .Descendants("ListingsProviderInfo")
                .FirstOrDefault(item =>
                    string.Equals((string?)item.Element("Type"), ListingsType, StringComparison.OrdinalIgnoreCase)
                    && ContainsHdhomerunGuidePath((string?)item.Element("Path")));

            return ((string?)tunerHost?.Element("Id") ?? string.Empty, (string?)listingsProvider?.Element("Id") ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not inspect existing Live TV config at {ConfigPath}", configPath);
            return (string.Empty, string.Empty);
        }

        bool ContainsHdhomerunGuidePath(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (value.Contains("hdhomerun-lineup.m3u", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("hdhomerun-guide.xml", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, result.M3uPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, result.GuidePath, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string BuildFriendlyName(int tunerCount)
    {
        if (tunerCount <= 0)
        {
            return FriendlyNamePrefix;
        }

        var noun = tunerCount == 1 ? "tuner" : "tuners";
        return $"{FriendlyNamePrefix} ({tunerCount} {noun})";
    }

    private static bool IsManagedFriendlyName(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (value.StartsWith(FriendlyNamePrefix, StringComparison.OrdinalIgnoreCase)
                || value.StartsWith(LegacyFriendlyNamePrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsManagedTunerType(string? value)
    {
        return string.Equals(value, TunerType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, LegacyTunerType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHdhomerunTuner(TunerHostInfo tuner)
    {
        return string.Equals(tuner.Type, "hdhomerun", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(tuner.FriendlyName)
                && tuner.FriendlyName.Replace(" ", string.Empty, StringComparison.Ordinal)
                    .Contains("HDHomeRun", StringComparison.OrdinalIgnoreCase));
    }

    private static string EmptyForLog(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
    }

    private static NameValuePair[] BuildChannelMappings(string m3uPath)
    {
        if (string.IsNullOrWhiteSpace(m3uPath) || !File.Exists(m3uPath))
        {
            return [];
        }

        return File.ReadLines(m3uPath)
            .Where(line => line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            .Select(ReadTvgId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new NameValuePair { Name = id, Value = id })
            .ToArray();
    }

    private static string ReadTvgId(string line)
    {
        const string Attribute = "tvg-id=\"";
        var start = line.IndexOf(Attribute, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += Attribute.Length;
        var end = line.IndexOf('"', start);
        return end > start ? line[start..end] : string.Empty;
    }

    private void PurgeXmlTvCache(string listingsProviderId)
    {
        var cachePath = Path.Combine(
            _configurationManager.ApplicationPaths.CachePath,
            "xmltv",
            listingsProviderId + ".xml");

        try
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
                _logger.LogInformation("Deleted stale Jellyfin XMLTV cache at {CachePath}", cachePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete Jellyfin XMLTV cache at {CachePath}", cachePath);
        }
    }
}
