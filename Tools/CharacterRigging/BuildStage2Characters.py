"""Prepare copied Farmhand/Spearman meshes, FK rigs, sockets and diagnostic poses.
Run with Blender --background --factory-startup --disable-autoexec --python this_file.
The original Inputs files are never saved. PoseCheck is a rig diagnostic, not final gameplay animation.
"""
from pathlib import Path
import bpy, bmesh, json, math, statistics, hashlib
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'Tools/CharacterRigging/Working/Stage2'
OUT=ROOT/'Assets/CharacterRigging/Stage2'
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage2'
CONFIG={
 'Farmhand':('*Straw_Hat_Farmhand*.blend',15000),
 'Spearman':('*Spearman*.blend',18000),
}

def active(o):
    if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o

def bbox(o):
    v=[o.matrix_world@p.co for p in o.data.vertices]
    return [Vector([min(p[i] for p in v) for i in range(3)]),Vector([max(p[i] for p in v) for i in range(3)])]

def triangles(o):
    return sum(len(p.vertices)-2 for p in o.data.polygons)

def simplify(o,target):
    active(o)
    if triangles(o)>target:
        tri=o.modifiers.new('TriangulateSource','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
        dec=o.modifiers.new('RTS_LightMesh','DECIMATE');dec.ratio=target/triangles(o);dec.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=dec.name)
    bm=bmesh.new();bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
    bm.to_mesh(o.data);bm.free();o.data.update()
    for p in o.data.polygons:p.use_smooth=True

def texture_export(body,name):
    folder=OUT/name/'Textures';folder.mkdir(parents=True,exist_ok=True)
    m=body.data.materials[0];m.name=name+'_Material'
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    records={}
    def save_image(image,label):
        dst=folder/(name+'_'+label+'.png')
        image.filepath_raw=str(dst);image.file_format='PNG';image.save()
        records[label]=str(dst.relative_to(ROOT));return image
    image=bs.inputs['Base Color'].links[0].from_node.image
    save_image(image,'BaseColor')
    normal=next((n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and any(l.to_node.type=='NORMAL_MAP' for l in n.outputs['Color'].links)),None)
    if normal:save_image(normal,'Normal')
    for link in list(bs.inputs['Emission Color'].links):m.node_tree.links.remove(link)
    bs.inputs['Emission Color'].default_value=(0,0,0,1);bs.inputs['Emission Strength'].default_value=0
    # Preserve the original ORM image as source; Unity build uses base colour and normal in this stage.
    orm=next((n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image not in (image,normal)),None)
    if orm:save_image(orm,'SourcePackedMap')
    return records

def create_rig(body,name):
    lo,hi=bbox(body);width=hi.x-lo.x
    points=[v.co for v in body.data.vertices]
    def arm_point(x):
        candidates=[p for p in points if abs(p.x-x)<width*.014 and p.z>1.1]
        assert candidates,('No arm sample',name,x)
        return (x,statistics.median(p.y for p in candidates),statistics.median(p.z for p in candidates))
    specs=[]
    def bone(n,h,t,parent=None,deform=True):specs.append((n,h,t,parent,deform))
    bone('Root',(0,0,0),(0,0,.15),None,False)
    bone('Hips',(0,.035,.94),(0,.035,1.065),'Root')
    bone('Spine',(0,.035,1.065),(0,.035,1.20),'Hips')
    bone('Spine1',(0,.035,1.20),(0,.035,1.34),'Spine')
    bone('Spine2',(0,.035,1.34),(0,.035,1.45),'Spine1')
    bone('Neck',(0,.035,1.45),(0,.035,1.55),'Spine2')
    bone('Head',(0,.035,1.55),(0,.035,1.75),'Neck')
    for side,sign in [('Left',1),('Right',-1)]:
        shoulder=arm_point(sign*width*.165);elbow=arm_point(sign*width*.30)
        wrist=arm_point(sign*width*.425);tip=arm_point(sign*width*.48)
        bone(side+'Shoulder',(sign*.06,.035,1.42),shoulder,'Spine2')
        bone(side+'Arm',shoulder,elbow,side+'Shoulder')
        bone(side+'ForeArm',elbow,wrist,side+'Arm')
        bone(side+'Hand',wrist,tip,side+'ForeArm')
        bone(side+'UpLeg',(sign*.115,.04,.94),(sign*.165,.035,.52),'Hips')
        bone(side+'Leg',(sign*.165,.035,.52),(sign*.17,.09,.125),side+'UpLeg')
        bone(side+'Foot',(sign*.17,.09,.125),(sign*.17,-.085,.045),side+'Leg')
        bone(side+'ToeBase',(sign*.17,-.085,.045),(sign*.17,-.16,.045),side+'Foot')
    data=bpy.data.armatures.new(name+'_Skeleton');rig=bpy.data.objects.new(name+'_Rig',data)
    bpy.context.scene.collection.objects.link(rig);active(rig);bpy.ops.object.mode_set(mode='EDIT')
    for n,h,t,parent,deform in specs:
        b=data.edit_bones.new('mixamorig:'+n if n!='Root' else n);b.head=h;b.tail=t;b.use_deform=deform
        if parent:b.parent=data.edit_bones['mixamorig:'+parent if parent!='Root' else parent]
        b.align_roll(Vector((0,-1,0)) if 'Arm' in n or 'Hand' in n or 'Shoulder' in n else Vector((0,0,1)))
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;data.display_type='OCTAHEDRAL'
    rig['usage']='FK deformation rig. PoseCheck is diagnostic only. Humanoid mapping uses mixamorig bone names.'
    return rig,specs

