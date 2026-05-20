#!/bin/zsh
set -euo pipefail

PROJECT_DIR="${HDHOMERUN_JELLYFIN_DIR:-$HOME/Library/Application Support/hdhomerun-jellyfin}"
SCRIPT="$PROJECT_DIR/hdhomerun_to_xmltv.py"
XMLTV="$PROJECT_DIR/hdhomerun-guide.xml"
M3U="$PROJECT_DIR/hdhomerun-lineup.m3u"
JELLYFIN_DB="${JELLYFIN_DB:-$HOME/Library/Application Support/jellyfin/data/jellyfin.db}"
JELLYFIN_URL="${JELLYFIN_URL:-http://127.0.0.1:8096}"
HDHOMERUN_DEVICE="${HDHOMERUN_DEVICE:-}"
LOG_DIR="$PROJECT_DIR/logs"

mkdir -p "$LOG_DIR"

log() {
  printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S %z')" "$*"
}

log "Refreshing HDHomeRun XMLTV and M3U files"
args=(--out "$XMLTV" --m3u-out "$M3U")
if [[ -n "$HDHOMERUN_DEVICE" ]]; then
  args+=(--device "$HDHOMERUN_DEVICE")
fi
python3 "$SCRIPT" "${args[@]}"

if ! curl -fsS --max-time 10 "$JELLYFIN_URL/System/Info/Public" >/dev/null; then
  log "Jellyfin is not reachable at $JELLYFIN_URL; files were updated but guide import was skipped"
  exit 0
fi

if [[ ! -f "$JELLYFIN_DB" ]]; then
  log "Jellyfin database not found at $JELLYFIN_DB; files were updated but guide import was skipped"
  exit 0
fi

if [[ ! "$JELLYFIN_URL" =~ ^https?://(127\.0\.0\.1|localhost|::1|10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[0-1])\.) ]]; then
  log "Refusing to send Jellyfin token to non-local URL: $JELLYFIN_URL"
  exit 1
fi

token="$(sqlite3 "$JELLYFIN_DB" "select AccessToken from Devices order by DateLastActivity desc limit 1;")"
if [[ -z "$token" ]]; then
  log "No Jellyfin access token found; files were updated but guide import was skipped"
  exit 0
fi

task_id="$(
  curl -fsS --max-time 20 -H "X-Emby-Token: $token" "$JELLYFIN_URL/ScheduledTasks" \
    | jq -r '.[] | select(.Key=="RefreshGuide") | .Id'
)"
if [[ -z "$task_id" || "$task_id" == "null" ]]; then
  log "Jellyfin Refresh Guide task was not found"
  exit 1
fi

state="$(
  curl -fsS --max-time 20 -H "X-Emby-Token: $token" "$JELLYFIN_URL/ScheduledTasks/$task_id" \
    | jq -r '.State'
)"
if [[ "$state" != "Idle" ]]; then
  log "Refresh Guide is already $state; leaving the regenerated files for the active import"
  exit 0
fi

log "Starting Jellyfin Refresh Guide task"
curl -fsS --max-time 20 -X POST -H "X-Emby-Token: $token" "$JELLYFIN_URL/ScheduledTasks/Running/$task_id" >/dev/null

for _ in {1..240}; do
  sleep 3
  task="$(
    curl -fsS --max-time 20 -H "X-Emby-Token: $token" "$JELLYFIN_URL/ScheduledTasks/$task_id"
  )"
  state="$(printf '%s' "$task" | jq -r '.State')"
  [[ "$state" == "Idle" ]] && break
done

task="$(
  curl -fsS --max-time 20 -H "X-Emby-Token: $token" "$JELLYFIN_URL/ScheduledTasks/$task_id"
)"
state="$(printf '%s' "$task" | jq -r '.State')"
result_status="$(printf '%s' "$task" | jq -r '.LastExecutionResult.Status // "Unknown"')"
ended="$(printf '%s' "$task" | jq -r '.LastExecutionResult.EndTimeUtc // "Unknown"')"

programmes="$(
  curl -fsS --max-time 20 -H "X-Emby-Token: $token" "$JELLYFIN_URL/LiveTv/Programs?Limit=1" \
    | jq -r '.TotalRecordCount'
)"
channels="$(
  curl -fsS --max-time 20 -H "X-Emby-Token: $token" "$JELLYFIN_URL/LiveTv/Channels?Limit=1" \
    | jq -r '.TotalRecordCount'
)"

log "Jellyfin Refresh Guide state=$state status=$result_status ended=$ended channels=$channels programmes=$programmes"

if [[ "$state" != "Idle" || "$result_status" != "Completed" ]]; then
  exit 1
fi
