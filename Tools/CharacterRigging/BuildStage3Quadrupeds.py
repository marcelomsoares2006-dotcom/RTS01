"""Build lightweight diagnostic quadruped rigs from copied Blender sources.
Run: blender --background --factory-startup --disable-autoexec --python BuildStage3Quadrupeds.py
PoseCheck is a joint diagnostic, not authored locomotion.
"""
from pathlib import Path
import bpy, bmesh, hashlib, json, math
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
INPUT=ROOT/'Tools/CharacterRigging/Inputs'
WORK=ROOT/'Tools/CharacterRigging/Working/Stage3'
OUT=ROOT/'Assets/CharacterRigging/Stage3'
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage3'
CONFIG={
 'Horse':('Meshy_AI_knight_horse_0928223644_texture.blend',30000,1.65),
 'RedDeer':('Meshy_AI_Red_Deer_Stag_0928222254_texture.blend',18000,1.8),
}

def select(o):
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o

def bounds(o):
    p=[v.co for v in o.data.vertices]
    return ([min(v[i] for v in p) for i in range(3)],[max(v[i] for v in p) for i in range(3)])

def tris(o):return sum(len(p.vertices)-2 for p in o.data.polygons)

def material_images(body,name):
    path=OUT/name/'Textures';path.mkdir(parents=True,exist_ok=True)
    mat=body.data.materials[0];mat.name=name+'_Material'
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    linked=shader.inputs['Base Color'].links
    assert linked and linked[0].from_node.type=='TEX_IMAGE',name+' missing base colour'
    base=linked[0].from_node.image
    result={}
    for label,img in [('BaseColor',base)]+[(f'Source_{i}',im) for i,im in enumerate(bpy.data.images) if im!=base and im.size[0]>16]:
        dst=path/(name+'_'+label+'.png');img.filepath_raw=str(dst);img.file_format='PNG';img.save()
        result[label]=str(dst.relative_to(ROOT))
    for link in list(shader.inputs['Emission Color'].links):mat.node_tree.links.remove(link)
    shader.inputs['Emission Color'].default_value=(0,0,0,1);shader.inputs['Emission Strength'].default_value=0
    return result

def rig_for(body,name):
    lo,hi=bounds(body);length=hi[1]-lo[1];height=hi[2]-lo[2]
    # Sources face negative Y; rear and front leg joints follow the original stance.
    rear=lo[1]+.76*length;front=lo[1]+.24*length
    shoulder_z=.73*height;hip_z=.72*height;hoof_z=.05*height
    knee_z=.41*height;pastern_z=.17*height
    side=max(.14,(hi[0]-lo[0])*.29)
    specs=[]
    def add(n,a,b,parent=None,deform=True):specs.append((n,a,b,parent,deform))
    add('Root',(0,0,0),(0,0,.18),None,False)
    add('Pelvis',(0,rear,hip_z),(0,rear-.14*length,hip_z),'Root')
    add('Spine',(0,rear-.14*length,hip_z),(0,front+.12*length,shoulder_z),'Pelvis')
    add('Chest',(0,front+.12*length,shoulder_z),(0,front,shoulder_z),'Spine')
    add('Neck',(0,front,shoulder_z),(0,lo[1]+.07*length,.87*height),'Chest')
    add('Head',(0,lo[1]+.07*length,.87*height),(0,lo[1]+.01*length,.92*height),'Neck')
    add('Tail',(0,rear,hip_z),(0,hi[1],.55*height),'Pelvis')
    for side_name,x in [('Left',side),('Right',-side)]:
        for pair,y,z,anchor in [('Front',front,shoulder_z,'Chest'),('Hind',rear,hip_z,'Pelvis')]:
            base=side_name+pair
            add(base+'Upper',(x,y,z),(x,y+.025*length,knee_z),anchor)
            add(base+'Lower',(x,y+.025*length,knee_z),(x,y,pastern_z),base+'Upper')
            add(base+'Hoof',(x,y,pastern_z),(x,y-.035*length,hoof_z),base+'Lower')
    data=bpy.data.armatures.new(name+'_Skeleton');rig=bpy.data.objects.new(name+'_Rig',data)
    bpy.context.scene.collection.objects.link(rig);select(rig);bpy.ops.object.mode_set(mode='EDIT')
    for n,a,b,parent,deform in specs:
        bone=data.edit_bones.new(n);bone.head=a;bone.tail=b;bone.use_deform=deform
        if parent:bone.parent=data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
    return rig,specs,{'front_y':front,'rear_y':rear,'side_x':side,'knee_z':knee_z,'pastern_z':pastern_z,'shoulder_z':shoulder_z,'hip_z':hip_z,'height':height,'lo':lo,'hi':hi}

def closest_segment(p,a,b):
    a=Vector(a);b=Vector(b);d=b-a
    t=max(0,min(1,(p-a).dot(d)/d.length_squared));return (p-(a+d*t)).length

def pose(rig):
    for pb in rig.pose.bones:pb.rotation_mode='XYZ'
    bends={'LeftFrontUpper':.20,'LeftFrontLower':-.42,'RightFrontUpper':-.16,'RightFrontLower':.30,
           'LeftHindUpper':-.21,'LeftHindLower':.36,'RightHindUpper':.17,'RightHindLower':-.31,
           'Neck':.10,'Head':-.07,'Tail':.12}
    for frame,factor in [(1,0),(13,1),(25,-.5),(37,0)]:
        for pb in rig.pose.bones:
            pb.rotation_euler=(bends.get(pb.name,0)*factor,0,0)
            pb.keyframe_insert(data_path='rotation_euler',frame=frame)
    rig.animation_data.action.name='PoseCheck'
    bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=37;bpy.context.scene.render.fps=24
    bpy.context.scene.frame_set(1)

