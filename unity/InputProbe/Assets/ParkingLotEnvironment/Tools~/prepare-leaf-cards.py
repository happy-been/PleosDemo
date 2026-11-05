"""CC0 Tree Small 02: fit one masked planar card per connected leaf island.

Usage: py prepare-leaf-cards.py original.gltf output-directory
Requires numpy and scipy. Produces geometry only; all source images remain unchanged.
The input is the original glTF from https://polyhaven.com/a/tree_small_02.
Output uses source glTF coordinates/UVs. Unity conversion is in ParkingLotTreeLeafFinish.
"""
import argparse
import json
from pathlib import Path
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('source',type=Path)
parser.add_argument('output',type=Path)
args=parser.parse_args()
g=json.loads(args.source.read_text(encoding='utf-8'))
if len(g['nodes'])!=1 or any(k in g['nodes'][0] for k in ('matrix','rotation','scale','translation')):
    raise ValueError('This converter expects the reviewed source with one untransformed node.')
buffers=[np.memmap(args.source.parent/b['uri'],dtype=np.uint8,mode='r') for b in g['buffers']]
def acc(i):
    a=g['accessors'][i];v=g['bufferViews'][a['bufferView']]
    if 'sparse' in a:raise ValueError('Sparse accessors need an explicit decoder.')
    dt={5126:'<f4',5125:'<u4',5123:'<u2'}[a['componentType']]
    width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
    offset=v.get('byteOffset',0)+a.get('byteOffset',0)
    return np.ndarray((a['count'],width),dtype=dt,buffer=buffers[v['buffer']],offset=offset,strides=(v.get('byteStride',np.dtype(dt).itemsize*width),np.dtype(dt).itemsize))
p=next(p for mesh in g['meshes'] for p in mesh['primitives'] if g['materials'][p['material']]['name']=='tree_small_02_leaves')
pos=acc(p['attributes']['POSITION']);normal=acc(p['attributes']['NORMAL']);uv=acc(p['attributes']['TEXCOORD_0']);tri=acc(p['indices']).reshape(-1,3)
rows=np.concatenate([tri[:,0],tri[:,1]]);cols=np.concatenate([tri[:,1],tri[:,2]])
graph=coo_matrix((np.ones(len(rows),np.uint8),(rows,cols)),shape=(len(pos),len(pos))).tocsr()
count,labels=connected_components(graph,directed=False)
del graph,rows,cols
order=np.argsort(labels,kind='stable');sizes=np.bincount(labels);offsets=np.r_[0,sizes.cumsum()]
verts=[];indices=[];errors=[];normal_lengths=[]
for i in range(count):
    ids=order[offsets[i]:offsets[i+1]];u=uv[ids].astype(np.float64);points=pos[ids].astype(np.float64)
    a=np.column_stack([u,np.ones(len(u))]);fit=np.linalg.lstsq(a,points,rcond=None)[0]
    extent=np.linalg.norm(np.ptp(points,axis=0));rms=np.sqrt(np.mean(np.sum((a@fit-points)**2,axis=1)))
    errors.append(rms/max(extent,1e-9));lo=u.min(0);hi=u.max(0)
    corners=np.array([[lo[0],lo[1]],[hi[0],lo[1]],[hi[0],hi[1]],[lo[0],hi[1]]])
    xyz=np.column_stack([corners,np.ones(4)])@fit
    n=np.cross(fit[0],fit[1]);length=np.linalg.norm(n)
    if length<1e-12:raise ValueError('Degenerate island '+str(i))
    n/=length;source_n=normal[ids].mean(0);normal_lengths.append(float(np.linalg.norm(source_n)))
    forward=np.dot(n,source_n)>=0
    if not forward:n=-n
    start=len(verts)*4;verts.append(np.column_stack([xyz,np.tile(n,(4,1)),corners]))
    indices.extend((np.array([0,1,2,0,2,3]) if forward else np.array([0,2,1,0,3,2]))+start)
v=np.concatenate(verts).astype('<f4');idx=np.array(indices,dtype='<u4')
if not np.isfinite(v).all() or max(errors)>.1:raise ValueError('Unreviewed source geometry: failed finite/fit tolerance.')
args.output.mkdir(parents=True,exist_ok=True)
(args.output/'leaf-cards.bin').write_bytes(v.tobytes()+idx.tobytes())
report={'name':'CC0 Tree Small 02 leaf island planar cards','vertices':len(v),'triangles':len(idx)//3,'originalReducedLeafTriangles':75247,'originalLeafTriangles':len(tri),'islands':count,'normalizedRmsPercentiles':np.percentile(errors,[0,25,50,75,90,95,99,100]).tolist(),'rmsAbovePoint1':int(np.count_nonzero(np.array(errors)>.1)),'rmsAbovePoint2':int(np.count_nonzero(np.array(errors)>.2)),'normalLengthPercentiles':np.percentile(normal_lengths,[0,50,100]).tolist(),'source':'https://polyhaven.com/a/tree_small_02','license':'CC0 1.0','coordinateSystem':'original glTF XYZ, UV origin top-left; importer must flip Z, winding, V, then recenter using original near-tree bounds','bounds':{'min':v[:,:3].min(0).tolist(),'max':v[:,:3].max(0).tolist()}}
(args.output/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({'vertices':len(v),'triangles':len(idx)//3,'islands':count,'maxNormalizedRms':max(errors),'output':str(args.output)},indent=2))
