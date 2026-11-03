import bpy, bmesh, math, os, json, struct, zlib
import numpy as np
from mathutils import Vector, Quaternion
from math import sin, cos, pi

ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=ROOT if os.path.isdir(os.path.join(ROOT,'Assets','Trimaculatus')) else os.path.join(ROOT,'outputs','Trimaculatus_URP')
ASSET=os.path.join(OUT,'Assets','Trimaculatus'); TEX=os.path.join(ASSET,'Textures')
S=.115; N=2048
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
col=bpy.data.collections.new('Hippocampus_trimaculatus_Male');scene.collection.children.link(col)
objects=[]; anchors={};meta={}

def mesh(name,vs,fs,kind='armor',fixed=None):
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata([tuple(S*x for x in p) for p in vs],[],fs);me.update()
    ob=bpy.data.objects.new(name,me);col.objects.link(ob);objects.append(ob)
    ob['anatomy']=kind;ob['species']='Hippocampus trimaculatus'
    if fixed:ob['fixed_bone']=fixed
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    for f in me.polygons:f.use_smooth=True
    return ob

def ellipsoid(name,p,r,kind='armor',fixed='Head',nu=32,nv=16):
    vs=[(p[0],p[1],p[2]+r[2])]
    for i in range(1,nv):
        a=pi*i/nv
        for j in range(nu):
            b=2*pi*j/nu;vs.append((p[0]+r[0]*sin(a)*cos(b),p[1]+r[1]*sin(a)*sin(b),p[2]+r[2]*cos(a)))
    bottom=len(vs);vs.append((p[0],p[1],p[2]-r[2]));fs=[]
    for j in range(nu):fs.append((0,1+j,1+(j+1)%nu))
    for i in range(nv-2):
        for j in range(nu):
            a=1+i*nu+j;b=1+i*nu+(j+1)%nu;fs.append((a,a+nu,b+nu,b))
    a=1+(nv-2)*nu
    for j in range(nu):fs.append((a+j,bottom,a+(j+1)%nu))
    return mesh(name,vs,fs,kind,fixed)

