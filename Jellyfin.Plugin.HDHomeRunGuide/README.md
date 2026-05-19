# Jellyfin.Plugin.HDHomeRunGuide

Jellyfin server plugin for SiliconDust HDHomeRun guide imports.

## What it does

- Stores the tuner IP/URL in Jellyfin plugin configuration.
- Can scan a local IPv4 subnet for HDHomeRun devices from the Jellyfin server.
- Refreshes automatically in the background on a configurable interval.
- Still exposes a Jellyfin scheduled task for manual/admin runs.
- Supports standard SiliconDust JSON guide data and paid DVR XMLTV guide data.
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
3. Check **Use paid DVR XMLTV guide data** only if the tuner has SiliconDust DVR guide service.
4. Set the refresh interval.
5. Leave **Update Jellyfin Live TV M3U/XMLTV paths after refresh** enabled if you want the plugin to manage Live TV.
6. Save.

The plugin immediately refreshes guide data after saving and keeps it fresh automatically.
