from pathlib import Path
import hashlib,json,re,sys
root=Path('/workspaces/parkside-source');kit=root/'unity/InputProbe/Assets/ParkingLotEnvironment/CloudPilot';out=Path((kit/'Inspection~/full-canopy-current.txt').read_text().strip());raw=json.loads((out/'scope-before.json').read_text());areas=tuple('unity/InputProbe/'+a+'/' for a in ('Assets','Packages','ProjectSettings'));before={p:h for p,h in raw.items() if p.startswith(areas)};current={}
for area in ('Assets','Packages','ProjectSettings'):
 for p in (root/'unity/InputProbe'/area).rglob('*'):
  if p.is_file():current[str(p.relative_to(root))]=hashlib.sha256(p.read_bytes()).hexdigest()
scene='unity/InputProbe/Assets/_Parkside/Scenes/ParklingLot.unity';prefix='unity/InputProbe/Assets/ParkingLotEnvironment/CloudPilot/'
changed=[p for p in before if p not in current or current[p]!=before[p]];added=[p for p in current if p not in before];bad=[p for p in changed+added if p!=scene and not p.startswith(prefix)]
report={'changed_existing':sorted(changed),'added':sorted(added),'outside_authorized_scope':bad,'scene_sha256':current.get(scene)};(out/'scope-audit.json').write_text(json.dumps(report,indent=2))
backup=Path((out/'checkpoint.txt').read_text().splitlines()[0].split('=',1)[1])
def docs(p):
 s=p.read_text();parts=re.split(r'(?m)^--- !u!\d+ &(-?\d+)(?: stripped)?\n',s);return {int(parts[i]):parts[i+1] for i in range(1,len(parts),2)}
a=docs(backup/scene);b=docs(root/scene);go=next(k for k,v in a.items() if '  m_Name: ParkingLot_V35_Preserved\n' in v);targets={go};transforms={k:v for k,v in a.items() if v.startswith(('Transform:','RectTransform:'))};todo=[k for k,v in transforms.items() if f'm_GameObject: {{fileID: {go}}}' in v]
children={}
for k,v in transforms.items():
 m=re.search(r'm_Father: \{fileID: (-?\d+)\}',v)
 if m:children.setdefault(int(m[1]),[]).append(k)
while todo:
 t=todo.pop()
 if t in targets:continue
 targets.add(t);v=a[t];m=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',v)
 if m:targets.add(int(m[1]))
 todo.extend(children.get(t,[]))
for k,v in a.items():
 m=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',v)
 if m and int(m[1]) in targets:targets.add(k)
changed_docs=[k for k in targets if a[k]!=b.get(k)];preserved={'documents_checked':len(targets),'changed_documents':changed_docs,'inactive_before':'  m_IsActive: 0\n' in a[go],'inactive_after':'  m_IsActive: 0\n' in b[go]};(out/'preserved-audit.json').write_text(json.dumps(preserved,indent=2))
print(json.dumps({'scope_changes':len(changed),'scope_additions':len(added),'outside_scope':bad,'preserved':preserved}));sys.exit(bool(bad or changed_docs))