def tube(name,paths,rr,kind='ridge',fixed=None,sides=8):
    vs=[];fs=[]
    for k,path in enumerate(paths):
        r=rr[k] if isinstance(rr[0],(list,tuple,np.ndarray)) else rr;off=len(vs)
        if kind=='hook':
            old=np.array(path,float);oldr=np.array(r);smooth=[];smoothr=[]
            for u in np.linspace(0,len(old)-1,25):
                q=min(len(old)-2,int(u));t=u-q
                a=old[max(0,q-1)];b=old[q];c=old[q+1];d=old[min(len(old)-1,q+2)]
                smooth.append(.5*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
                smoothr.append(np.interp(u,np.arange(len(old)),oldr))
            path=smooth;r=smoothr
        for i,p in enumerate(path):
            t=(Vector(path[min(i+1,len(path)-1)])-Vector(path[max(0,i-1)])).normalized()
            ref=Vector((0,1,0)) if abs(t.y)<.9 else Vector((0,0,1))
            a=t.cross(ref).normalized();b=t.cross(a).normalized()
            for j in range(sides):vs.append(tuple(Vector(p)+float(r[i])*(a*cos(2*pi*j/sides)+b*sin(2*pi*j/sides))))
        for i in range(len(path)-1):
            for j in range(sides):
                a=off+i*sides+j;b=off+i*sides+(j+1)%sides;fs.append((a,b,b+sides,a+sides))
        fs.append(tuple(reversed(range(off,off+sides))))
        end=off+(len(path)-1)*sides;fs.append(tuple(range(end,end+sides)))
    return mesh(name,vs,fs,kind,fixed)

def lerp(a,b,t):return tuple((1-t)*x+t*y for x,y in zip(a,b))

# Cubic interpolating center lines, reparameterized by arc length.
def curve_table(points,samples=1400):
    pts=np.array(points,float);vals=[]
    for u in np.linspace(0,len(pts)-1,samples):
        k=min(len(pts)-2,int(u));t=u-k
        a=pts[max(0,k-1)];b=pts[k];c=pts[k+1];d=pts[min(len(pts)-1,k+2)]
        vals.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    vals=np.array(vals);ds=np.linalg.norm(np.diff(vals[:,:2],axis=0),axis=1)
    ss=np.r_[0,np.cumsum(ds)];ss/=ss[-1]
    return ss,vals

headtab=curve_table([
 [-.290,.900,.014,.013],[-.244,.904,.0148,.014],[-.1582,.911,.017,.018],
 [-.137,.922,.027,.024],[-.104,.935,.040,.029],[-.060,.944,.049,.032],
 [-.018,.935,.046,.031],[.020,.908,.039,.029],[.034,.866,.034,.027],
 [.035,.835,.033,.027]])
tailtab=curve_table([[.020,.290],[.024,.170],[.022,.045],[.000,-.085],[-.050,-.205],
 [-.148,-.287],[-.244,-.277],[-.293,-.205],[-.264,-.133],[-.199,-.139]])

def tabget(tab,t):return np.array([np.interp(t,tab[0],tab[1][:,i]) for i in range(tab[1].shape[1])])
def trunk(t):
    zs=np.array([0,.17,.36,.58,.78,1]);xx=[.035,.016,-.004,-.012,.000,.020]
    dd=[.033,.048,.077,.096,.082,.046];ww=[.027,.034,.045,.051,.044,.033]
    return np.array([np.interp(t,zs,xx),.835-.545*t,np.interp(t,zs,dd),np.interp(t,zs,ww)])

def axial(region,t):
    if region=='head':
        p=tabget(headtab,t);a=tabget(headtab,max(0,t-.001));b=tabget(headtab,min(1,t+.001));expo=2.3
    elif region=='trunk':
        p=trunk(t);a=trunk(max(0,t-.001));b=trunk(min(1,t+.001));expo=2.8
        # Eleven complete armor bands, with small raised transverse junctions.
        ridge=(.5+.5*cos(2*pi*(11*t-.5)))**10
        p[2:]*=1+.058*ridge
    else:
        q=tabget(tailtab,t);a=tabget(tailtab,max(0,t-.001));b=tabget(tailtab,min(1,t+.001))
        r=.044*(1-t)**1.13+.0018;p=np.r_[q,r,r*.74]
        ridge=(.5+.5*cos(2*pi*(41*t-.5)))**9;p[2:]*=1+.075*ridge;expo=5.5
    tt=Vector((b[0]-a[0],0,b[1]-a[1])).normalized();nn=Vector((tt.z,0,-tt.x))
    return p,tt,nn,expo

def section(region,t,theta,offset=0):
    p,tt,nn,n=axial(region,t);c=cos(theta);s=sin(theta)
    # Smooth transition into the square tail without a cross-section discontinuity.
    if region=='tail':n=2.8+2.7*min(1,t/.07)
    q=Vector((p[0],0,p[1]))+nn*(p[2]+offset)*math.copysign(abs(c)**(2/n),c)
    q.y=(p[3]+offset)*math.copysign(abs(s)**(2/n),s)
    # Low longitudinal keels at plate junctions; no imbricated scales.
    if region=='trunk':
        keel=(.5+.5*cos(8*theta))**16*.0018
        q+=nn*keel*c;q.y+=keel*s
    return tuple(q)

# A single watertight quad-dominant skin from tubular snout through the four-sided tail.
stations=[('head',t) for t in np.linspace(0,1,65)]
stations += [('trunk',t) for t in np.linspace(0,1,11*8+1)[1:]]
stations += [('tail',t) for t in np.linspace(0,1,41*6+1)[1:]]
ns=32;vs=[];fs=[];uvfaces=[];vparam=[]
centers=[(axial(r,t)[0][0],0,axial(r,t)[0][1]) for r,t in stations]
arcs=np.r_[0,np.cumsum(np.linalg.norm(np.diff(np.array(centers),axis=0),axis=1))];arcs/=arcs[-1]
for r,t in stations:
    for j in range(ns):vs.append(section(r,t,2*pi*j/ns));vparam.append(({'head':0,'trunk':1,'tail':2}[r]+t))
for i in range(len(stations)-1):
    for j in range(ns):
        a=i*ns+j;b=i*ns+(j+1)%ns;fs.append((a,b,b+ns,a+ns))
        x0=.025+.95*j/ns;x1=.025+.95*(j+1)/ns;y0=.420+.555*arcs[i];y1=.420+.555*arcs[i+1]
        uvfaces.append([(x0,y0),(x1,y0),(x1,y1),(x0,y1)])

# Recessed terminal mouth: continuous outer lip, cavity wall and inner cap.
outer=list(range(ns));prev=outer
for k,(x,z,r,w) in enumerate([(-.291,.900,.0098,.0090),(-.266,.902,.0085,.0078)]):
    curr=[]
    for j in range(ns):
        curr.append(len(vs));th=2*pi*j/ns;vs.append((x,w*sin(th),z-r*cos(th)));vparam.append(-.15)
    for j in range(ns):
        a,b=prev[j],prev[(j+1)%ns];c,d=curr[(j+1)%ns],curr[j];fs.append((a,d,c,b))
        x0=.025+.28*j/ns;x1=.025+.28*(j+1)/ns;y0=.355+.015*k;y1=y0+.012
        uvfaces.append([(x0,y0),(x0,y1),(x1,y1),(x1,y0)])
    prev=curr
c=len(vs);vs.append((-.265,0,.902));vparam.append(-.15)
for j in range(ns):
    fs.append((prev[j],c,prev[(j+1)%ns]));th=2*pi*j/ns;th2=2*pi*(j+1)/ns
    uvfaces.append([(.37+.014*cos(th),.373+.014*sin(th)),(.37,.373),(.37+.014*cos(th2),.373+.014*sin(th2))])
end=(len(stations)-1)*ns;c=len(vs);last=axial('tail',1)[0];vs.append((last[0],0,last[1]));vparam.append(3)
for j in range(ns):
    fs.append((end+j,end+(j+1)%ns,c));th=2*pi*j/ns;th2=2*pi*(j+1)/ns
    uvfaces.append([(.42+.014*cos(th),.373+.014*sin(th)),(.42+.014*cos(th2),.373+.014*sin(th2)),(.42,.373)])
body=mesh('AxialArmor_11Trunk_41Tail',vs,fs,'body');uv=body.data.uv_layers.new(name='UV0_UniqueAtlas')
for face,coords in zip(body.data.polygons,uvfaces):
    for li,c in zip(face.loop_indices,coords):uv.data[li].uv=c
meta[body.name]={'station':vparam}
body['trunk_ring_count']=11;body['tail_ring_count']=41;body['surface']='Bony rings and raised plate junctions, no scales'

# Small sharp recurved cheek and supraorbital spines, each bilaterally paired.
for sg,suf in [(-1,'L'),(1,'R')]:
    tube('Cheek_HookSpine.'+suf,[[(-.079,sg*.030,.914),(-.077,sg*.037,.899),(-.065,sg*.040,.896),(-.060,sg*.039,.906)]],
        [.006,.0045,.0026,.00035],'hook','Head',10)
    tube('Supraorbital_HookSpine.'+suf,[[(-.108,sg*.023,.963),(-.108,sg*.027,.979),(-.119,sg*.030,.981),(-.124,sg*.029,.973)]],
        [.0052,.0035,.0019,.0003],'hook','Head',10)
    ec=(-.115,sg*.0295,.943)
    ellipsoid('Eye_Globe.'+suf,ec,(.0175,.0125,.0175),'eye', 'Eye.'+suf)
    ellipsoid('Eye_Iris.'+suf,(-.115,sg*.0400,.943),(.0135,.0038,.0135),'iris','Eye.'+suf)
    ellipsoid('Eye_Pupil.'+suf,(-.115,sg*.0433,.943),(.0067,.0015,.0067),'pupil','Eye.'+suf,24,12)
    # Convex opercular ridge ending at a discrete small gill pore.
    tube('Operculum_RaisedRidge.'+suf,[[(-.041,sg*.028,.949),(-.021,sg*.031,.936),(-.009,sg*.032,.918),(.001,sg*.030,.902)]],
        [.0014,.0023,.0022,.0012],'ridge','Head')
    ellipsoid('GillOpening.'+suf,(.006,sg*.0298,.897),(.0044,.0014,.0063),'cavity','Head',20,10)

# Five very low tips follow the posterior cranial/neck arch.
coronets=[(-.032,-.008,.991),(-.032,.008,.991),(-.016,-.010,.985),(-.016,.010,.985),(-.006,0,.982)]
for i,p in enumerate(coronets):
    tube('Coronet_TinyPoint_%02d'%(i+1),[[[p[0],p[1],p[2]-.005],[p[0]-.0005,p[1],p[2]+.003]]],[[.0030,.0003]],'coronet','Head',10)

# Longitudinal raised plate junctions intersect the eleven transverse armor bands.
paths=[];radii=[]
for theta in [pi/3,pi/2,2*pi/3,4*pi/3,3*pi/2,5*pi/3]:
    path=[section('trunk',t,theta,.00035) for t in np.linspace(.012,.992,133)]
    paths.append(path);radii.append([.0010]*len(path))
tube('Trunk_PlateJunctionRidges',paths,radii,'ridge',None,6)

# Discrete mandible with a very short hinge excursion, preserving the tubular snout.
tube('Mandible_LowerJaw',[[(-.150,0,.895),(-.210,0,.887),(-.270,0,.884),(-.290,0,.886)]],
     [.006,.005,.004,.0035],'armor','LowerJaw',12)

# One small closed membrane per fin, with real, separately counted soft rays.
def fin(name,bases,tips,count,kind,fixed=None,bone_rows=None):
    steps=5;vs=[];fs=[]
    # Closed double surface, both sides receive unique UV islands.
    for side in [-1,1]:
        for i,(a,b) in enumerate(zip(bases,tips)):
            for j in range(steps+1):
                t=j/steps;p=Vector(lerp(a,b,t));p.y+=side*.00042*(1-.75*t);vs.append(tuple(p))
    off=len(bases)*(steps+1)
    for side in range(2):
        for i in range(len(bases)-1):
            for j in range(steps):
                a=side*off+i*(steps+1)+j;f=(a,a+steps+1,a+steps+2,a+1);fs.append(f if side else tuple(reversed(f)))
    border=[i*(steps+1) for i in range(len(bases))]+[(len(bases)-1)*(steps+1)+j for j in range(1,steps+1)]+[i*(steps+1)+steps for i in range(len(bases)-2,-1,-1)]+list(range(steps-1,0,-1))
    for a,b in zip(border,border[1:]+border[:1]):fs.append((a,b,b+off,a+off))
    ob=mesh(name+'_Membrane',vs,fs,kind,fixed);ob['soft_ray_count']=count;ob['spine_count']=0
    paths=[]
    for a,b in zip(bases,tips):
        p=lerp(a,b,.55);p=(p[0]+.002,p[1],p[2]);paths.append([a,p,b])
    rays=tube(name+'_SoftRays_%02d'%count,paths,[[.0010,.00068,.00024]]*len(paths),'ray',fixed,7)
    rays['soft_ray_count']=count;rays['spine_count']=0
    if bone_rows:
        meta[ob.name]={'fin_rows':[(i//(steps+1))%len(bases) for i in range(len(vs))],'bones':bone_rows}
        meta[rays.name]={'fin_rows':[min(len(bases)-1,i//(3*7)) for i in range(len(rays.data.vertices))],'bones':bone_rows}
    return ob,rays

db=[];dt=[];dorsal_bones=[]
for i in range(20):
    q=i/19;z=.386-.118*q
    if z>=.290:
        t=(.835-z)/.545;p=section('trunk',t,pi)
    else:
        t=(.290-z)/.86;p=section('tail',t,pi)
    # theta pi is the dorsal (+X) keel for the downward-pointing trunk.
    reach=.022+.049*sin(pi*q)**.7;db.append(p);dt.append((p[0]+reach,0,p[2]-.007))
    dorsal_bones.append('DorsalWave_%02d'%(min(4,int(q*5))+1))
fin('Dorsal_Single_20Soft',db,dt,20,'fin',bone_rows=dorsal_bones)
for sg,suf in [(-1,'L'),(1,'R')]:
    pb=[];pt=[]
    for i in range(18):
        q=i/17;a=.72-1.95*q
        pb.append((.008+.008*q,sg*.031,.879-.015*q))
        reach=.057*(.78+.22*sin(pi*q));pt.append((.018+reach*cos(a),sg*(.044+.010*sin(pi*q)),.869+reach*sin(a)))
    fin('Pectoral_Fan.'+suf,pb,pt,18,'fin','Pectoral.'+suf)
ellipsoid('Anus',(-.028,0,.301),(.0035,.004,.0035),'cavity','Root',20,10)
ab=[];at=[]
for i in range(4):
    q=i/3;ab.append((-.026+.003*q,0,.295-.010*q));at.append((-.040-.004*sin(pi*q),0,.291-.014*q))
fin('Anal_Tiny_4Soft',ab,at,4,'fin','AnalFin')

# Male pouch on the ventral proximal tail, tapering into the upper tail below the anus.
pv=[];pf=[];nu=40;nv=30
for i in range(nv+1):
    t=i/nv;z=.302-.195*t
    width=.030*sin(pi*t)**.68+.0012;depth=.044*sin(pi*t)**.7+.0012
    cx=.009-.024*sin(pi*t)**.7
    for j in range(nu):
        a=2*pi*j/nu;pv.append((cx-depth*cos(a),width*sin(a),z))
for i in range(nv):
    for j in range(nu):
        a=i*nu+j;b=i*nu+(j+1)%nu;pf.append((a,b,b+nu,a+nu))
pf.append(tuple(reversed(range(nu))));pf.append(tuple(nv*nu+j for j in range(nu)))
pouch=mesh('Male_BroodPouch_VentralTail',pv,pf,'pouch')
tube('BroodPouch_OpeningRim',[[(-.019,-.015,.298),(-.028,-.010,.300),(-.031,0,.301),(-.028,.010,.300),(-.019,.015,.298)]],
     [.0012]*5,'ridge','Tail_01')

# Unwrap every additional part together, then reserve the lower atlas for those islands.
secondary=[o for o in objects if o!=body]
bpy.ops.object.select_all(action='DESELECT')
for o in secondary:o.select_set(True)
bpy.context.view_layer.objects.active=secondary[0];bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.12,island_margin=.007,correct_aspect=True,scale_to_bounds=False)
bpy.ops.object.mode_set(mode='OBJECT')
for ob in secondary:
    for v in ob.data.uv_layers.active.data:v.uv=(.020+.960*v.uv.x,.020+.315*v.uv.y)
# Long slender keels use analytic cylinder strips; projection can fold tiny UV quads.
keels=bpy.data.objects['Trunk_PlateJunctionRidges'];kuv=keels.data.uv_layers.active.data
for face in keels.data.polygons:
    ids=list(face.vertices);part=ids[0]//798
    if len(ids)==6:
        startcap=(ids[0]%798)//6==0;cx=.982 if startcap else .991;cy=.354+part*.0085
        for li,vi in zip(face.loop_indices,ids):
            th=2*pi*(vi%6)/6;kuv[li].uv=(cx+.0025*cos(th),cy+.0025*sin(th))
    else:
        js=[vi%6 for vi in ids];seam=0 in js and 5 in js
        for li,vi in zip(face.loop_indices,ids):
            row=(vi%798)//6;j=vi%6
            if seam and j==0:j=6
            kuv[li].uv=(.450+.525*row/132,.350+part*.0085+.0065*j/6)
print('GEOMETRY_READY',len(objects),len(body.data.vertices),flush=True)

# Deterministic object-space pigmentation: sandy ochre, pores, and three dorsolateral spots.
rgb=np.zeros((N,N,3),np.float32)+[.47,.36,.21];rough=np.full((N,N),.66,np.float32)
height=np.zeros((N,N),np.float32);occupied=np.zeros((N,N),bool)

def paint(ob,xyz,aux=None):
    x,y,z=xyz[...,0],xyz[...,1],xyz[...,2];shape=x.shape;k=ob['anatomy']
    grain=(np.sin(x*411+y*237+z*199)*np.sin(x*173-y*331+z*513)+np.sin(x*83+y*97-z*113))*.5
    fine=np.sin(x*2310+y*3371+z*2989)*np.cos(x*1787-y*2213+z*3127)
    c=np.zeros(shape+(3,),np.float32)+[.61,.455,.257];r=np.zeros(shape,np.float32)+.65
    h=fine*.08+grain*.03
    if k=='body':
        a=aux;tr=(a>=1)&(a<2);ta=a>=2
        phase=np.where(tr,(a-1)*11-.5,(a-2)*41-.5)
        crest=(.5+.5*np.cos(2*pi*phase))**8;crest*=tr|ta
        joints=(.5+.5*np.cos(2*pi*(phase+.16)))**14*(tr|ta)
        c+=grain[...,None]*np.array([.052,.044,.027])+fine[...,None]*.010
        c+=crest[...,None]*np.array([.095,.080,.049])-joints[...,None]*np.array([.063,.046,.025])
        # Pale ventrum and darker dorsal armor; all pigment is bilateral.
        c+=np.clip((-x+.010)/.14,0,1)[...,None]*np.array([.08,.085,.068])
        for ring in [1,4,7]:
            t=(ring-.5)/11;pp=trunk(t);cx=pp[0]+pp[2]*.57;zz=pp[1]
            d=((x-cx)/.025)**2+((z-zz)/.020)**2
            spot=np.clip((1.12-d)*4,0,1)*tr
            c=c*(1-.79*spot[...,None])+np.array([.19,.145,.083])*.79*spot[...,None]
        # Tiny head marbling, never an overlapping scale pattern.
        mar=np.maximum(0,np.sin(x*143+z*163+grain*1.1))**5*(a<1)*(a>=0)
        c-=mar[...,None]*np.array([.10,.079,.043])
        cavity=a<0;c[cavity]=[.078,.049,.027];r[cavity]=.7
    elif k in ['armor','ridge','hook','coronet','pouch']:
        if k=='ridge':c[:]=[.70,.55,.33]
        elif k in ['hook','coronet']:c[:]=[.73,.59,.37]
        elif k=='pouch':c[:]=[.67,.525,.329]
        c+=grain[...,None]*.028+fine[...,None]*.008
    elif k=='fin':
        c[:]=[.74,.658,.438];c+=grain[...,None]*.014;r[:]=.48;h=fine*.035
    elif k=='ray':c[:]=[.59,.48,.285];r[:]=.51;h=fine*.03
    elif k=='cavity':c[:]=[.066,.045,.027];r[:]=.66;h*=.1
    elif k=='eye':c[:]=[.30,.263,.145];r[:]=.24;h*=.04
    elif k=='iris':
        a=np.arctan2(z-.943,x+.115);rad=np.sqrt((x+.115)**2+(z-.943)**2)
        c[:]=[.51,.431,.214];c+=(.5+.5*np.cos(a*61+rad*2700))[...,None]*np.array([.12,.10,.05]);r[:]=.24;h*=.05
    elif k=='pupil':c[:]=[.008,.010,.008];r[:]=.13;h*=.01
    return np.clip(c,0,1),r,h

for ob in objects:
    me=ob.data;me.calc_loop_triangles();uvs=me.uv_layers.active.data
    positions=np.array([v.co[:] for v in me.vertices])/S
    aux=np.array(meta[ob.name]['station']) if ob==body else None
    for tr in me.loop_triangles:
        uv=np.array([uvs[l].uv[:] for l in tr.loops])*N-.5
        lo=np.maximum(0,np.floor(uv.min(0)).astype(int));hi=np.minimum(N-1,np.ceil(uv.max(0)).astype(int))
        if np.any(hi<lo):continue
        a,b,c=uv;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-10:continue
        yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        w0=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        w1=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w2=1-w0-w1
        inside=(w0>=-1e-6)&(w1>=-1e-6)&(w2>=-1e-6)
        if not inside.any():continue
        pos=positions[tr.vertices];xyz=w0[...,None]*pos[0]+w1[...,None]*pos[1]+w2[...,None]*pos[2]
        av=w0*aux[tr.vertices[0]]+w1*aux[tr.vertices[1]]+w2*aux[tr.vertices[2]] if aux is not None else None
        cc,rr,hh=paint(ob,xyz,av)
        for dest,val in [(rgb,cc),(rough,rr),(height,hh)]:
            view=dest[lo[1]:hi[1]+1,lo[0]:hi[0]+1];view[inside]=val[inside]
        occupied[lo[1]:hi[1]+1,lo[0]:hi[0]+1][inside]=True
print('ATLAS_RASTERIZED',flush=True)
for step in range(10):
    prev=occupied.copy()
    for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
        m=np.roll(prev,(dy,dx),(0,1))&~occupied
        for ar in [rgb,rough,height]:ar[m]=np.roll(ar,(dy,dx),(0,1))[m]
        occupied[m]=True
dy,dx=np.gradient(height);nm=np.stack([-dx*.85,-dy*.85,np.ones_like(dx)],-1);nm/=np.linalg.norm(nm,axis=-1,keepdims=True)
normal=nm*.5+.5

def png(name,ar,srgb=False):
    if ar.ndim==2:ar=np.repeat(ar[...,None],3,axis=-1)
    a=np.round(np.clip(ar,0,1)*255).astype(np.uint8)[::-1];channels=a.shape[2]
    def chunk(t,data):return struct.pack('>I',len(data))+t+data+struct.pack('>I',zlib.crc32(t+data)&0xffffffff)
    data=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',N,N,8,6 if channels==4 else 2,0,0,0))
    if srgb:data+=chunk(b'sRGB',b'\0')
    raw=b''.join(b'\0'+row.tobytes() for row in a);data+=chunk(b'IDAT',zlib.compress(raw,7))+chunk(b'IEND',b'')
    path=os.path.join(TEX,name+'.png');open(path,'wb').write(data)
    im=bpy.data.images.load(path);im.colorspace_settings.name='sRGB' if srgb else 'Non-Color';return im

baseim=png('HT_BaseColor_2K',rgb,True);normalim=png('HT_Normal_OpenGL_2K',normal)
roughim=png('HT_Roughness_2K',rough);png('HT_Metallic_2K',np.zeros((N,N),np.float32))
ms=np.zeros((N,N,4),np.float32);ms[:,:,3]=1-rough;png('HT_URP_MetallicSmoothness_2K',ms)
mat=bpy.data.materials.new('HT_Armor_URP');mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF')
bs.inputs['Metallic'].default_value=0;bs.inputs['Specular IOR Level'].default_value=.32
for im,socket in [(baseim,'Base Color'),(roughim,'Roughness')]:
    n=nodes.new('ShaderNodeTexImage');n.image=im;links.new(n.outputs['Color'],bs.inputs[socket])
n=nodes.new('ShaderNodeTexImage');n.image=normalim;nmnode=nodes.new('ShaderNodeNormalMap');nmnode.inputs['Strength'].default_value=.6
links.new(n.outputs['Color'],nmnode.inputs['Color']);links.new(nmnode.outputs['Normal'],bs.inputs['Normal'])
for ob in objects:ob.data.materials.append(mat)

# Connected Generic skeleton: 11 trunk, 41 tail, jaw, two independent eyes, five dorsal controls.
ad=bpy.data.armatures.new('HT_Skeleton');rig=bpy.data.objects.new('HT_Rig',ad);col.objects.link(rig);rig.show_in_front=True
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
spec={}
def bone(name,a,b,parent=None,connect=False):
    e=ad.edit_bones.new(name);e.head=Vector(a)*S;e.tail=Vector(b)*S
    if parent:e.parent=ad.edit_bones[parent];e.use_connect=connect
    spec[name]={'head':list(a),'tail':list(b),'parent':parent}
bone('Root',(.020,0,.290),(.020,0,.35))
for i in range(11):
    a=trunk(1-i/11);b=trunk(1-(i+1)/11)
    bone('Spine_%02d'%(i+1),(a[0],0,a[1]),(b[0],0,b[1]),'Root' if i==0 else 'Spine_%02d'%i,i>0)
bone('Head',(.035,0,.835),(-.10,0,.938),'Spine_11',True)
bone('LowerJaw',(-.15,0,.895),(-.29,0,.886),'Head')
for sg,suf in [(-1,'L'),(1,'R')]:
    bone('Eye.'+suf,(-.115,sg*.0295,.943),(-.115,sg*.051,.943),'Head')
    bone('Pectoral.'+suf,(.012,sg*.031,.872),(.065,sg*.051,.850),'Head')
for i in range(41):
    a=tabget(tailtab,i/41);b=tabget(tailtab,(i+1)/41)
    bone('Tail_%02d'%(i+1),(a[0],0,a[1]),(b[0],0,b[1]),'Root' if i==0 else 'Tail_%02d'%i,i>0)
for i in range(5):
    p=db[min(19,i*4+1)];bone('DorsalWave_%02d'%(i+1),p,(p[0]+.060,0,p[2]),'Spine_01')
bone('AnalFin',(-.026,0,.291),(-.044,0,.282),'Tail_01')
bpy.ops.object.mode_set(mode='OBJECT')

def chain_weights(region,t):
    if region=='head':return [('Head',1)]
    if region=='trunk':
        v=(1-t)*11-.5;names=['Spine_%02d'%(i+1) for i in range(11)]
    else:v=t*41-.5;names=['Tail_%02d'%(i+1) for i in range(41)]
    v=max(0,min(len(names)-1,v));i=int(v);w=v-i
    return [(names[i],1-w)]+([(names[i+1],w)] if w>1e-6 and i+1<len(names) else [])

for ob in objects:
    ob.parent=rig;mod=ob.modifiers.new('HT_GenericSkin','ARMATURE');mod.object=rig
    for name in spec:ob.vertex_groups.new(name=name)
    fixed=ob.get('fixed_bone');m=meta.get(ob.name,{})
    for v in ob.data.vertices:
        if fixed:weights=[(fixed,1)]
        elif ob==body:
            if v.index<len(stations)*ns:r,t=stations[v.index//ns];weights=chain_weights(r,t)
            else:weights=[('Head',1)] if v.index!=len(ob.data.vertices)-1 else [('Tail_41',1)]
        elif 'fin_rows' in m:weights=[(m['bones'][m['fin_rows'][v.index]],1)]
        elif ob==pouch:
            zz=v.co.z/S;t=max(0,min(.23,(.29-zz)/.9));weights=chain_weights('tail',t)
        elif ob.name=='Trunk_PlateJunctionRidges':weights=chain_weights('trunk',max(0,min(1,(.835-v.co.z/S)/.545)))
        else:weights=[('Root',1)]
        for name,w in weights:
            if w>1e-7:ob.vertex_groups[name].add([v.index],w,'REPLACE')

# Nondeforming named vertex groups and bone-parented anchors expose each armor ring.
for r,count in [('trunk',11),('tail',41)]:
    for k in range(count):
        name=('TrunkRing' if r=='trunk' else 'TailRing')+'_%02d'%(k+1);ids=[]
        for i,(reg,t) in enumerate(stations):
            if reg==r and min(count-1,int(t*count))==k:ids.extend(range(i*ns,(i+1)*ns))
        # Store semantic membership as an integer attribute instead of extra skin weights.
        if r+'_ring_id' not in body.data.attributes:body.data.attributes.new(r+'_ring_id','INT','POINT')
        att=body.data.attributes[r+'_ring_id']
        for vi in ids:att.data[vi].value=k+1
        p=section(r,(k+.5)/count,1.0);target=chain_weights(r,(k+.5)/count)[0][0]
        anchors[name]=(p,target,'躯干环' if r=='trunk' else '尾环')
anchors.update({
 'HeadTrunk_RightAngle':((.038,-.028,.854),'Head','头部与躯干直角连接'),
 'TubularSnout':((-.232,-.016,.904),'Head','管状长吻'),
 'LowCoronet_5TinyPoints':((-.023,-.010,.992),'Head','五突低骨冠'),
 'CheekHook_L':((-.067,-.040,.899),'Head','颊部钩状棘'),
 'SupraorbitalHook_L':((-.114,-.030,.981),'Head','眼上钩状棘'),
 'DorsalFin_20Soft':((.122,-.002,.331),'DorsalWave_03','单一背鳍：20软条，无硬棘'),
 'AnalFin_4Soft':((-.042,-.002,.281),'AnalFin','肛门后方极小臀鳍：4软条'),
 'PectoralFin_L_18Soft':((.063,-.054,.850),'Pectoral.L','左胸鳍：18软条'),
 'PectoralFin_R_18Soft':((.063,.054,.850),'Pectoral.R','右胸鳍：18软条'),
 'MaleBroodPouch':((-.057,-.022,.224),'Tail_05','雄性育儿囊'),
 'QuadrangularPrehensileTail':(section('tail',.45,.8),'Tail_19','四棱形可卷曲尾'),
 'TailTip_NoCaudalFin':(tuple(np.r_[tabget(tailtab,1)[0],0,tabget(tailtab,1)[1]]),'Tail_41','无尾鳍的尾端'),
 'NoPelvicFins':((-.059,-.015,.324),'Root','无腹鳍；此标注点不代表存在鳍'),
 'GillOpening_L':((.006,-.032,.897),'Head','小鳃孔'),
 'OpercularRidge_L':((-.018,-.034,.928),'Head','鳃盖骨隆棱'),
 'Eye_L':((-.115,-.044,.943),'Eye.L','左眼：独立转动'),
 'Eye_R':((-.115,.044,.943),'Eye.R','右眼：独立转动'),
 'Anus':((-.028,0,.301),'Root','肛门'),
})
empty=[]
for name,(p,target,label) in anchors.items():
    o=bpy.data.objects.new('Label_'+name,None);col.objects.link(o);o.empty_display_type='SPHERE';o.empty_display_size=.0006
    o.location=Vector(p)*S;bpy.context.view_layer.update();world=o.matrix_world.copy();o.parent=rig;o.parent_type='BONE';o.parent_bone=target
    bpy.context.view_layer.update();o.matrix_world=world;o['label_zh']=label;o['annotation_id']=name;empty.append(o)

scene.render.fps=30;actions=[]
def reset_pose():
    for p in rig.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0)
def rotate(name,axis,angle):
    b=rig.pose.bones[name];local=b.bone.matrix_local.to_3x3().inverted()@Vector(axis);b.rotation_quaternion=Quaternion(local,angle)
def key(name,f):rig.pose.bones[name].keyframe_insert('rotation_quaternion',frame=f,group=name)
def new_action(name):
    rig.animation_data_create();rig.animation_data.action=bpy.data.actions.new(name);actions.append(rig.animation_data.action);reset_pose()
new_action('Idle_FinPropulsion_2s')
for f in range(1,62):
    t=(f-1)/30
    for i in range(5):
        name='DorsalWave_%02d'%(i+1);rotate(name,(0,0,1),.20*sin(2*pi*6*t-i*.9));key(name,f)
    for sg,suf in [(-1,'L'),(1,'R')]:
        name='Pectoral.'+suf;rotate(name,(1,0,0),sg*.24*sin(2*pi*4*t));key(name,f)
        name='Eye.'+suf;rotate(name,(0,0,1),.045*sin(2*pi*(.5 if sg<0 else 1)*t));key(name,f)
new_action('TailCurl_Demonstration_3s')
for f in range(1,92):
    a=sin(pi*(f-1)/90)**2
    for i in range(41):
        name='Tail_%02d'%(i+1);rotate(name,(0,1,0),-.013*a*max(0,min(1,(i-8)/20)));key(name,f)
new_action('Jaw_SubtleOpen_1s')
for f in range(1,32):rotate('LowerJaw',(0,1,0),-.022*sin(pi*(f-1)/30)**2);key('LowerJaw',f)
for a in actions:
    a.use_fake_user=True
    for fc in a.fcurves:
        for kp in fc.keyframe_points:kp.interpolation='LINEAR'
rig.animation_data.action=None;reset_pose();scene.frame_start=1;scene.frame_end=61;scene.frame_set(1)

# NLA takes preserve animation separation in FBX.
for a in actions:
    track=rig.animation_data.nla_tracks.new();track.name=a.name;strip=track.strips.new(a.name,1,a);strip.extrapolation='NOTHING';track.mute=True
for tr in rig.animation_data.nla_tracks:tr.mute=False
bpy.ops.object.select_all(action='DESELECT')
for o in objects+[rig]+empty:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(ASSET,'Hippocampus_trimaculatus_Rigged.fbx'),use_selection=True,
 object_types={'MESH','ARMATURE','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
 use_space_transform=True,bake_space_transform=False,add_leaf_bones=False,use_armature_deform_only=False,
 mesh_smooth_type='FACE',use_mesh_modifiers=True,path_mode='RELATIVE',embed_textures=False,
 bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0)
for tr in rig.animation_data.nla_tracks:tr.mute=True
reset_pose();rig.animation_data.action=actions[0];scene.frame_set(1)
# glTF uses root-level skinned meshes; triangulate only temporary n-gon copies for tangents.
gltf_backup=[]
for ob in objects:
    world=ob.matrix_world.copy();gltf_backup.append((ob,ob.data,ob.parent,world))
    ob.data=ob.data.copy();ob.parent=None;ob.matrix_world=world
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>4]);bm.to_mesh(ob.data);bm.free();ob.data.update()
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'Hippocampus_trimaculatus_Rigged.glb'),export_format='GLB',use_selection=True,
 export_skins=True,export_animations=True,export_animation_mode='ACTIONS',export_yup=True,export_materials='EXPORT',export_tangents=True)
for ob,data,parent,world in gltf_backup:
    temp=ob.data;ob.data=data;ob.parent=parent;ob.matrix_world=world;bpy.data.meshes.remove(temp)
rig.animation_data.action=None;reset_pose();scene.frame_set(1)
for o in objects:o.data.calc_loop_triangles()
stats={'scientific_name':'Hippocampus trimaculatus','sex':'male','scale_m_per_authoring_unit':S,
 'counts':{'trunk_rings':11,'tail_rings':41,'coronet_tips':5,'dorsal_soft_rays':20,'pectoral_soft_rays_each':18,'anal_soft_rays':4,'dorsal_spines':0,'pelvic_fins':0,'caudal_fins':0},
 'snout_length':.1318,'head_length':.290,'HL_div_SnL':.290/.1318,
 'mesh_objects':len(objects),'vertices':sum(len(o.data.vertices) for o in objects),'triangles':sum(len(o.data.loop_triangles) for o in objects),
 'bone_count':len(spec),'texture_resolution':[N,N],'texture_normal_convention':'OpenGL tangent-space +Y',
 'anchors':{k:{'position_blender_m':[float(a*S) for a in v[0]],'bone':v[1],'label_zh':v[2]} for k,v in anchors.items()},
 'bones':spec,'animations':[a.name for a in actions],
 'references':['https://projectseahorse.org/wp-content/uploads/2021/06/Seahorse_ID_Guide_2004.pdf','https://eprints.cmfri.org.in/14956/'],
 'parts':[{'name':o.name,'anatomy':o['anatomy'],'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles)} for o in objects]}
open(os.path.join(ASSET,'AnatomyManifest.json'),'w').write(json.dumps(stats,ensure_ascii=False,indent=2))
print('EXPORTED',stats['vertices'],stats['triangles'],stats['bone_count'],flush=True)

# Review images rendered from the delivered geometry and PBR atlas.
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1400;scene.render.resolution_y=1700;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
world=bpy.data.worlds.new('Studio');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.13,.17,.20,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
target=Vector((-.006,0,.039))
def area(name,p,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=p;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-.12,-.18,.23),1.0,.17,(1,.94,.84));area('Fill',(.10,-.15,.09),.50,.16,(.78,.87,1));area('Rim',(.04,.10,.17),1.2,.12,(1,.92,.77))
cd=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',cd);scene.collection.objects.link(cam);scene.camera=cam
cd.type='ORTHO';cd.ortho_scale=.176;cd.lens=65
def render(name,p,look=None,scale=None):
    cam.location=p;cam.rotation_euler=(Vector(look or target)-cam.location).to_track_quat('-Z','Y').to_euler()
    if scale:cd.ortho_scale=scale
    scene.render.filepath=os.path.join(OUT,name);bpy.ops.render.render(write_still=True)
render('Preview_Side.png',(-.006,-.43,.039))
render('Preview_ThreeQuarter.png',(-.19,-.42,.10))
scene.render.resolution_x=1500;scene.render.resolution_y=1100
render('Preview_HeadDetail.png',(-.10,-.34,.14),(-.011,0,.104),.052)
for im in bpy.data.images:
    if im.source=='FILE':
        try:im.pack()
        except:pass
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Hippocampus_trimaculatus_Source.blend'))
print('BUILD_COMPLETE',flush=True)
