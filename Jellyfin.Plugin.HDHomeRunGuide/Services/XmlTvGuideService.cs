using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HDHomeRunGuide.Services;

/// <summary>
/// Downloads XMLTV guide data from SiliconDust.
/// </summary>
public sealed class XmlTvGuideService
{
    private static readonly HttpClient HttpClient = new(
        new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    private readonly ILogger<XmlTvGuideService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTvGuideService"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public XmlTvGuideService(ILogger<XmlTvGuideService> logger)
    {
        _logger = logger;
        if (HttpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("jellyfin-plugin-hdhomerun-guide/0.3.2");
        }
    }

    /// <summary>
    /// Fetches XMLTV guide data from SiliconDust.
    /// </summary>
    /// <param name="deviceAuth">HDHomeRun DeviceAuth token.</param>
    /// <param name="accountEmail">Optional SiliconDust account email.</param>
    /// <param name="deviceIds">Optional comma-separated DeviceIDs for account email access.</param>
    /// <param name="requestPaidGuideWindow">Whether to ask for paid 14-day XMLTV data when available.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>XMLTV guide text.</returns>
    public async Task<string> GetXmlTvAsync(
        string deviceAuth,
        string accountEmail,
        string deviceIds,
        bool requestPaidGuideWindow,
        CancellationToken cancellationToken)
    {
        var url = BuildXmlTvUri(deviceAuth, accountEmail, deviceIds, requestPaidGuideWindow);
        _logger.LogInformation("Fetching HDHomeRun XMLTV guide data from {GuideUrl}", RedactGuideUrl(url));

        return await HttpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the SiliconDust XMLTV endpoint URI.
    /// </summary>
    /// <param name="deviceAuth">HDHomeRun DeviceAuth token.</param>
    /// <param name="accountEmail">Optional SiliconDust account email.</param>
    /// <param name="deviceIds">Optional comma-separated DeviceIDs for account email access.</param>
    /// <param name="requestPaidGuideWindow">Whether to ask for paid 14-day XMLTV data when available.</param>
    /// <returns>XMLTV endpoint URI.</returns>
    public static string BuildXmlTvUri(string deviceAuth, string accountEmail, string deviceIds, bool requestPaidGuideWindow)
    {
        var useAccountAccess = !string.IsNullOrWhiteSpace(accountEmail);
        if (useAccountAccess)
        {
            if (string.IsNullOrWhiteSpace(deviceIds))
            {
                throw new ArgumentException("DeviceIDs are required when using SiliconDust account email XMLTV access.", nameof(deviceIds));
            }

            var query = "Email="
                + Uri.EscapeDataString(accountEmail.Trim())
                + "&DeviceIDs="
                + Uri.EscapeDataString(NormalizeDeviceIds(deviceIds));
            return BuildApiUri(query, requestPaidGuideWindow);
        }

        if (string.IsNullOrWhiteSpace(deviceAuth))
        {
            throw new ArgumentException("DeviceAuth is required.", nameof(deviceAuth));
        }

        return BuildApiUri("DeviceAuth=" + Uri.EscapeDataString(deviceAuth), requestPaidGuideWindow);
    }

    private static string BuildApiUri(string query, bool requestPaidGuideWindow)
    {
        var builder = new UriBuilder(Uri.UriSchemeHttps, "api.hdhomerun.com")
        {
            Path = "api/xmltv",
            Query = requestPaidGuideWindow ? query + "&Duration=14" : query
        };
        return builder.Uri.ToString();
    }

    private static string NormalizeDeviceIds(string deviceIds)
    {
        return string.Join(
            ',',
            deviceIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string RedactGuideUrl(string url)
    {
        var question = url.IndexOf('?', StringComparison.Ordinal);
        if (question < 0)
        {
            return url;
        }

        return url[..question] + "?REDACTED";
    }
}
