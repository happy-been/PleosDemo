#!/usr/bin/env bash
set -eu
export PARKSIDE_STOP_FILE=/workspaces/parkside-setup/STOP
project=/workspaces/parkside-source/unity/InputProbe
kit="$project/Assets/ParkingLotEnvironment/CloudPilot"
entry="${1:-ParkingLotEnvironment.CloudPilot.CloudPilotDaylightPass.ImproveAndCaptureStills}"
run_stamp=$(date -u +%Y%m%d-%H%M%S)
test ! -e "$PARKSIDE_STOP_FILE"
exec 9>/workspaces/parkside-setup/unity-job.lock
flock -n 9 || { echo 'Another Unity job is running'; exit 2; }
if pgrep -x Unity >/dev/null; then echo 'An existing Unity Editor is running'; exit 2; fi
python3 /workspaces/parkside-setup/verify-source.py --allow-parking-scene > "$kit/Inspection~/daylight-protected-before.json"
# Always verify and preserve results, including a TERM during wrapper cleanup.
finish_run() {
 run_result=$?
 trap - EXIT
 set +e
 python3 /workspaces/parkside-setup/verify-source.py --allow-parking-scene > "$kit/Inspection~/daylight-protected-after-$run_stamp.json"
 scope_result=$?
 printf 'WrapperExit=%s\nUnityExit=%s\nProtectedFilesExit=%s\n' "$run_result" "${unity_result:-NOT_RECORDED}" "$scope_result" > "$kit/Inspection~/daylight-run-$run_stamp.exit"
 cat "$kit/Inspection~/daylight-run-$run_stamp.exit"
 cat "$kit/Inspection~/daylight-protected-after-$run_stamp.json"
 if [ "$scope_result" -ne 0 ]; then exit 1; fi
 exit "$run_result"
}
trap finish_run EXIT
trap 'exit 143' TERM
trap 'exit 130' INT
set +e
timeout --signal=TERM --kill-after=20s 9m xvfb-run -a -s '-screen 0 1920x1080x24' \
 python3 "$kit/Tools~/unity-child-status.py" "$entry" \
 "$kit/Inspection~/daylight-editor-$run_stamp.log" \
 "$kit/Inspection~/unity-child-$run_stamp.json"
unity_result=$?
exit "$unity_result"
