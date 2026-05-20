#!/usr/bin/env python3
"""Fetch HDHomeRun guide data and write an XMLTV file for Jellyfin."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from xml.etree import ElementTree as ET


DEFAULT_TIMEOUT = 20


def fetch_json(url: str, timeout: int = DEFAULT_TIMEOUT) -> Any:
    request = urllib.request.Request(url, headers={"User-Agent": "hdhomerun-to-xmltv/1.0"})
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            charset = response.headers.get_content_charset() or "utf-8"
            return json.loads(response.read().decode(charset))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code} fetching {redact_url(url)}: {detail[:300]}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Unable to fetch {redact_url(url)}: {exc.reason}") from exc


def discover_device_url() -> str | None:
    try:
        proc = subprocess.run(
            ["hdhomerun_config", "discover"],
            check=False,
            capture_output=True,
            text=True,
            timeout=5,
        )
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return None

    for line in proc.stdout.splitlines():
        parts = line.split()
        if "found" in parts and "at" in parts:
            ip = parts[-1]
            if ":" not in ip:
                return f"http://{ip}"

    return None


def normalize_device_url(device: str | None) -> str:
    if not device:
        discovered = discover_device_url()
        if discovered:
            return discovered
        raise RuntimeError("No HDHomeRun device found. Install hdhomerun_config or pass --device.")
    if device.startswith(("http://", "https://")):
        return device.rstrip("/")
    return f"http://{device.rstrip('/')}"


def build_guide_url(auth: str, start: int | None, guide_number: str | None) -> str:
    query = {"DeviceAuth": auth}
    if start is not None:
        query["Start"] = str(start)
    if guide_number:
        query["GuideNumber"] = guide_number
    return "https://api.hdhomerun.com/api/guide?" + urllib.parse.urlencode(query)


def xmltv_time(epoch: int) -> str:
    return datetime.fromtimestamp(epoch, timezone.utc).strftime("%Y%m%d%H%M%S +0000")


def child(parent: ET.Element, tag: str, text: Any = None, **attrs: str) -> ET.Element:
    element = ET.SubElement(parent, tag, attrs)
    if text is not None and text != "":
        element.text = str(text)
    return element


def channel_id(entry: dict[str, Any]) -> str:
    return str(entry.get("GuideNumber") or entry.get("GuideName") or entry.get("Affiliate"))


def add_channels(root: ET.Element, guide: list[dict[str, Any]], lineup_by_number: dict[str, dict[str, Any]]) -> None:
    seen: set[str] = set()
    for entry in guide:
        guide_number = channel_id(entry)
        if not guide_number or guide_number in seen:
            continue
        seen.add(guide_number)

        lineup_entry = lineup_by_number.get(guide_number, {})
        guide_name = entry.get("GuideName") or lineup_entry.get("GuideName") or guide_number
        affiliate = entry.get("Affiliate")

        channel = child(root, "channel", id=guide_number)
        child(channel, "display-name", guide_number)
        if guide_name and guide_name != guide_number:
            child(channel, "display-name", guide_name)
        if affiliate and affiliate not in {guide_name, guide_number}:
            child(channel, "display-name", affiliate)
        if entry.get("ImageURL"):
            child(channel, "icon", src=str(entry["ImageURL"]))


def add_programmes(root: ET.Element, guide: list[dict[str, Any]]) -> int:
    count = 0
    for channel_entry in guide:
        guide_number = channel_id(channel_entry)
        for item in channel_entry.get("Guide") or []:
            start = item.get("StartTime")
            stop = item.get("EndTime")
            title = item.get("Title")
            if not guide_number or not start or not stop or not title:
                continue

            programme = child(
                root,
                "programme",
                start=xmltv_time(int(start)),
                stop=xmltv_time(int(stop)),
                channel=guide_number,
            )
            child(programme, "title", title, lang="en")
            child(programme, "sub-title", item.get("EpisodeTitle"), lang="en")
            child(programme, "desc", item.get("Synopsis"), lang="en")
            child(programme, "category", item.get("Category"), lang="en")
            child(programme, "date", item.get("OriginalAirdate"))

            if item.get("EpisodeNumber"):
                child(programme, "episode-num", item["EpisodeNumber"], system="onscreen")
            if item.get("ProgramID"):
                child(programme, "episode-num", item["ProgramID"], system="dd_progid")
            if item.get("ImageURL"):
                child(programme, "icon", src=str(item["ImageURL"]))

            count += 1
    return count


def write_m3u(lineup: list[dict[str, Any]], output: Path) -> int:
    count = 0
    lines = ["#EXTM3U"]
    for item in lineup:
        guide_number = item.get("GuideNumber")
        guide_name = item.get("GuideName") or guide_number
        url = item.get("URL")
        if not guide_number or not url:
            continue

        tvg_id = sanitize_m3u_text(str(guide_number))
        name = sanitize_m3u_text(str(guide_name))
        stream_url = sanitize_m3u_text(str(url))
        if not tvg_id or not stream_url:
            continue

        lines.append(f'#EXTINF:-1 tvg-id="{tvg_id}" tvg-name="{name}" tvg-chno="{tvg_id}",{name}')
        lines.append(stream_url)
        count += 1

    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return count


def sanitize_m3u_text(value: str) -> str:
    return "".join(ch for ch in value if ch not in "\r\n" and ord(ch) >= 32).strip().replace('"', "'")


def indent_xml(element: ET.Element) -> None:
    # ElementTree.indent exists in modern Python, but keep this function central.
    ET.indent(element, space="  ")


def redact_url(url: str) -> str:
    parsed = urllib.parse.urlsplit(url)
    query = urllib.parse.parse_qsl(parsed.query, keep_blank_values=True)
    redacted = [
        (key, "REDACTED" if key.lower() == "deviceauth" else value)
        for key, value in query
    ]
    return urllib.parse.urlunsplit(parsed._replace(query=urllib.parse.urlencode(redacted)))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Download HDHomeRun guide data and convert it to XMLTV for Jellyfin."
    )
    parser.add_argument(
        "--device",
        help="HDHomeRun base URL or IP address. Defaults to hdhomerun_config discovery.",
    )
    parser.add_argument(
        "--out",
        default="hdhomerun-guide.xml",
        help="Output XMLTV path. Default: hdhomerun-guide.xml",
    )
    parser.add_argument(
        "--m3u-out",
        help="Optional output M3U tuner path for Jellyfin.",
    )
    parser.add_argument(
        "--start",
        type=int,
        help="Unix epoch start time to request from SiliconDust. Default: API default.",
    )
    parser.add_argument(
        "--guide-number",
        help="Optional channel number filter, for example 2.1.",
    )
    parser.add_argument(
        "--print-source",
        action="store_true",
        help="Print the redacted guide API URL used.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    device_url = normalize_device_url(args.device)

    discover = fetch_json(f"{device_url}/discover.json")
    auth = discover.get("DeviceAuth")
    lineup_url = discover.get("LineupURL") or f"{device_url}/lineup.json"
    if not auth:
        raise RuntimeError(f"No DeviceAuth found in {device_url}/discover.json")

    lineup = fetch_json(lineup_url)
    lineup_by_number = {str(item.get("GuideNumber")): item for item in lineup if item.get("GuideNumber")}

    guide_url = build_guide_url(auth, args.start, args.guide_number)
    if args.print_source:
        print(f"Guide API: {redact_url(guide_url)}", file=sys.stderr)
    guide = fetch_json(guide_url)
    if not isinstance(guide, list):
        raise RuntimeError("Guide API did not return a channel list")
    if args.guide_number:
        guide = [entry for entry in guide if channel_id(entry) == args.guide_number]

    root = ET.Element(
        "tv",
        {
            "generator-info-name": "hdhomerun-to-xmltv",
            "source-info-name": "SiliconDust HDHomeRun",
        },
    )
    add_channels(root, guide, lineup_by_number)
    programme_count = add_programmes(root, guide)
    indent_xml(root)

    output = Path(args.out).expanduser().resolve()
    output.write_bytes(ET.tostring(root, encoding="utf-8", xml_declaration=True))

    print(f"Wrote {output}")
    print(f"Channels: {len(guide)}")
    print(f"Programmes: {programme_count}")

    if args.m3u_out:
        m3u_output = Path(args.m3u_out).expanduser().resolve()
        m3u_count = write_m3u(lineup, m3u_output)
        print(f"Wrote {m3u_output}")
        print(f"M3U channels: {m3u_count}")

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(1)
