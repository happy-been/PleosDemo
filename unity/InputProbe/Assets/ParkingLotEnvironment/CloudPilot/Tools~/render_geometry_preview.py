"""Software OpenGL geometry preview. NOT a Unity render or realism approval."""
from pathlib import Path
import os, math, ctypes as c
os.environ['EGL_PLATFORM']='surfaceless';os.environ['LIBGL_ALWAYS_SOFTWARE']='1'
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[1];env=root.parent
e=c.CDLL('libEGL.so.1');g=c.CDLL('libGL.so.1');p=c.c_void_p;i=c.c_int;f=c.c_float
def sig(lib,name,result,args):
    fn=getattr(lib,name);fn.restype=result;fn.argtypes=args;return fn
display=sig(e,'eglGetDisplay',p,[p])(None);a,b=i(),i()
assert sig(e,'eglInitialize',i,[p,c.POINTER(i),c.POINTER(i)])(display,c.byref(a),c.byref(b))
e.eglBindAPI(0x30A2);cfg=p();n=i()
assert sig(e,'eglChooseConfig',i,[p,c.POINTER(i),c.POINTER(p),i,c.POINTER(i)])(display,(i*9)(0x3033,1,0x3040,8,0x3025,24,0x3024,8,0x3038),c.byref(cfg),1,c.byref(n))
ctx=sig(e,'eglCreateContext',p,[p,p,p,c.POINTER(i)])(display,cfg,None,(i*1)(0x3038))
w,h=1280,720
surf=sig(e,'eglCreatePbufferSurface',p,[p,p,c.POINTER(i)])(display,cfg,(i*5)(0x3057,w,0x3056,h,0x3038))
assert sig(e,'eglMakeCurrent',i,[p,p,p,p])(display,surf,surf,ctx)
g.glViewport(0,0,w,h);g.glEnable(0x0B71);g.glEnable(0x0B44)
sig(g,'glClearColor',None,[f]*4)(.46,.61,.72,1)
g.glClear(0x4000|0x0100)
g.glMatrixMode(0x1701);g.glLoadIdentity()
near=.05;top=math.tan(math.radians(44)/2)*near
sig(g,'glFrustum',None,[c.c_double]*6)(-top*w/h,top*w/h,-top,top,near,150)
def norm(v):
    n=math.sqrt(sum(x*x for x in v));return [x/n for x in v]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def look(eye,target):
    forward=norm([target[j]-eye[j] for j in range(3)]);side=norm(cross(forward,[0,1,0]));up=cross(side,forward)
    m=(c.c_double*16)(side[0],up[0],-forward[0],0,side[1],up[1],-forward[1],0,side[2],up[2],-forward[2],0,0,0,0,1)
    g.glMatrixMode(0x1700);g.glLoadIdentity();sig(g,'glMultMatrixd',None,[c.POINTER(c.c_double)])(m)
    sig(g,'glTranslated',None,[c.c_double]*3)(*[-x for x in eye])
g.glEnable(0x0B50);g.glEnable(0x4000);g.glEnable(0x0BA1);g.glEnable(0x0B57)
sig(g,'glLightfv',None,[c.c_uint,c.c_uint,c.POINTER(f)])(0x4000,0x1203,(f*4)(-.45,.8,.3,0))
g.glLightfv(0x4000,0x1201,(f*4)(.85,.82,.76,1))
sig(g,'glLightModelfv',None,[c.c_uint,c.POINTER(f)])(0x0B53,(f*4)(.3,.32,.36,1))
sig(g,'glColor3f',None,[f]*3);sig(g,'glNormal3f',None,[f]*3);sig(g,'glVertex3f',None,[f]*3);sig(g,'glTexCoord2f',None,[f]*2)
g.glGenTextures.argtypes=[i,c.POINTER(c.c_uint)]
sig(g,'glTexImage2D',None,[c.c_uint,i,i,i,i,i,c.c_uint,c.c_uint,p])
textures={}
def texture(path):
    if path not in textures:
        im=Image.open(env/path).convert('RGB').transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        tid=c.c_uint();g.glGenTextures(1,c.byref(tid));g.glBindTexture(0x0DE1,tid.value)
        g.glTexParameteri(0x0DE1,0x2801,0x2601);g.glTexParameteri(0x0DE1,0x2800,0x2601)
        data=im.tobytes();g.glTexImage2D(0x0DE1,0,0x1907,im.width,im.height,0,0x1907,0x1401,c.cast(c.c_char_p(data),p));textures[path]=tid.value
    g.glBindTexture(0x0DE1,textures[path]);g.glEnable(0x0DE1)
