import bpy,bmesh,os,json,math
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=ROOT if os.path.isdir(os.path.join(ROOT,'Assets','Trimaculatus')) else os.path.join(ROOT,'outputs','Trimaculatus_URP')
ASSET=os.path.join(OUT,'Assets','Trimaculatus')
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'Hippocampus_trimaculatus_Source.blend'))
scene=bpy.context.scene;scene.frame_set(1)
rig=bpy.data.objects['HT_Rig'];rig.animation_data.action=None
for tr in rig.animation_data.nla_tracks:tr.mute=True
for b in rig.pose.bones:b.matrix_basis.identity()
meshes=[o for o in scene.objects if o.type=='MESH'];body=bpy.data.objects['AxialArmor_11Trunk_41Tail']
missing=[];maxsum=0;maxinf=0;degenerate=[];topology={};coords=[];raw={};anchors={}
def evaluated(o):
    dg=bpy.context.evaluated_depsgraph_get();dg.update();e=o.evaluated_get(dg);me=e.to_mesh()
    pts=np.array([tuple(e.matrix_world@v.co) for v in me.vertices]);e.to_mesh_clear();return pts
for ob in meshes:
    me=ob.data;me.calc_loop_triangles();p=np.array([tuple(ob.matrix_world@v.co) for v in me.vertices]);coords.extend(p);raw[ob.name]=p
    for v in me.vertices:
        ww=[g.weight for g in v.groups if g.weight>1e-7]
        if not ww:missing.append([ob.name,v.index])
        maxsum=max(maxsum,abs(sum(ww)-1));maxinf=max(maxinf,len(ww))
    for tr in me.loop_triangles:
        a,b,c=[me.vertices[i].co for i in tr.vertices]
        if (b-a).cross(c-a).length<1e-17:degenerate.append([ob.name,tr.index])
    bm=bmesh.new();bm.from_mesh(me)
    topology[ob.name]={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges)};bm.free()
    if not me.uv_layers:missing.append([ob.name,'UV'])
for o in scene.objects:
    if o.name.startswith('Label_'):anchors[o.name]=list(o.matrix_world.translation)
coords=np.array(coords);kd=KDTree(len(coords))
for i,p in enumerate(coords):kd.insert(p,i)
kd.balance();mirror=max(kd.find((p[0],-p[1],p[2]))[2] for p in coords)

# Test UV interiors at native resolution: adjacent shared edges do not count as overlap.
N=2048;hits=np.zeros((N,N),np.uint16);owner=np.full((N,N),-1,np.int32);outside=0;uvzero=0;pairs=[];tri_names=[]
for ob in meshes:
    me=ob.data;uvs=me.uv_layers.active.data
    for tr in me.loop_triangles:
        tid=len(tri_names);tri_names.append(ob.name+':'+str(tr.index))
        uv=np.array([uvs[l].uv[:] for l in tr.loops])
        outside+=int(np.any(uv<0) or np.any(uv>1));uv=uv*N-.5
        lo=np.maximum(0,np.ceil(uv.min(0)).astype(int));hi=np.minimum(N-1,np.floor(uv.max(0)).astype(int))
        a,b,c=uv;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:uvzero+=1;continue
        if np.any(hi<lo):continue
        yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        w0=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        w1=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w2=1-w0-w1
        inside=(w0>1e-7)&(w1>1e-7)&(w2>1e-7)
        prev=owner[lo[1]:hi[1]+1,lo[0]:hi[0]+1];ov=inside&(prev>=0)
        if ov.any() and len(pairs)<30:
            for old in np.unique(prev[ov]):pairs.append([tri_names[int(old)],tri_names[tid],int(ov.sum())])
        prev[inside]=tid;hits[lo[1]:hi[1]+1,lo[0]:hi[0]+1]+=inside.astype(np.uint16)
print('UV_COMPLETE',int((hits>1).sum()),flush=True)
animations={};animation_refs={}
for name in ['Idle_FinPropulsion_2s','TailCurl_Demonstration_3s','Jaw_SubtleOpen_1s']:
    act=bpy.data.actions[name];rig.animation_data.action=act
    if hasattr(act,'slots') and len(act.slots):rig.animation_data.action_slot=act.slots[0]
    probe=bpy.data.objects['Dorsal_Single_20Soft_Membrane' if 'Idle' in name else 'Mandible_LowerJaw' if 'Jaw' in name else body.name]
    scene.frame_set(1);a=evaluated(probe)
    # Avoid a complete fin cycle and sample the mid-point of the curl demonstration.
    frame=3 if 'Idle' in name else 16 if 'Jaw' in name else 46;scene.frame_set(frame);b=evaluated(probe)
    animation_refs[name]={'probe':probe.name,'frame':frame,'positions':b}
    animations[name]={'frame_range':list(act.frame_range),'probe':probe.name,'max_displacement_m':float(np.linalg.norm(a-b,axis=1).max())}
rig.animation_data.action=None
for b in rig.pose.bones:b.matrix_basis.identity()
scene.frame_set(1)
source={'mesh_objects':len(meshes),'vertices':sum(len(o.data.vertices) for o in meshes),'triangles':sum(len(o.data.loop_triangles) for o in meshes),
 'bones':len(rig.data.bones),'label_anchors':len(anchors),'bounds_m':{'min':coords.min(0).tolist(),'max':coords.max(0).tolist(),'size':(coords.max(0)-coords.min(0)).tolist()},
 'unweighted_vertices':len(missing),'max_skin_weight_sum_error':maxsum,'max_bone_influences':maxinf,
 'mirror_max_error_m':mirror,'degenerate_triangles':degenerate,'mesh_topology':topology,
 'UV_outside_0_1_triangles':outside,'UV_zero_area_triangles':uvzero,'UV_overlap_interior_texels_2K':int((hits>1).sum()),
 'UV_overlap_examples':pairs,'UV_occupancy':float((hits>0).mean()),'animation_probes':animations}

