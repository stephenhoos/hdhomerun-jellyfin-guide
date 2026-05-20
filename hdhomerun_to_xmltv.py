#!/usr/bin/env python3
"""Fetch HDHomeRun guide data and write an XMLTV file for Jellyfin."""

from __future__ import annotations

import argparse
import gzip
import ipaddress
import json
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any
from xml.etree import ElementTree as ET


DEFAULT_TIMEOUT = 20
ALLOWED_IPV4_NETWORKS = (
    ipaddress.ip_network("10.0.0.0/8"),
    ipaddress.ip_network("172.16.0.0/12"),
    ipaddress.ip_network("192.168.0.0/16"),
    ipaddress.ip_network("169.254.0.0/16"),
)


def fetch_json(url: str, timeout: int = DEFAULT_TIMEOUT) -> Any:
    request = urllib.request.Request(url, headers={"Accept-Encoding": "gzip", "User-Agent": "hdhomerun-to-xmltv/1.0"})
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            charset = response.headers.get_content_charset() or "utf-8"
            return json.loads(read_response_body(response).decode(charset))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code} fetching {redact_url(url)}: {detail[:300]}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Unable to fetch {redact_url(url)}: {exc.reason}") from exc


def fetch_text(url: str, timeout: int = DEFAULT_TIMEOUT) -> str:
    request = urllib.request.Request(url, headers={"Accept-Encoding": "gzip", "User-Agent": "hdhomerun-to-xmltv/1.0"})
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            charset = response.headers.get_content_charset() or "utf-8"
            return read_response_body(response).decode(charset)
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code} fetching {redact_url(url)}: {detail[:300]}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Unable to fetch {redact_url(url)}: {exc.reason}") from exc


def read_response_body(response: Any) -> bytes:
    body = response.read()
    if response.headers.get("Content-Encoding", "").lower() == "gzip":
        return gzip.decompress(body)
    return body


def validate_local_http_url(url: str, label: str) -> str:
    parsed = urllib.parse.urlsplit(url)
    if parsed.scheme not in {"http", "https"}:
        raise RuntimeError(f"{label} must be an absolute HTTP or HTTPS URL")
    if not parsed.hostname:
        raise RuntimeError(f"{label} must include a host")

    try:
        address = ipaddress.ip_address(parsed.hostname)
    except ValueError as exc:
        raise RuntimeError(f"{label} must use a literal local IP address") from exc

    if not is_allowed_local_address(address):
        raise RuntimeError(f"{label} must point to a private, link-local, or loopback address")

    return urllib.parse.urlunsplit(parsed)


def is_allowed_local_address(address: ipaddress.IPv4Address | ipaddress.IPv6Address) -> bool:
    if address.is_loopback or address.is_link_local:
        return True
    if isinstance(address, ipaddress.IPv4Address):
        return any(address in network for network in ALLOWED_IPV4_NETWORKS)
    if isinstance(address, ipaddress.IPv6Address):
        return address in ipaddress.ip_network("fc00::/7")
    return False


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
            return validate_local_http_url(discovered, "HDHomeRun device URL")
        raise RuntimeError("No HDHomeRun device found. Install hdhomerun_config or pass --device.")
    if device.startswith(("http://", "https://")):
        return validate_local_http_url(device.rstrip("/"), "HDHomeRun device URL")
    return validate_local_http_url(f"http://{device.rstrip('/')}", "HDHomeRun device URL")


def build_xmltv_url(auth: str, request_paid_guide_window: bool = False) -> str:
    if not auth:
        raise RuntimeError("DeviceAuth is required to build the SiliconDust XMLTV URL")
    query = {"DeviceAuth": auth}
    if request_paid_guide_window:
        query["Duration"] = "14"
    return "https://api.hdhomerun.com/api/xmltv?" + urllib.parse.urlencode(query)


def validate_xmltv(xmltv: str) -> tuple[int, int]:
    try:
        root = ET.fromstring(xmltv)
    except ET.ParseError as exc:
        raise RuntimeError("SiliconDust XMLTV API did not return valid XMLTV XML") from exc

    if root.tag != "tv":
        raise RuntimeError("SiliconDust XMLTV API did not return a tv root element")

    return len(root.findall("channel")), len(root.findall("programme"))


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
        description="Download SiliconDust HDHomeRun XMLTV guide data for Jellyfin."
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
        "--print-source",
        action="store_true",
        help="Print the redacted XMLTV API URL used.",
    )
    parser.add_argument(
        "--paid",
        action="store_true",
        help="Ask SiliconDust for paid 14-day XMLTV data when available.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    device_url = normalize_device_url(args.device)

    discover = fetch_json(f"{device_url}/discover.json")
    auth = discover.get("DeviceAuth")
    lineup_url = validate_local_http_url(
        discover.get("LineupURL") or f"{device_url}/lineup.json",
        "HDHomeRun LineupURL",
    )
    if not auth:
        raise RuntimeError(f"No DeviceAuth found in {device_url}/discover.json")

    lineup = fetch_json(lineup_url)
    if not isinstance(lineup, list):
        raise RuntimeError("HDHomeRun LineupURL did not return a lineup list")

    guide_url = build_xmltv_url(auth, args.paid)
    if args.print_source:
        print(f"XMLTV API: {redact_url(guide_url)}", file=sys.stderr)
    xmltv = fetch_text(guide_url)
    channel_count, programme_count = validate_xmltv(xmltv)

    output = Path(args.out).expanduser().resolve()
    output.write_text(xmltv, encoding="utf-8")

    print(f"Wrote {output}")
    print(f"Channels: {channel_count}")
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
