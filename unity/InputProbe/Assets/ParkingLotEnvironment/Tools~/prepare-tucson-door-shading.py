"""External experiment only: confine the reviewed bounded normals to the side doors.
No original position/UV moves; original triangles outside the mask remain unsubdivided.
"""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
from pathlib import Path
import json, hashlib
import numpy as np

base=Path(__file__).resolve().parents[1] / 'SourceModels~' / 'tucson-door-reflections'
meta=json.loads((base/'tucson-paint-current.json').read_text())
raw=(base/'tucson-paint-current.bin').read_bytes()
v=np.frombuffer(raw,'<f4',meta['vertices']*8).reshape(-1,8)
ix=np.frombuffer(raw,'<u4',meta['triangles']*3,meta['vertices']*32).reshape(-1,3)
parent=v[ix].astype(np.float64)
candidate_raw=(base/'tucson-subdivision-experiment/hybrid-bounded.bin').read_bytes()
candidate=np.frombuffer(candidate_raw,'<f4',57264*8).reshape(4772,4,3,8).astype(np.float64)
# Candidate order is fixed by the reviewed subdivision writer.
bary=np.array([[1,0,0],[0,1,0],[0,0,1],[.5,.5,0],[0,.5,.5],[.5,0,.5]])
loc=np.array([[0,3,5],[3,1,4],[5,4,2],[3,4,5]])
expected=np.einsum('ckv,tvd->tckd',bary[loc],parent[:,:,:3])
assert np.max(np.abs(expected-candidate[:,:,:,0:3]))<2e-5
expected_uv=np.einsum('ckv,tvd->tckd',bary[loc],parent[:,:,6:8])
assert np.max(np.abs(expected_uv-candidate[:,:,:,6:8]))<1e-7
unit=meta['worldMetresPerLocalUnit']
xyz=candidate[:,:,:,0:3]*unit
x,y,z=np.abs(xyz[:,:,:,0]),xyz[:,:,:,1],xyz[:,:,:,2]
def smooth(a):
    a=np.clip(a,0,1)
    return a*a*(3-2*a)
# Soft spatial falloff stops before bonnet, lamps, lower sill and upper shoulder.
mask=smooth((x-.72)/.12)*smooth((z+.75)/.20)*smooth((1.35-z)/.20)*smooth((y-.45)/.15)*smooth((1.35-y)/.15)
selected=np.max(mask,axis=(1,2))>.00001
normal=np.einsum('ckv,tvd->tckd',bary[loc],parent[:,:,3:6])
normal/=np.maximum(np.linalg.norm(normal,axis=3,keepdims=True),1e-12)
outdir=base/'tucson-door-shading-experiment';outdir.mkdir(exist_ok=True)
reports={}
for strength in [.0,.5,.8]:
    out=candidate.copy();weight=mask[:,:,:,None]*strength
    newnormal=normal*(1-weight)+candidate[:,:,:,3:6]*weight
    newnormal/=np.maximum(np.linalg.norm(newnormal,axis=3,keepdims=True),1e-12)
    out[:,:,:,3:6]=newnormal
    chunks=[out[i].reshape(-1,8) if selected[i] else parent[i] for i in range(len(parent))]
    verts=np.concatenate(chunks).astype('<f4')
    assert np.isfinite(verts).all()
    assert np.max(np.abs(np.linalg.norm(verts[:,3:6],axis=1)-1))<1e-6
    indices=np.arange(len(verts),dtype='<u4')
    key='door-'+str(int(strength*100));data=verts.tobytes()+indices.tobytes()
    (outdir/(key+'.bin')).write_bytes(data)
    reports[key]={'vertices':len(verts),'triangles':len(verts)//3,'sha256':hashlib.sha256(data).hexdigest(),'position_uv_sha256':hashlib.sha256(np.ascontiguousarray(verts[:,[0,1,2,6,7]]).tobytes()).hexdigest(),'weight_max':float(weight.max()),'unchanged_parent_triangles':int((~selected).sum()),'subdivided_parent_triangles':int(selected.sum())}
assert len({r['position_uv_sha256'] for r in reports.values()})==1
(outdir/'report.json').write_text(json.dumps({'vertices':len(verts),'triangles':len(verts)//3,'source_sha256':hashlib.sha256(raw).hexdigest(),'source_vertices':meta['vertices'],'source_triangles':meta['triangles'],'world_metres_per_local_unit':unit,'note':'Temporary offline candidates. No original vertex or UV motion; only side-door normals and linear subdivision. Unselected original triangles untouched. Not adopted.','variants':reports},indent=2))
print(json.dumps(reports,indent=2))
