"""Compare this bounded pass against its initial source snapshot; never restore files."""
from pathlib import Path
import hashlib, json, sys
root=Path('/workspaces/parkside-source')
kit=root/'unity/InputProbe/Assets/ParkingLotEnvironment/CloudPilot'
backup=Path((kit/'Inspection~/daylight-backup.txt').read_text().strip())
before=json.loads((backup/'pass-start-hashes.json').read_text())
current={}
for area in ('Assets','Packages','ProjectSettings'):
    for p in (root/'unity/InputProbe'/area).rglob('*'):
        if p.is_file():
            current[str(p.relative_to(root))]=hashlib.sha256(p.read_bytes()).hexdigest()
scene='unity/InputProbe/Assets/_Parkside/Scenes/ParklingLot.unity'
prefix='unity/InputProbe/Assets/ParkingLotEnvironment/CloudPilot/'
changed=[p for p in before if p not in current or before[p]!=current[p]]
added=[p for p in current if p not in before]
bad=[p for p in changed+added if p!=scene and not p.startswith(prefix)]
report={'baseline':str(backup/'pass-start-hashes.json'),'changed_existing':sorted(changed),'added':sorted(added),'outside_authorized_scope':sorted(bad),'scene_sha256':current.get(scene)}
(kit/'Inspection~/daylight-scope-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'changed_existing_count':len(changed),'added_count':len(added),'outside_authorized_scope':bad,'scene_sha256':current.get(scene)}))
sys.exit(bool(bad))
