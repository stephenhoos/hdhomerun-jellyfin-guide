# HDHomeRun Guide for Jellyfin

Server-side Jellyfin plugin that keeps Live TV guide data fresh for SiliconDust HDHomeRun tuners.

The plugin reads `DeviceAuth` from your HDHomeRun tuner, generates Jellyfin-compatible Live TV files, updates Jellyfin's M3U/XMLTV configuration, clears stale XMLTV cache entries, and triggers Jellyfin's guide import automatically.

## Why this exists

Jellyfin's native HDHomeRun guide flow can be unreliable depending on tuner, subscription, and cache state. This plugin keeps the setup inside Jellyfin while using SiliconDust's own guide APIs directly.

It supports two guide modes:

- **Standard guide API**: uses `https://api.hdhomerun.com/api/guide?Duration=24&DeviceAuth=...`.
- **Paid DVR XMLTV API**: uses `https://api.hdhomerun.com/api/xmltv?DeviceAuth=...` for users with SiliconDust DVR guide service. This can provide a much larger guide window.

## Features

- Configurable HDHomeRun tuner IP/URL from the Jellyfin plugin page.
- **Add My Tuners** button that uses Jellyfin's built-in HDHomeRun discovery.
- Optional subnet scan fallback when Jellyfin discovery returns no devices.
- Automatic background refresh on a configurable interval.
- Immediate refresh after saving plugin configuration.
- Standard and paid DVR XMLTV guide modes.
- Automatic Jellyfin Live TV M3U/XMLTV path management.
- Explicit Jellyfin channel mappings for M3U/XMLTV imports.
- Stale Jellyfin XMLTV cache deletion before each guide import.
- Correct HDHomeRun tuner count in Jellyfin's M3U tuner config.
- No `DeviceAuth` token is stored in plugin configuration or logs.

## Repository Layout

```text
Jellyfin.Plugin.HDHomeRunGuide/    Jellyfin server plugin source
scripts/                           Legacy standalone refresh helper
examples/                          macOS LaunchAgent example for standalone mode
hdhomerun_to_xmltv.py              Legacy standalone XMLTV/M3U generator
build.yaml                         Jellyfin plugin metadata
```

The Jellyfin plugin is the recommended path. The standalone Python script remains for troubleshooting or non-plugin deployments.

## Build

Install the .NET SDK for the target framework, then run:

```bash
dotnet build Jellyfin.Plugin.HDHomeRunGuide/Jellyfin.Plugin.HDHomeRunGuide.csproj -c Release
```

Build output is written under:

```text
Jellyfin.Plugin.HDHomeRunGuide/bin/Release/net9.0/
```

## Install

1. Create a plugin folder under your Jellyfin plugin directory.
2. Copy these files from the build output:
   - `Jellyfin.Plugin.HDHomeRunGuide.dll`
   - `Jellyfin.Plugin.HDHomeRunGuide.deps.json`
   - `Jellyfin.Plugin.HDHomeRunGuide.xml`
3. Restart Jellyfin.

Typical plugin directories:

```text
Linux: /var/lib/jellyfin/plugins/HDHomeRun Guide_0.2.0.1/
macOS: ~/Library/Application Support/jellyfin/plugins/HDHomeRun Guide_0.2.0.1/
```

## Configure

In Jellyfin:

1. Open **Dashboard -> Plugins -> HDHomeRun Guide**.
2. Click **Add My Tuners** to find your HDHomeRun with Jellyfin's built-in tuner discovery and configure Live TV automatically.
3. Choose the guide source:
   - Leave **Use paid DVR XMLTV guide data** unchecked for the standard SiliconDust guide API.
   - Check it if you have SiliconDust's paid DVR guide service.
4. Set the refresh interval.
5. Leave **Update Jellyfin Live TV M3U/XMLTV paths after refresh** enabled unless you want to manage Live TV manually.
6. Save.

Saving triggers an immediate refresh. Future refreshes run in the background.

You can still enter a tuner IP or URL manually, such as `192.168.1.4`, and use **Find HDHomeRun Tuners** for troubleshooting. Discovery is deduplicated by physical device, so a two-tuner HDHomeRun appears as one device with two available tuners. If no scan subnet is configured, the plugin infers private LAN `/24` ranges from the Jellyfin server network interfaces and never scans `127.0.0.1`.

## Notes

- The paid DVR XMLTV feed can be large. On a full guide import, Jellyfin may spend several minutes rebuilding its guide cache.
- In paid XMLTV mode, a longer refresh interval such as 12 hours is usually more appropriate than frequent 1-2 hour refreshes.
- Generated XMLTV/M3U files are local to the Jellyfin server and are ignored by git.
- Guide data may contain local channel metadata and tuner URLs; do not commit generated guide files.

## License

MIT
