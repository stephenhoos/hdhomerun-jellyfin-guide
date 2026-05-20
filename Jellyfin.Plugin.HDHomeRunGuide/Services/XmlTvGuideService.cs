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
        if (!HttpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("jellyfin-plugin-hdhomerun-guide/0.2");
        }
    }

    /// <summary>
    /// Fetches XMLTV guide data from SiliconDust.
    /// </summary>
    /// <param name="deviceAuth">HDHomeRun DeviceAuth token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>XMLTV guide text.</returns>
    public async Task<string> GetXmlTvAsync(string deviceAuth, CancellationToken cancellationToken)
    {
        var url = BuildXmlTvUri(deviceAuth);
        _logger.LogInformation("Fetching HDHomeRun XMLTV guide data from {GuideUrl}", RedactGuideUrl(url));

        return await HttpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the SiliconDust XMLTV endpoint URI.
    /// </summary>
    /// <param name="deviceAuth">HDHomeRun DeviceAuth token.</param>
    /// <returns>XMLTV endpoint URI.</returns>
    public static string BuildXmlTvUri(string deviceAuth)
    {
        if (string.IsNullOrWhiteSpace(deviceAuth))
        {
            throw new ArgumentException("DeviceAuth is required.", nameof(deviceAuth));
        }

        return "https://api.hdhomerun.com/api/xmltv?DeviceAuth=" + Uri.EscapeDataString(deviceAuth);
    }

    private static string RedactGuideUrl(string url)
    {
        var question = url.IndexOf('?', StringComparison.Ordinal);
        if (question < 0)
        {
            return url;
        }

        return url[..question] + "?DeviceAuth=REDACTED";
    }
}