materials={
 'Asphalt':((.9,.9,.9),'KoreanAssets/aerial_asphalt_01_diff_2k.jpg',1),
 'Curb':((.8,.8,.8),'KoreanAssets/concrete_wall_009_diff_2k.jpg',1),
 'Sidewalk':((1,1,1),'KoreanAssets/concrete_pavement_diff_2k.jpg',1/1.8),
 'BrickShell':((.9,.9,.9),'KoreanAssets/red_brick_diff_2k.jpg',1/1.4),
 'ConcreteTrim':((.8,.8,.8),'KoreanAssets/concrete_wall_009_diff_2k.jpg',1),
 'ParkingPaint':((.8,.79,.72),None,1),'WindowGlass':((.12,.2,.24),None,1),
 'WindowFrames':((.5,.55,.55),None,1),'DrainSteel':((.27,.29,.3),None,1),'DrainWell':((.035,.03,.025),None,1)}
def draw_mesh(path):
    color,tex,scale=materials[path.stem.replace('Pilot_','')];g.glColor3f(*color)
    g.glDisable(0x0DE1)
    if tex:texture(tex)
    verts=[];uv=[];faces=[]
    for l in path.read_text().splitlines():
        s=l.split()
        if not s:continue
        if s[0]=='v':verts.append(tuple(map(float,s[1:])))
        if s[0]=='vt':uv.append(tuple(map(float,s[1:])))
        if s[0]=='f':faces.append([int(x.split('/')[0])-1 for x in s[1:]])
    g.glBegin(4)
    for face in faces:
        a,b,c0=[verts[j] for j in face]
        normal=norm(cross([b[j]-a[j] for j in range(3)],[c0[j]-a[j] for j in range(3)]));g.glNormal3f(*normal)
        for j in face:g.glTexCoord2f(uv[j][0]*scale,uv[j][1]*scale);g.glVertex3f(*verts[j])
    g.glEnd()

out=Path(os.environ.get('PARKSIDE_PREVIEW_OUTPUT',str(root/'Inspection~')))
out.mkdir(exist_ok=True,parents=True)
for label,eye,target in [('eye',(4.8,1.65,9),(-1.5,2,-3)),('side',(1.8,1.65,9),(-4.5,2,-3)),('detail',(4.7,1.65,3.5),(3.2,.1,1.8))]:
    g.glClear(0x4000|0x0100);look(eye,target)
    for path in sorted((root/'Geometry').glob('Pilot_*.obj')):draw_mesh(path)
    g.glFinish();pixels=(c.c_ubyte*(w*h*3))();g.glReadPixels(0,0,w,h,0x1907,0x1401,pixels)
    assert g.glGetError()==0
    im=Image.frombytes('RGB',(w,h),bytes(pixels)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    d=ImageDraw.Draw(im);d.rectangle((0,0,w,32),fill=(20,24,30));d.text((12,10),'SOFTWARE OPENGL GEOMETRY PREVIEW | NOT UNITY | NO VEHICLE / SHADOW / REALISM VALIDATION',fill='white')
    im.save(out/('pilot-geometry-'+label+'.png'))
print('Rendered 3 software geometry inspection views. Not Unity validation.')
