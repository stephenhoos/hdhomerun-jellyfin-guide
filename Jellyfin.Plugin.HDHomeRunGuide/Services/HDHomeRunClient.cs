using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HDHomeRunGuide.Services;

/// <summary>
/// Talks to HDHomeRun devices.
/// </summary>
public sealed class HDHomeRunClient
{
    private static readonly HttpClient HttpClient = new(
        new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    private readonly ILogger<HDHomeRunClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HDHomeRunClient"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public HDHomeRunClient(ILogger<HDHomeRunClient> logger)
    {
        _logger = logger;
        if (!HttpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("jellyfin-plugin-hdhomerun-guide/0.1");
        }
    }

    /// <summary>
    /// Fetches discover.json from a tuner.
    /// </summary>
    /// <param name="address">Tuner address or base URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discover information.</returns>
    public async Task<DiscoverInfo> GetDiscoverInfoAsync(string address, CancellationToken cancellationToken)
    {
        var baseUri = NormalizeBaseUri(address);
        EnsureAllowedLocalHttpUri(baseUri, nameof(address));
        var discover = await HttpClient.GetFromJsonAsync<DiscoverInfo>(
            new Uri(baseUri, "discover.json"),
            cancellationToken).ConfigureAwait(false);

        if (discover is null || string.IsNullOrWhiteSpace(discover.DeviceAuth))
        {
            throw new InvalidOperationException($"No DeviceAuth found at {baseUri}discover.json");
        }

        if (string.IsNullOrWhiteSpace(discover.LineupUrl))
        {
            discover.LineupUrl = new Uri(baseUri, "lineup.json").ToString();
        }
        else
        {
            var lineupUri = new Uri(discover.LineupUrl, UriKind.Absolute);
            EnsureAllowedLocalHttpUri(lineupUri, nameof(discover.LineupUrl));
            discover.LineupUrl = lineupUri.ToString();
        }

        return discover;
    }

    /// <summary>
    /// Fetches the current lineup.
    /// </summary>
    /// <param name="discover">Discover information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lineup entries.</returns>
    public async Task<IReadOnlyList<LineupItem>> GetLineupAsync(DiscoverInfo discover, CancellationToken cancellationToken)
    {
        EnsureAllowedLocalHttpUri(new Uri(discover.LineupUrl, UriKind.Absolute), nameof(discover.LineupUrl));
        var lineup = await HttpClient.GetFromJsonAsync<List<LineupItem>>(
            discover.LineupUrl,
            cancellationToken).ConfigureAwait(false);

        return lineup ?? [];
    }

    /// <summary>
    /// Scans a subnet for HDHomeRun devices.
    /// </summary>
    /// <param name="subnet">CIDR subnet, for example 192.168.1.0/24.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered tuners.</returns>
    public async Task<IReadOnlyList<DiscoveredTuner>> DiscoverTunersAsync(string subnet, CancellationToken cancellationToken)
    {
        var addresses = ExpandSubnet(subnet).Take(254).ToList();
        var results = new List<DiscoveredTuner>();

        await Parallel.ForEachAsync(
            addresses,
            new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = cancellationToken },
            async (address, token) =>
            {
                try
                {
                    using var probeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                    probeTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
                    var discover = await GetDiscoverInfoAsync(address, probeTokenSource.Token).ConfigureAwait(false);
                    lock (results)
                    {
                        results.Add(new DiscoveredTuner(
                            address,
                            discover.DeviceId,
                            string.IsNullOrWhiteSpace(discover.FriendlyName) ? "HDHomeRun" : discover.FriendlyName,
                            discover.TunerCount));
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException or UriFormatException)
                {
                    _logger.LogDebug(ex, "No HDHomeRun discover.json at {Address}", address);
                }
            }).ConfigureAwait(false);

        return results.OrderBy(item => item.Address, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Normalizes a tuner address to a base URI.
    /// </summary>
    /// <param name="address">Tuner address.</param>
    /// <returns>Base URI.</returns>
    public static Uri NormalizeBaseUri(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Tuner address is required.", nameof(address));
        }

        var value = address.Trim();
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "http://" + value;
        }

        if (!value.EndsWith("/", StringComparison.Ordinal))
        {
            value += "/";
        }

        return new Uri(value, UriKind.Absolute);
    }

    private static void EnsureAllowedLocalHttpUri(Uri uri, string parameterName)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("HDHomeRun URLs must be absolute HTTP or HTTPS URLs.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || !IPAddress.TryParse(uri.Host, out var address))
        {
            throw new ArgumentException("HDHomeRun URLs must use a literal local IP address.", parameterName);
        }

        if (!IsAllowedLocalAddress(address))
        {
            throw new ArgumentException("HDHomeRun URLs must point to a private, link-local, or loopback address.", parameterName);
        }
    }

    private static bool IsAllowedLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal;
        }

        return false;
    }

    private static IEnumerable<string> ExpandSubnet(string subnet)
    {
        if (string.IsNullOrWhiteSpace(subnet))
        {
            throw new ArgumentException("Subnet is required.", nameof(subnet));
        }

        var parts = subnet.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix))
        {
            throw new ArgumentException("Use CIDR notation, for example 192.168.1.0/24.", nameof(subnet));
        }

        if (prefix < 24 || prefix > 30)
        {
            throw new ArgumentException("For safety, scans are limited to /24 through /30 subnets.", nameof(subnet));
        }

        var octets = parts[0].Split('.');
        if (octets.Length != 4 || octets.Any(o => !byte.TryParse(o, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            throw new ArgumentException("Invalid IPv4 subnet.", nameof(subnet));
        }

        var ip = octets.Select(o => byte.Parse(o, CultureInfo.InvariantCulture)).ToArray();
        var value = ((uint)ip[0] << 24) | ((uint)ip[1] << 16) | ((uint)ip[2] << 8) | ip[3];
        var mask = uint.MaxValue << (32 - prefix);
        var network = value & mask;
        var broadcast = network | ~mask;

        for (var candidate = network + 1; candidate < broadcast; candidate++)
        {
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"{(candidate >> 24) & 255}.{(candidate >> 16) & 255}.{(candidate >> 8) & 255}.{candidate & 255}");
        }
    }
}