def bind(body,rig):
    active(body);rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    assert body.find_armature()==rig
    active(body)
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
    unweighted=[v.index for v in body.data.vertices if not any(g.weight>1e-6 for g in v.groups)]
    if unweighted:
        # Generated meshes can contain internal/disconnected geometry. Solve weights on
        # a temporary watertight volume, then interpolate back without changing the UV mesh.
        proxy=bpy.data.objects.new('WeightVolume',body.data.copy());bpy.context.scene.collection.objects.link(proxy)
        active(proxy)
        remesh=proxy.modifiers.new('WatertightWeightProxy','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.025
        bpy.ops.object.modifier_apply(modifier=remesh.name)
        smooth=proxy.modifiers.new('SmoothWeightProxy','SMOOTH');smooth.factor=1;smooth.iterations=3
        bpy.ops.object.modifier_apply(modifier=smooth.name)
        rig.select_set(True);bpy.context.view_layer.objects.active=rig
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        missing=[v.index for v in proxy.data.vertices if not any(g.weight>1e-6 for g in v.groups)]
        assert len(missing)<len(proxy.data.vertices)*.02,f'Weight proxy has too many gaps: {len(missing)}'
        valid=[v for v in proxy.data.vertices if v.index not in set(missing)]
        tree=KDTree(len(valid))
        for v in valid:tree.insert(v.co,v.index)
        tree.balance();body.vertex_groups.clear()
        for group in proxy.vertex_groups:body.vertex_groups.new(name=group.name)
        for v in body.data.vertices:
            values={}
            for _,index,distance in tree.find_n(v.co,4):
                factor=1/max(distance,.001)**2
                for entry in proxy.data.vertices[index].groups:
                    values[entry.group]=values.get(entry.group,0)+entry.weight*factor
            top=sorted(values.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(w for _,w in top)
            assert total>0
            for group,w in top:body.vertex_groups[group].add([v.index],w/total,'REPLACE')
        bpy.data.objects.remove(proxy,do_unlink=True)
        body['weight_method']='Bone heat on voxel proxy; nearest-four-vertex interpolation to preserved UV mesh'
    else:body['weight_method']='Direct bone heat'
    return max(sum(g.weight>1e-6 for g in v.groups) for v in body.data.vertices)

def attach(o,rig,bone_name,offset):
    b=rig.data.bones['mixamorig:'+bone_name]
    lo,hi=bbox(o);center=(lo+hi)/2
    grip=center.copy()
    if o.name.endswith('_Spear'):grip.z=lo.z+(hi.z-lo.z)*.53
    target=b.head_local+Vector(offset)
    world=o.matrix_world.copy();world.translation+=target-grip
    o.parent=rig;o.parent_type='BONE';o.parent_bone=b.name
    bpy.context.view_layer.update();o.matrix_world=world;bpy.context.view_layer.update()
    o['attachment_bone']=b.name

def pose_check(rig):
    rig.animation_data_create()
    for pb in rig.pose.bones:pb.rotation_mode='XYZ'
    bends={
      'LeftUpLeg':(.30,0,0),'LeftLeg':(-.60,0,0),
      'RightUpLeg':(-.20,0,0),'RightLeg':(-.15,0,0),
      'LeftForeArm':(.45,0,0),'RightForeArm':(.65,0,0),
      'Spine2':(0,.10,0),'Head':(0,.08,0),
    }
    for frame,factor in [(1,0),(13,1),(25,-.45),(37,0)]:
        for pb in rig.pose.bones:
            angles=bends.get(pb.name.replace('mixamorig:',''),(0,0,0))
            pb.rotation_euler=tuple(a*factor for a in angles)
            pb.keyframe_insert(data_path='rotation_euler',frame=frame)
    rig.animation_data.action.name='PoseCheck'
    bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=37
    bpy.context.scene.render.fps=24;bpy.context.scene.frame_set(1)

def render_view(body,rig,objects,path,frame):
    scene=bpy.context.scene;scene.frame_set(frame)
    data=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',data)
    scene.collection.objects.link(cam);cam.location=(2,-5,2.6)
    cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO';data.ortho_scale=3.0 if objects else 2.45;scene.camera=cam
    scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=640;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    s=scene.display.shading;s.light='STUDIO';s.color_type='TEXTURE';s.show_cavity=True;s.cavity_type='BOTH'
    s.background_type='WORLD';scene.world.color=(.08,.08,.08)
    for m in body.data.materials:
        bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        m.node_tree.nodes.active=bs.inputs['Base Color'].links[0].from_node
    scene.render.filepath=str(path);scene.render.image_settings.file_format='PNG'
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam,do_unlink=True)

def main():
    WORK.mkdir(parents=True,exist_ok=True);REPORT.mkdir(parents=True,exist_ok=True)
    records=[]
    for name,(pattern,target) in CONFIG.items():
        src=next((ROOT/'Tools/CharacterRigging/Inputs').glob(pattern))
        source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
        bpy.ops.wm.open_mainfile(filepath=str(src),load_ui=False,use_scripts=False)
        body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
        for o in list(bpy.context.scene.objects):
            if o!=body:bpy.data.objects.remove(o,do_unlink=True)
        body.data.transform(body.matrix_world.copy());body.parent=None;body.matrix_world=Matrix.Identity(4)
        original=triangles(body);parts=[]
        if name=='Spearman':
            active(body);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
            components=sorted([o for o in bpy.context.scene.objects if o.type=='MESH'],key=lambda o:len(o.data.vertices),reverse=True)
            body=components[0]
            for o in components[1:]:
                if len(o.data.vertices)<100:bpy.data.objects.remove(o,do_unlink=True)
                else:parts.append(o)
            assert len(parts)==2
        lo,hi=bbox(body);height=hi.z-lo.z
        transform=Matrix.Scale(1.8/height,4)@Matrix.Translation((-(lo.x+hi.x)/2,0,-lo.z))
        for o in [body]+parts:o.data.transform(transform)
        body.name=name+'_Body';simplify(body,target)
        for o in parts:
            lo,hi=bbox(o);o.name=name+('_Spear' if (hi.z-lo.z)/max(hi.x-lo.x,.001)>6 else '_Shield')
            simplify(o,800 if o.name.endswith('_Spear') else 1200)
        textures=texture_export(body,name)
        rig,specs=create_rig(body,name);influences=bind(body,rig)
        for o in parts:attach(o,rig,'RightHand' if o.name.endswith('_Spear') else 'LeftHand',(0,-.04,0))
        pose_check(rig)
        render_view(body,rig,parts,REPORT/(name+'-rest.png'),1)
        render_view(body,rig,parts,REPORT/(name+'-bend.png'),13)
        scene=bpy.context.scene;scene.frame_set(1)
        asset=OUT/name/'Models';asset.mkdir(parents=True,exist_ok=True)
        active(rig)
        for o in [body]+parts:o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(asset/(name+'.fbx')),use_selection=True,
          object_types={'ARMATURE','MESH'},add_leaf_bones=False,use_armature_deform_only=True,
          bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
          bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',path_mode='STRIP')
        bpy.ops.wm.save_as_mainfile(filepath=str(WORK/(name+'.blend')),compress=True)
        record={'name':name,'source':str(src.relative_to(ROOT)),'source_sha256':source_hash,
          'source_triangles':original,'body_triangles':triangles(body),'total_triangles':sum(triangles(o) for o in [body]+parts),
          'height_metres':1.8,'deform_bones':sum(b.use_deform for b in rig.data.bones),'max_influences':influences,
          'textures':textures,'accessories':[{'name':o.name,'bone':o.parent_bone,'triangles':triangles(o)} for o in parts],
          'joint_positions':[{'name':n,'head':list(h),'tail':list(t)} for n,h,t,_,_ in specs],
          'animation':'PoseCheck: diagnostic, not final gameplay movement'}
        assert hashlib.sha256(src.read_bytes()).hexdigest()==source_hash
        records.append(record);(REPORT/'build-report.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
        print('STAGE2_BUILT',name,record['total_triangles'],flush=True)

if __name__=='__main__':main()
