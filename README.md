# HDHomeRun Guide for Jellyfin

Server-side Jellyfin plugin that keeps Live TV XMLTV guide data fresh for
SiliconDust HDHomeRun tuners.

The plugin reads fresh `DeviceAuth` from your HDHomeRun tuner, downloads
SiliconDust's official XMLTV feed, writes Jellyfin-compatible Live TV files,
updates Jellyfin's M3U/XMLTV configuration, clears stale XMLTV cache entries,
and triggers Jellyfin's guide import automatically.

## Why this exists

Jellyfin's native HDHomeRun guide flow can be unreliable depending on tuner,
subscription, and cache state. This plugin keeps the setup inside Jellyfin while
using SiliconDust's official XMLTV API directly.

At SiliconDust/HDHomeRun's request, the plugin was changed to stop using the
render-oriented `/api/guide` endpoint and now uses the XMLTV endpoint as its only
guide source. The plugin is now an XMLTV cache and Jellyfin Live TV configurator,
not a guide reconstruction layer.

The guide source is always:

```text
https://api.hdhomerun.com/api/xmltv?DeviceAuth=...
```

SiliconDust has indicated this XMLTV endpoint now supports free guide access and
automatically provides the DVR/subscription guide depth when the tuner token is
entitled to it.

The public SiliconDust XMLTV documentation is here:

https://github.com/Silicondust/documentation/wiki/XMLTV-Guide-Data

That page documents the XMLTV URL, gzip requirement, fresh `DeviceAuth`
requirement, the newer `Email` + `DeviceIDs` access option, and randomized
scheduling guidance. The plugin uses fresh `DeviceAuth` from the tuner on every
refresh so it does not need to store SiliconDust account details. Public
documentation may lag Nick's newer note that free 3-day XMLTV access is
available without a DVR account.

## Features

- Configurable HDHomeRun tuner IP/URL from the Jellyfin plugin page.
- **Add My Tuners** button that uses Jellyfin's built-in HDHomeRun discovery.
- Optional subnet scan fallback when Jellyfin discovery returns no devices.
- Automatic background refresh on a randomized configurable interval.
- Immediate refresh after saving plugin configuration.
- XMLTV-only guide retrieval from SiliconDust, with all old `/api/guide` logic removed.
- Optional paid guide request flag that appends `Duration=14`; SiliconDust may still decide the returned window from tuner/account entitlement.
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

Current plugin version:

```text
0.3.0.0
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
Linux: /var/lib/jellyfin/plugins/HDHomeRun Guide_0.3.0.0/
macOS: ~/Library/Application Support/jellyfin/plugins/HDHomeRun Guide_0.3.0.0/
```

## Configure

In Jellyfin:

1. Open **Dashboard -> Plugins -> HDHomeRun Guide**.
2. Click **Add My Tuners** to find your HDHomeRun with Jellyfin's built-in tuner discovery and configure Live TV automatically.
3. Check **Request paid 14-day XMLTV guide when available** if you have SiliconDust DVR guide service.
4. Set the refresh interval.
5. Leave **Update Jellyfin Live TV M3U/XMLTV paths after refresh** enabled unless you want to manage Live TV manually.
6. Save.

Saving triggers an immediate refresh. Future refreshes run in the background.
Jellyfin also exposes a manual scheduled task named **Refresh HDHomeRun Guide**
for admins who want to run the XMLTV import by hand.

You can still enter a tuner IP or URL manually, such as `192.168.1.4`, and use **Find HDHomeRun Tuners** for troubleshooting. Discovery is deduplicated by physical device, so a two-tuner HDHomeRun appears as one device with two available tuners. If no scan subnet is configured, the plugin infers private LAN `/24` ranges from the Jellyfin server network interfaces and never scans `127.0.0.1`.

## Notes

- The XMLTV feed can be large, especially for DVR subscribers. On a full guide import, Jellyfin may spend several minutes rebuilding its guide cache.
- In testing, SiliconDust returned the same paid 14-day XMLTV feed with or without `Duration=14` when the tuner token had DVR entitlement. The checkbox keeps the request explicit, but entitlement appears to control the actual guide span.
- The default refresh interval is 36 hours for free XMLTV mode and 168 hours for paid 14-day XMLTV mode. The plugin randomizes the next automatic refresh around the configured interval so requests do not land at a fixed time each day.
- SiliconDust documents `DeviceAuth` as changing regularly, so the plugin reads it from `discover.json` each time rather than storing it.
- Generated XMLTV/M3U files are local to the Jellyfin server and are ignored by git.
- Guide data may contain local channel metadata and tuner URLs; do not commit generated guide files.

## License

MIT. Redistribution of this software or substantial portions of it must include
the copyright notice and MIT license text from [LICENSE](LICENSE).
