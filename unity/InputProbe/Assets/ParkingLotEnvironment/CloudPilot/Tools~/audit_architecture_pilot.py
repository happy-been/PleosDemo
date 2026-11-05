"""Read-only layout/camera/mesh guard for the single-building pilot checkpoint."""
from pathlib import Path
import re, json, hashlib

root = Path('/workspaces/parkside-source')
kit = root / 'unity/InputProbe/Assets/ParkingLotEnvironment/CloudPilot'
out = Path((kit / 'Inspection~/architecture-current.txt').read_text().strip())
backup = Path((out / 'checkpoint.txt').read_text().splitlines()[0].split('=', 1)[1])
relative = 'unity/InputProbe/Assets/_Parkside/Scenes/ParklingLot.unity'

def documents(path):
    parts = re.split(r'(?m)^--- !u!\d+ &(-?\d+)(?: stripped)?\n', path.read_text())
    return {int(parts[i]): parts[i+1] for i in range(1, len(parts), 2)}

before, after = documents(backup / relative), documents(root / relative)
layout = []
cameras = []
meshes = []
for key, doc in before.items():
    current = after.get(key, '')
    if doc.startswith(('Transform:', 'RectTransform:')):
        for field in ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale', 'm_Father'):
            pattern = rf'(?m)^  {field}: .*$'
            if re.findall(pattern, doc) != re.findall(pattern, current):
                layout.append([key, field])
    if doc.startswith('Camera:') and current != doc:
        cameras.append(key)
    if doc.startswith('MeshFilter:') and current != doc:
        meshes.append(key)

new_root = [key for key, doc in after.items() if '  m_Name: ArchitecturePass_BuildingPilot\n' in doc]
old_go = next(key for key, doc in before.items() if '  m_Name: ContextBuilding_-6_24\n' in doc)
report = {
    'building': 'ContextBuilding_-6_24',
    'footprint_metres': [8, 7],
    'body_height_metres': 9.6,
    'existing_transform_pose_or_parent_changes': layout,
    'existing_camera_document_changes': cameras,
    'existing_mesh_filter_document_changes': meshes,
    'owned_replacement_roots': new_root,
    'old_building_root_document_unchanged': before[old_go] == after.get(old_go),
    'scene_sha256': hashlib.sha256((root / relative).read_bytes()).hexdigest(),
}
(out / 'architecture-layout-audit.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
assert not (layout or cameras or meshes)
assert len(new_root) == 1
assert report['old_building_root_document_unchanged']