def render(body,frame,path):
    scene=bpy.context.scene;scene.frame_set(frame)
    lo,hi=bounds(body);mid=Vector(((lo[0]+hi[0])/2,(lo[1]+hi[1])/2,(lo[2]+hi[2])/2))
    camdata=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',camdata)
    scene.collection.objects.link(cam);cam.location=mid+Vector((3,-4,2.1))
    cam.rotation_euler=(mid-cam.location).to_track_quat('-Z','Y').to_euler()
    camdata.type='ORTHO';camdata.ortho_scale=max(hi[2]-lo[2],hi[1]-lo[1])*1.75;scene.camera=cam
    scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=800;scene.render.resolution_y=720
    scene.display.shading.color_type='TEXTURE';scene.display.shading.show_cavity=True
    scene.render.filepath=str(path);scene.render.image_settings.file_format='PNG'
    bpy.ops.render.render(write_still=True);bpy.data.objects.remove(cam,do_unlink=True)

def main():
    WORK.mkdir(parents=True,exist_ok=True);REPORT.mkdir(parents=True,exist_ok=True)
    records=[]
    for name,(file,target,scale_height) in CONFIG.items():
        src=INPUT/file;digest=hashlib.sha256(src.read_bytes()).hexdigest()
        bpy.ops.wm.open_mainfile(filepath=str(src),load_ui=False,use_scripts=False)
        body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
        for o in list(bpy.context.scene.objects):
            if o!=body:bpy.data.objects.remove(o,do_unlink=True)
        body.data.transform(body.matrix_world.copy());body.parent=None;body.matrix_world=Matrix.Identity(4)
        original=tris(body);lo,hi=bounds(body)
        world=Matrix.Scale(scale_height/(hi[2]-lo[2]),4)@Matrix.Translation((-(lo[0]+hi[0])/2,-(lo[1]+hi[1])/2,-lo[2]))
        body.data.transform(world);body.name=name+'_Body'
        select(body);dec=body.modifiers.new('RTS_LightMesh','DECIMATE');dec.ratio=target/original
        dec.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=dec.name)
        bm=bmesh.new();bm.from_mesh(body.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
        bm.to_mesh(body.data);bm.free();body.data.update()
        for p in body.data.polygons:p.use_smooth=True
        textures=material_images(body,name)
        rig,specs,layout=rig_for(body,name)
        # Assign normalized spatial weights to the already posed quadruped.
        groups={n:body.vertex_groups.new(name=n) for n,a,b,parent,deform in specs if deform}
        geom={n:(a,b) for n,a,b,parent,deform in specs if deform}
        front=layout['front_y'];rear=layout['rear_y'];side=layout['side_x']
        for v in body.data.vertices:
            p=v.co;y=p.y;z=p.z;x=p.x
            if y<front and z>layout['shoulder_z']*.98:
                candidates=['Neck','Head']
                if name=='RedDeer' and z>layout['height']*.74 and y<front-.06*(rear-front):candidates=['Head']
            elif y>rear+.07*(rear-front) and z>layout['hip_z']*.65:candidates=['Pelvis','Tail']
            elif z<min(layout['shoulder_z'],layout['hip_z'])*.82 and abs(x)>side*.31:
                base=('Left' if x>=0 else 'Right')+('Front' if abs(y-front)<abs(y-rear) else 'Hind')
                candidates=[base+'Upper',base+'Lower',base+'Hoof']
            else:candidates=['Pelvis','Spine','Chest','Neck']
            scores=sorted(((n,1/(closest_segment(p,*geom[n])+.055)**3) for n in candidates),key=lambda t:t[1],reverse=True)[:4]
            total=sum(w for _,w in scores)
            for n,w in scores:groups[n].add([v.index],w/total,'REPLACE')
        body.parent=rig;body.matrix_parent_inverse=Matrix.Identity(4)
        arm=body.modifiers.new('QuadrupedSkin','ARMATURE');arm.object=rig
        pose(rig)
        render(body,1,REPORT/(name+'-rest.png'));render(body,13,REPORT/(name+'-bend.png'))
        bpy.context.scene.frame_set(1)
        model=OUT/name/'Models';model.mkdir(parents=True,exist_ok=True)
        select(rig);body.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(model/(name+'.fbx')),use_selection=True,
          object_types={'ARMATURE','MESH'},add_leaf_bones=False,use_armature_deform_only=True,
          bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
          bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
        bpy.ops.wm.save_as_mainfile(filepath=str(WORK/(name+'.blend')),compress=True)
        assert hashlib.sha256(src.read_bytes()).hexdigest()==digest
        record={'name':name,'source':str(src.relative_to(ROOT)),'source_sha256':digest,
          'source_triangles':original,'triangles':tris(body),'height_metres':scale_height,
          'bone_count':len(rig.data.bones),'deform_bones':sum(b.use_deform for b in rig.data.bones),
          'textures':textures,'rig':'Spatial FK quadruped; PoseCheck is diagnostic, not locomotion'}
        records.append(record);(REPORT/'build-report.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
        print('STAGE3_BUILT',name,record['triangles'],flush=True)

if __name__=='__main__':main()
