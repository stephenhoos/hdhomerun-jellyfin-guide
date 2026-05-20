# Jellyfin.Plugin.HDHomeRunGuide

Jellyfin server plugin for SiliconDust HDHomeRun guide imports.

## What it does

- Stores the tuner IP/URL in Jellyfin plugin configuration.
- Can scan a local IPv4 subnet for HDHomeRun devices from the Jellyfin server.
- Refreshes automatically in the background on a randomized configurable interval.
- Still exposes a Jellyfin scheduled task for manual/admin runs.
- Uses SiliconDust's XMLTV API as the only guide source.
- Can explicitly request the paid 14-day XMLTV window by adding `Duration=14`.
- Writes `hdhomerun-guide.xml` and `hdhomerun-lineup.m3u` under the plugin data folder.
- Updates Jellyfin Live TV's M3U tuner and XMLTV provider paths after each refresh.
- Writes explicit channel mappings so Jellyfin can attach XMLTV programmes to M3U channels.
- Deletes Jellyfin's stale XMLTV provider cache before guide import.
- Shows the detected HDHomeRun tuner count in the Jellyfin M3U tuner name.
- Does not store the `DeviceAuth` token in plugin configuration or logs.

## Build

From the repository root:

```bash
dotnet build Jellyfin.Plugin.HDHomeRunGuide/Jellyfin.Plugin.HDHomeRunGuide.csproj -c Release
```

Or from this project folder:

```bash
dotnet build Jellyfin.Plugin.HDHomeRunGuide.csproj -c Release
```

## Setup

1. In Jellyfin, open **Dashboard -> Plugins -> HDHomeRun Guide**.
2. Enter the tuner IP address, or enter a subnet such as `192.168.1.0/24` and scan.
3. Check **Request paid 14-day XMLTV guide when available** if the tuner has SiliconDust DVR guide service.
4. Set the refresh interval.
5. Leave **Update Jellyfin Live TV M3U/XMLTV paths after refresh** enabled if you want the plugin to manage Live TV.
6. Save.

The plugin immediately refreshes guide data after saving and keeps it fresh automatically.

The plugin follows SiliconDust's XMLTV guidance by accepting gzip responses,
reading fresh `DeviceAuth` from the tuner for every refresh, and randomizing the
next automatic refresh time instead of downloading at a fixed wall-clock time.
