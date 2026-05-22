using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HDHomeRunGuide.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HDHomeRunGuide.Tasks;

/// <summary>
/// Keeps plugin-managed guide data fresh without requiring a manual Jellyfin guide refresh.
/// </summary>
public sealed class GuideRefreshHostedService : IHostedService, IDisposable
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private readonly HDHomeRunGuideService _guideService;
    private readonly ILogger<GuideRefreshHostedService> _logger;
    private readonly CancellationTokenSource _stoppingTokenSource = new();
    private Task? _refreshLoopTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuideRefreshHostedService"/> class.
    /// </summary>
    /// <param name="guideService">Guide service.</param>
    /// <param name="logger">Logger.</param>
    public GuideRefreshHostedService(
        HDHomeRunGuideService guideService,
        ILogger<GuideRefreshHostedService> logger)
    {
        _guideService = guideService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _refreshLoopTask = RunAsync(_stoppingTokenSource.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stoppingTokenSource.CancelAsync().ConfigureAwait(false);

        if (_refreshLoopTask is not null)
        {
            await Task.WhenAny(_refreshLoopTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stoppingTokenSource.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(StartupDelay, cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                await RefreshIfDueAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(CheckInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "HDHomeRun guide background refresh loop stopped.");
        }
    }

    private async Task RefreshIfDueAsync(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        var config = plugin?.Configuration;

        if (config is null || string.IsNullOrWhiteSpace(config.TunerAddress))
        {
            return;
        }

        if (!IsRefreshDue(config.LastRefreshUtc, config.NextRefreshUtc, HDHomeRunGuideService.GetEffectiveRefreshIntervalHours(config)))
        {
            return;
        }

        try
        {
            _logger.LogInformation("HDHomeRun guide background refresh is due; starting refresh");
            await _guideService.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HDHomeRun guide background refresh failed");
        }
    }

    private static bool IsRefreshDue(string lastRefreshUtc, string nextRefreshUtc, int refreshIntervalHours)
    {
        var hours = Math.Clamp(refreshIntervalHours, 1, 168);
        if (DateTimeOffset.TryParse(
            nextRefreshUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var nextRefresh))
        {
            return DateTimeOffset.UtcNow >= nextRefresh.ToUniversalTime();
        }

        if (string.IsNullOrWhiteSpace(lastRefreshUtc)
            || !DateTimeOffset.TryParse(
                lastRefreshUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var lastRefresh))
        {
            return true;
        }

        return DateTimeOffset.UtcNow - lastRefresh.ToUniversalTime() >= TimeSpan.FromHours(hours);
    }
}
