#!/bin/bash
set -eu
test ! -e /workspaces/parkside-setup/STOP
exec 9>/workspaces/parkside-setup/unity-job.lock
flock -n 9
if pgrep -x Unity >/dev/null; then exit 2; fi
kit=/workspaces/parkside-source/unity/InputProbe/Assets/ParkingLotEnvironment/CloudPilot
out=$(cat "$kit/Inspection~/full-canopy-current.txt")
entry="$1"
exec xvfb-run -a -s '-screen 0 1920x1080x24' python3 "$kit/Tools~/full-canopy-child-status.py" "ParkingLotEnvironment.CloudPilot.CloudPilotCanopyPass.$entry" "$out/editor-$entry.log" "$out/unity-child-$entry.json"
