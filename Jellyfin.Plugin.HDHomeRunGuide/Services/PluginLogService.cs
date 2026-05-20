using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HDHomeRunGuide.Services;

/// <summary>
/// Keeps recent plugin diagnostics available from the configuration page.
/// </summary>
public sealed class PluginLogService
{
    private const int MaxEntries = 250;
    private const string LogFileName = "hdhomerun-guide-plugin.log";
    private readonly ConcurrentQueue<string> _entries = new();
    private readonly ILogger<PluginLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLogService"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public PluginLogService(ILogger<PluginLogService> logger)
    {
        _logger = logger;
        Info("HDHomeRun Guide diagnostics initialized. Build=0.1.7.0-security-fixes");
    }

    /// <summary>
    /// Adds an informational diagnostic line.
    /// </summary>
    /// <param name="message">Message.</param>
    public void Info(string message)
    {
        Add("INFO", message);
        _logger.LogInformation("{Message}", message);
    }

    /// <summary>
    /// Adds a warning diagnostic line.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="exception">Optional exception.</param>
    public void Warning(string message, Exception? exception = null)
    {
        Add("WARN", exception is null ? message : message + " " + exception.Message);
        _logger.LogWarning(exception, "{Message}", message);
    }

    /// <summary>
    /// Adds an error diagnostic line.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="exception">Optional exception.</param>
    public void Error(string message, Exception? exception = null)
    {
        Add("ERROR", exception is null ? message : message + " " + exception.Message);
        _logger.LogError(exception, "{Message}", message);
    }

    /// <summary>
    /// Gets recent diagnostic lines.
    /// </summary>
    /// <returns>Recent diagnostic lines.</returns>
    public IReadOnlyList<string> GetRecent()
    {
        var logPath = GetLogPath();
        if (!string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath))
        {
            return File.ReadLines(logPath).TakeLast(MaxEntries).ToList();
        }

        return _entries.ToArray();
    }

    private void Add(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {RedactSecrets(message)}";
        _entries.Enqueue(line);
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }

        var logPath = GetLogPath();
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not write HDHomeRun Guide plugin log");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Could not write HDHomeRun Guide plugin log");
        }
    }

    private static string GetLogPath()
    {
        return Plugin.Instance is null
            ? string.Empty
            : Path.Combine(Plugin.Instance.DataFolderPath, LogFileName);
    }

    private static string RedactSecrets(string value)
    {
        var index = value.IndexOf("DeviceAuth=", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return value;
        }

        var end = value.IndexOf('&', index);
        return end < 0
            ? value[..index] + "DeviceAuth=REDACTED"
            : value[..index] + "DeviceAuth=REDACTED" + value[end..];
    }
}
