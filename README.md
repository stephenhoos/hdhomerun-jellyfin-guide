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

The guide source is always SiliconDust's XMLTV endpoint:

```text
https://api.hdhomerun.com/api/xmltv?DeviceAuth=...
```

SiliconDust documents this as 2 days of guide data for everyone, or 14 days if
you have a HDHomeRun DVR guide subscription.

The public SiliconDust XMLTV documentation is here:

https://github.com/Silicondust/documentation/wiki/XMLTV-Guide-Data

That page documents the XMLTV URL, gzip requirement, fresh `DeviceAuth`
requirement, `Email` + `DeviceIDs` access option, and randomized scheduling
guidance. The plugin uses fresh `DeviceAuth` from the tuner by default so it
does not need to store SiliconDust account details. If you prefer the account
path, the plugin can use an optional SiliconDust account email and read the
DeviceID from the configured tuner.

## Features

- Configurable HDHomeRun tuner IP/URL from the Jellyfin plugin page.
- **Add My Tuners** button that uses Jellyfin's built-in HDHomeRun discovery.
- Optional subnet scan fallback when Jellyfin discovery returns no devices.
- Automatic background refresh on a randomized configurable interval.
- Immediate refresh after saving plugin configuration.
- XMLTV-only guide retrieval from SiliconDust, with all old `/api/guide` logic removed.
- Optional SiliconDust account email XMLTV access using the configured tuner's DeviceID.
- Optional paid guide request flag; SiliconDust decides the returned guide window from tuner/account entitlement.
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
0.3.2.0
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
Linux: /var/lib/jellyfin/plugins/HDHomeRun Guide_0.3.2.0/
macOS: ~/Library/Application Support/jellyfin/plugins/HDHomeRun Guide_0.3.2.0/
```

## Configure

In Jellyfin:

1. Open **Dashboard -> Plugins -> HDHomeRun Guide**.
2. Click **Add My Tuners** to find your HDHomeRun with Jellyfin's built-in tuner discovery and configure Live TV automatically.
3. Check **Request paid 14-day XMLTV guide when available** if you have SiliconDust DVR guide service.
4. Optionally enter your SiliconDust account email to use the documented `Email` + `DeviceIDs` XMLTV access path. The plugin reads DeviceID from the configured tuner.
5. Set the refresh interval.
6. Leave **Update Jellyfin Live TV M3U/XMLTV paths after refresh** enabled unless you want to manage Live TV manually.
7. Save.

Saving triggers an immediate refresh. Future refreshes run in the background.
Jellyfin also exposes a manual scheduled task named **Refresh HDHomeRun Guide**
for admins who want to run the XMLTV import by hand.

You can still enter a tuner IP or URL manually, such as `192.168.1.4`, and use **Find HDHomeRun Tuners** for troubleshooting. Discovery is deduplicated by physical device, so a two-tuner HDHomeRun appears as one device with two available tuners. If no scan subnet is configured, the plugin infers private LAN `/24` ranges from the Jellyfin server network interfaces and never scans `127.0.0.1`.

## Notes

- The XMLTV feed can be large, especially for DVR subscribers. On a full guide import, Jellyfin may spend several minutes rebuilding its guide cache.
- SiliconDust documents 2 days of guide for everyone and 14 days with HDHomeRun DVR guide service. Entitlement and SiliconDust server behavior control the actual guide span; in local testing, a DVR-entitled account returned about 7 days through both DeviceAuth and Email + DeviceIDs access.
- The default refresh interval is 36 hours for free XMLTV mode and 68 hours for paid 14-day XMLTV mode. The plugin randomizes the next automatic refresh around the configured interval so requests do not land at a fixed time each day. If SiliconDust's 14-day window starts returning consistently, the paid default should be doubled in a future release.
- SiliconDust documents `DeviceAuth` as changing regularly, so the plugin reads it from `discover.json` each time rather than storing it. Account email is only stored if you choose the optional Email + DeviceIDs access path, and DeviceID is read from the tuner.
- Generated XMLTV/M3U files are local to the Jellyfin server and are ignored by git.
- Guide data may contain local channel metadata and tuner URLs; do not commit generated guide files.

## License

MIT. Redistribution of this software or substantial portions of it must include
the copyright notice and MIT license text from [LICENSE](LICENSE).