# Fresh-process-style import: validate the real FBX rather than merely its source scene.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(ASSET,'Hippocampus_trimaculatus_Rigged.fbx'),use_anim=True,automatic_bone_orientation=False)
imported=[o for o in bpy.context.scene.objects if o.type=='MESH'];irig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
if irig.animation_data:
    irig.animation_data.action=None
    for tr in irig.animation_data.nla_tracks:tr.mute=True
for b in irig.pose.bones:b.matrix_basis.identity()
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
diff={};icoords=[];uv_err=0;influences=0
for ob in imported:
    pts=np.array([tuple(ob.matrix_world@v.co) for v in ob.data.vertices]);icoords.extend(pts)
    src=raw.get(ob.name)
    if src is not None:
        tree=KDTree(len(src))
        for i,p in enumerate(src):tree.insert(p,i)
        tree.balance();diff[ob.name]=max(tree.find(p)[2] for p in pts)
    if not ob.data.uv_layers:uv_err+=1
    influences+=sum(not any(g.weight>1e-7 for g in v.groups) for v in ob.data.vertices)
icoords=np.array(icoords);anchor_error={}
for name,old in anchors.items():
    ob=bpy.data.objects.get(name)
    anchor_error[name]=float(np.linalg.norm(np.array(ob.matrix_world.translation)-old)) if ob else None
from io_scene_fbx import parse_fbx
fbxroot,fbxversion=parse_fbx.parse(os.path.join(ASSET,'Hippocampus_trimaculatus_Rigged.fbx'))
fbxobjects=next(e for e in fbxroot.elems if e.id==b'Objects')
stacks=[e.props[1].decode('utf-8','replace').split('\x00')[0] for e in fbxobjects.elems if e.id==b'AnimationStack']
fbx={'mesh_objects':len(imported),'vertices':sum(len(o.data.vertices) for o in imported),
 'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in imported),
 'bones':len(irig.data.bones),'animation_stacks':stacks,'imported_per_object_action_count':len(bpy.data.actions),
 'skeleton_actions':[{'name':a.name,'frames':list(a.frame_range)} for a in bpy.data.actions if a.name.startswith('HT_Rig|')],
 'missing_uv_meshes':uv_err,'unweighted_vertices':influences,'rest_geometry_max_error_m':max(diff.values()),
 'per_mesh_error_m':diff,'label_anchors':len(anchor_error),'missing_anchors':[k for k,v in anchor_error.items() if v is None],
 'label_max_position_error_m':max(v for v in anchor_error.values() if v is not None),
 'bounds_m':{'min':icoords.min(0).tolist(),'max':icoords.max(0).tolist(),'size':(icoords.max(0)-icoords.min(0)).tolist()}}
fbx['animation_geometry_errors_m']={}
for name,ref in animation_refs.items():
    for ob in bpy.context.scene.objects:
        if ob.animation_data:
            ob.animation_data.action=None
            for track in ob.animation_data.nla_tracks:track.mute=True
            action=bpy.data.actions.get(ob.name+'|'+name)
            if action:
                ob.animation_data.action=action
                if hasattr(action,'slots') and len(action.slots):ob.animation_data.action_slot=action.slots[0]
    for b in irig.pose.bones:b.matrix_basis.identity()
    bpy.context.scene.frame_set(ref['frame']);p=evaluated(bpy.data.objects[ref['probe']])
    tree=KDTree(len(ref['positions']))
    for i,v in enumerate(ref['positions']):tree.insert(v,i)
    tree.balance();fbx['animation_geometry_errors_m'][name]=max(tree.find(v)[2] for v in p)
manifest=json.load(open(os.path.join(ASSET,'AnatomyManifest.json')))
texture={}
for name in ['HT_BaseColor_2K','HT_Normal_OpenGL_2K','HT_Roughness_2K','HT_Metallic_2K','HT_URP_MetallicSmoothness_2K']:
    im=bpy.data.images.load(os.path.join(ASSET,'Textures',name+'.png'));texture[name]={'size':list(im.size),'channels':im.channels}
report={'source':source,'FBX_roundtrip':fbx,'textures':texture,'anatomy_counts':manifest['counts'],
 'Unity_editor_tested':False,'Unity_note':'Unity Editor is not installed here; FBX, animation, UV, rig and texture checks were performed in Blender 4.5.3. URP setup uses documented Unity 2022.3+ APIs.'}
report['passed']=(not missing and maxsum<1e-5 and maxinf<=2 and not degenerate and outside==0 and uvzero==0 and int((hits>1).sum())==0 and mirror<1e-6 and
 all(x['boundary_edges']==0 and x['nonmanifold_edges']==0 for x in topology.values()) and fbx['rest_geometry_max_error_m']<1e-6 and not fbx['missing_anchors'] and fbx['label_max_position_error_m']<1e-6 and len(stacks)==3 and max(fbx['animation_geometry_errors_m'].values())<1e-5)
open(os.path.join(OUT,'Documentation','ValidationReport.json'),'w').write(json.dumps(report,ensure_ascii=False,indent=2))
print('VALIDATION_RESULT',json.dumps(report),flush=True)
