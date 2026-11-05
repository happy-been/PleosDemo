"""Author meter-scale, real mesh pilot parts. Does not touch the saved scene.

Only Python's standard library is needed. Dimensions are fictional design choices,
not measurements of the reference image or a verified regulatory layout.
"""
from pathlib import Path
import json, math, hashlib

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Geometry'
OUT.mkdir(exist_ok=True)
meshes = {}

class Mesh:
    def __init__(self, name):
        self.name, self.vertices, self.uv, self.triangles = name, [], [], []
        meshes[name] = self

    def quad(self, pts, uv=None):
        n = len(self.vertices)
        self.vertices.extend(pts)
        if uv is None:
            u=[pts[1][i]-pts[0][i] for i in range(3)]
            v=[pts[2][i]-pts[0][i] for i in range(3)]
            normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
            axis=max(range(3), key=lambda i:abs(normal[i]))
            axes=[i for i in range(3) if i != axis]
            uv=[(p[axes[0]],p[axes[1]]) for p in pts]
        self.uv.extend(uv)
        self.triangles.extend([(n,n+1,n+2),(n,n+2,n+3)])

    def box(self, x0,y0,z0,x1,y1,z1):
        # CCW exterior faces, Y up. UVs in physical meters for surface materials.
        assert x1>x0 and y1>y0 and z1>z0
        a,b,c,d=(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0)
        e,f,g,h=(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)
        for pts,w,t in [([b,a,d,c],x1-x0,y1-y0),([e,f,g,h],x1-x0,y1-y0),
                        ([a,e,h,d],z1-z0,y1-y0),([f,b,c,g],z1-z0,y1-y0),
                        ([d,h,g,c],x1-x0,z1-z0),([a,b,f,e],x1-x0,z1-z0)]:
            self.quad(pts)

    def curb(self, x0, length, z=-.22):
        # 30 mm beveled top corners; actual cross section, not a shading trick.
        p=[(z,-.08),(z+.22,-.08),(z+.22,.15),(z+.19,.18),(z+.03,.18),(z,.15)]
        n=len(self.vertices); first=len(self.triangles)
        self.vertices.extend([(x0,y,zz) for zz,y in p]+[(x0+length,y,zz) for zz,y in p])
        self.uv.extend([(zz,y) for zz,y in p]*2)
        for i in range(1,len(p)-1):
            self.triangles.append((n,n+i+1,n+i))
            self.triangles.append((n+len(p),n+len(p)+i,n+len(p)+i+1))
        for i in range(len(p)):
            j=(i+1)%len(p)
            self.triangles.extend([(n+i,n+j,n+len(p)+j),(n+i,n+len(p)+j,n+len(p)+i)])
        self.triangles[first:]=[(a,c,b) for a,b,c in self.triangles[first:]]

    def write(self):
        for tri in self.triangles:
            a,b,c=[self.vertices[i] for i in tri]
            u=[b[i]-a[i] for i in range(3)];v=[c[i]-a[i] for i in range(3)]
            cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
            assert sum(x*x for x in cross)>1e-16, (self.name,tri)
        lines=['# Authored PARKSIDE pilot geometry, meters; not a scan.',f'o {self.name}','s off']
        lines += ['v %.6f %.6f %.6f'%p for p in self.vertices]
        lines += ['vt %.6f %.6f'%p for p in self.uv]
        lines += ['f '+' '.join(f'{i+1}/{i+1}' for i in t) for t in self.triangles]
        p=OUT/(self.name+'.obj');p.write_text('\n'.join(lines)+'\n')
        return {'file':p.name,'vertices':len(self.vertices),'triangles':len(self.triangles),
                'bounds':[[min(p[i] for p in self.vertices) for i in range(3)],
                          [max(p[i] for p in self.vertices) for i in range(3)]],
                'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}

ground=Mesh('Pilot_Asphalt')
def asphalt_patch(x0,z0,x1,z1):
    pts=[(x0,-.006,z0),(x0,-.006,z1),(x1,-.006,z1),(x1,-.006,z0)]
    ground.quad(pts,[(x/30,z/30) for x,_,z in pts])
# Leave a real hole for the drain; asphalt must not fill the grate openings.
asphalt_patch(-6,-.35,3.03,12)
asphalt_patch(3.38,-.35,6,12)
asphalt_patch(3.03,-.35,3.38,-.2)
asphalt_patch(3.03,5.5,3.38,12)
curb=Mesh('Pilot_Curb')
for i in range(12): curb.curb(-6+i+.006,.988)
sidewalk=Mesh('Pilot_Sidewalk');sidewalk.box(-6,-.08,-8,6,.13,-.23)
lines=Mesh('Pilot_ParkingPaint')
for x in [-5,-2.5,0,2.5]: lines.box(x-.045,-.002,.05,x+.045,.003,5.05)
lines.box(-5,-.002,0,2.5,.003,.09)
# An actual recessed trench and individual steel grate bars, no printed drain.
trench=Mesh('Pilot_DrainWell');steel=Mesh('Pilot_DrainSteel')
trench.box(3.05,-.16,-.2,3.36,-.085,5.5)
trench.box(3.03,-.16,-.2,3.06,.004,5.5)
trench.box(3.35,-.16,-.2,3.38,.004,5.5)
for z in [i*.5 for i in range(11)]:
    steel.box(3.065,-.025,z,3.095,.002,z+.485)
    steel.box(3.315,-.025,z,3.345,.002,z+.485)
    for n in range(15):
        steel.box(3.095,-.02,z+n*.032,3.315,.002,z+n*.032+.009)

# Front facade has real holes and reveals. Back and sides have real thickness.
brick=Mesh('Pilot_BrickShell');trim=Mesh('Pilot_ConcreteTrim')
glass=Mesh('Pilot_WindowGlass');frames=Mesh('Pilot_WindowFrames')
brick.box(-5,0,-8,5,9.6,-7.78)
brick.box(-5,0,-7.78,-4.78,9.6,-5)
brick.box(4.78,0,-7.78,5,9.6,-5)
windows=[(-4.35,-2.85),(-1.95,-.45),(.45,1.95),(2.85,4.35)]
for floor in range(3):
    y=floor*3.2;lo=y+.9;hi=y+2.5
    brick.box(-5,y,-5.22,5,lo,-5)
    brick.box(-5,hi,-5.22,5,y+3.2,-5)
    last=-5
    for x0,x1 in windows:
        brick.box(last,lo,-5.22,x0,hi,-5)
        # Frame, deeper glazing and sill are separately visible surfaces.
        frames.box(x0,lo,-5.12,x0+.045,hi,-4.98)
        frames.box(x1-.045,lo,-5.12,x1,hi,-4.98)
        frames.box(x0+.045,lo,-5.12,x1-.045,lo+.045,-4.98)
        frames.box(x0+.045,hi-.045,-5.12,x1-.045,hi,-4.98)
        mid=(x0+x1)/2
        frames.box(mid-.025,lo+.045,-5.12,mid+.025,hi-.045,-4.98)
        glass.box(x0+.045,lo+.045,-5.16,x1-.045,hi-.045,-5.15)
        trim.box(x0-.08,lo-.065,-5.25,x1+.08,lo,-4.83)
        last=x1
    brick.box(last,lo,-5.22,5,hi,-5)
    trim.box(-5.08,y+3.12,-8.08,5.08,y+3.2,-4.92)
trim.box(-5.12,9.6,-8.12,5.12,9.74,-4.88)

def copy_triangles(source,dest,dy=0,predicate=lambda pts: True):
    for tri in source.triangles:
        points=[source.vertices[i] for i in tri]
        if not predicate(points):continue
        n=len(dest.vertices)
        dest.vertices.extend([(x,y+dy,z) for x,y,z in points])
        dest.uv.extend([source.uv[i] for i in tri])
        dest.triangles.append((n,n+1,n+2))
# A real extra storey, rather than stretching existing windows vertically.
brick4=Mesh('Pilot4_BrickShell');trim4=Mesh('Pilot4_ConcreteTrim')
frames4=Mesh('Pilot4_WindowFrames');glass4=Mesh('Pilot4_WindowGlass')
for src,dst in [(brick,brick4),(trim,trim4),(frames,frames4),(glass,glass4)]:
    copy_triangles(src,dst)
    copy_triangles(src,dst,3.2,lambda pts: min(p[1] for p in pts)>=6.4-1e-5 and min(p[2] for p in pts)>=-5.26 and (src is not trim or max(p[1] for p in pts)<9.5))
brick4.box(-5,9.6,-8,5,12.8,-7.78)
brick4.box(-5,9.6,-7.78,-4.78,12.8,-5)
brick4.box(4.78,9.6,-7.78,5,12.8,-5)
trim4.box(-5.08,12.72,-8.08,5.08,12.8,-4.92)
trim4.box(-5.12,12.8,-8.12,5.12,12.94,-4.88)

# Full candidate plot; separate from the reusable three-bay component above.
zone=Mesh('Zone_Asphalt')
def zone_patch(x0,z0,x1,z1):
    pts=[(x0,-.006,z0),(x0,-.006,z1),(x1,-.006,z1),(x1,-.006,z0)]
    zone.quad(pts,[(x/30,z/30) for x,_,z in pts])
zone_patch(-10,-14,8.5,14);zone_patch(8.85,-14,10,14)
zone_patch(8.5,-14,8.85,-13.5);zone_patch(8.5,13.5,8.85,14)
zonewalk=Mesh('Zone_Sidewalk')
zonewalk.box(-14,-.1,-14,-8.82,.13,14)
zonewalk.box(9.02,-.1,-14,10.3,.13,14)
zonecurb=Mesh('Zone_Curb')
for side in [-8.82,8.8]:
    for j in range(28):
        first=len(zonecurb.vertices);zonecurb.curb(-14+j+.006,.988)
        for k in range(first,len(zonecurb.vertices)):
            x,y,z=zonecurb.vertices[k];zonecurb.vertices[k]=(-z+side,y,x)
zonelines=Mesh('Zone_ParkingPaint')
for z in [-12.5+j*2.5 for j in range(11)]:
    zonelines.box(-8,-.002,z-.045,-3,.003,z+.045)
    zonelines.box(3,-.002,z-.045,8,.003,z+.045)
zonelines.box(-8.045,-.002,-12.5,-7.955,.003,12.5)
zonelines.box(7.955,-.002,-12.5,8.045,.003,12.5)
zw=Mesh('Zone_DrainWell');zs=Mesh('Zone_DrainSteel')
zw.box(8.5,-.16,-13.5,8.85,-.085,13.5)
zw.box(8.5,-.16,-13.5,8.525,.004,13.5)
zw.box(8.825,-.16,-13.5,8.85,.004,13.5)
for z in [-13.5+j*.5 for j in range(54)]:
    zs.box(8.53,-.025,z,8.56,.002,z+.485)
    zs.box(8.79,-.025,z,8.82,.002,z+.485)
    for k in range(15):zs.box(8.56,-.02,z+k*.032,8.79,.002,z+k*.032+.009)
sign=Mesh('Zone_SignPanel');sign.box(8.05,2.35,9.9,9.95,3.2,10.02)
post=Mesh('Zone_SignPost');post.box(8.96,-.1,9.94,9.04,3.25,10.02)

def vec(x=0,y=0,z=0):return dict(x=x,y=y,z=z)
objects=[]
for name,material,collision in [('Asphalt','Asphalt',True),('Sidewalk','Paving',True),
    ('Curb','Concrete',True),('ParkingPaint','ParkingPaint',False),
    ('DrainWell','DrainWell',False),('DrainSteel','DrainSteel',False),
    ('SignPanel','SignBlue',False),('SignPost','WindowFrames',False)]:
    objects.append(dict(name='Zone_'+name,asset='Geometry/Zone_'+name+'.obj',material=material,position=vec(),yaw=0,collider=collision))
for index,z in enumerate([-6.5,5,16]):
    for name,material in [('BrickShell','Brick' if index!=1 else 'Concrete'),
        ('ConcreteTrim','Concrete'),('WindowFrames','WindowFrames'),('WindowGlass','WindowGlass')]:
        prefix='Pilot4_' if index==1 else 'Pilot_'
        objects.append(dict(name='Building_%d_%s'%(index,name),asset='Geometry/'+prefix+name+'.obj',
                            material=material,position=vec(-5.3,0,z),yaw=90,collider=name=='BrickShell'))
layout={'status':'Provisional assembly; final review and Unity validation pending',
        'width':20,'length':28,'aisle':6,'eyeHeight':1.65,'verticalFov':44,
        'objects':objects,'trees':[vec(-9.45,.13,z) for z in [-7.5,-.8,6.1,11.5]],
        'vehicleSlots':[vec(-5.5,0,z) for z in [-6.25,-3.75,-1.25,1.25,3.75,6.25,8.75]],
        'cameraPosition':vec(-1.7,1.65,-10.5),'cameraYaw':-18,
        'nearVehicleFramingStatus':'Candidate about 3-4m from approximate body bounds; require actual Unity framing confirmation.',
        'forwardDistance':10,'sideDistance':3,
        'warning':'Existing vehicle comparison is not approval of close-range shape/material quality.'}
(ROOT/'candidate-layout.json').write_text(json.dumps(layout,indent=2))

stats=[m.write() for m in meshes.values()]
result={'units':'metres','status':'authored geometry only; Unity import/render unverified',
        'design_assumptions':{'bay_width':2.5,'bay_length':5,'aisle_depth':6,
                              'eye_height':1.65,'dimensions_measured_from_image':False},
        'parts':stats,'total_triangles':sum(s['triangles'] for s in stats),
        'vehicle':'No new high fidelity vehicle acquired; retain comparison slot pending review',
        'source':'Authored geometry, existing credited photographic material sources referenced separately.'}
(ROOT/'geometry-manifest.json').write_text(json.dumps(result,indent=2))
print(json.dumps({'parts':len(stats),'triangles':result['total_triangles'],'output':str(OUT)}))
