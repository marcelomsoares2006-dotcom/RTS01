"""Stage a separate rider and horse in an editable Blender scene for fit review.
This is an assembly prototype, not a mounted gameplay animation or Unity prefab.
"""
from pathlib import Path
import bpy,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'Tools/CharacterRigging/Working/Stage5';REPORT=ROOT/'Docs/CharacterMeshAudit/Stage5'
bpy.ops.wm.read_factory_settings(use_empty=True)
horse=ROOT/'Assets/CharacterRigging/Stage3/Horse/Models/Horse.fbx'
rider=ROOT/'Assets/CharacterRigging/Stage5/ArmoredKnight/Models/ArmoredKnight.fbx'
bpy.ops.import_scene.fbx(filepath=str(horse))
horse_rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
before=set(bpy.context.scene.objects)
bpy.ops.import_scene.fbx(filepath=str(rider))
rider_rig=next(o for o in set(bpy.context.scene.objects)-before if o.type=='ARMATURE')
for objects,base in [
    (before,ROOT/'Assets/CharacterRigging/Stage3/Horse/Textures/Horse_BaseColor.png'),
    (set(bpy.context.scene.objects)-before,ROOT/'Assets/CharacterRigging/Stage5/ArmoredKnight/Textures/ArmoredKnight_BaseColor.png')]:
    image=bpy.data.images.load(str(base),check_existing=True)
    mat=bpy.data.materials.new(base.stem+'_Review');mat.use_nodes=True
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    mat.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color'])
    mat.node_tree.nodes.active=node
    for obj in objects:
        if obj.type=='MESH':obj.data.materials.clear();obj.data.materials.append(mat)
horse_rig.animation_data_clear();rider_rig.animation_data_clear()
horse_rig.location=(0,0,0);rider_rig.location=(0,.10,.48)
for pb in rider_rig.pose.bones:pb.rotation_mode='XYZ'
for side,spread in [('Left',-.06),('Right',.06)]:
    upper=rider_rig.pose.bones.get('mixamorig:'+side+'UpLeg')
    lower=rider_rig.pose.bones.get('mixamorig:'+side+'Leg')
    if upper:upper.rotation_euler=(.35,spread,0)
    if lower:lower.rotation_euler=(-.90,0,0)
rider_rig.animation_data_create()
for frame in (1,24):
    for pb in rider_rig.pose.bones:
        pb.keyframe_insert(data_path='rotation_euler',frame=frame)
rider_rig.animation_data.action.name='MountedIdlePose'
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=24
bpy.context.scene.render.fps=24;bpy.context.scene.frame_set(1)
mounted_folder=ROOT/'Assets/CharacterRigging/Stage5/Mounted';mounted_folder.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='DESELECT');rider_rig.select_set(True)
for obj in bpy.context.scene.objects:
    if obj.type=='MESH' and obj.find_armature()==rider_rig:obj.select_set(True)
bpy.context.view_layer.objects.active=rider_rig
bpy.ops.export_scene.fbx(filepath=str(mounted_folder/'ArmoredKnight_MountedIdle.fbx'),
    use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
    use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
    axis_forward='-Z',axis_up='Y',path_mode='STRIP')
bpy.context.view_layer.update()
mark=bpy.data.objects.new('SeatReference_NoRuntimeAttachment',None)
bpy.context.scene.collection.objects.link(mark);mark.empty_display_type='SPHERE';mark.empty_display_size=.07
mark.location=(0,.10,1.35)
camera_data=bpy.data.cameras.new('ReviewCamera');camera=bpy.data.objects.new('ReviewCamera',camera_data)
bpy.context.scene.collection.objects.link(camera);camera.location=(3,-5,3.1)
camera.rotation_euler=(Vector((0,0,1.15))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO';camera_data.ortho_scale=3.6;bpy.context.scene.camera=camera
bpy.context.scene.render.engine='BLENDER_WORKBENCH';bpy.context.scene.display.shading.color_type='TEXTURE'
bpy.context.scene.render.resolution_x=800;bpy.context.scene.render.resolution_y=800
bpy.context.scene.render.filepath=str(REPORT/'MountedPrototype.png')
bpy.context.scene.render.image_settings.file_format='PNG';bpy.ops.render.render(write_still=True)
WORK.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'MountedPrototype.blend'),compress=True)
(REPORT/'mounted-prototype.json').write_text(json.dumps({
  'horse':str(horse.relative_to(ROOT)),'rider':str(rider.relative_to(ROOT)),
  'rider_location':[0,.10,.48],
  'status':'visual fit prototype only; no shared saddle socket, locomotion or mounted attack'},indent=2),encoding='utf-8')
print('STAGE5_MOUNT_PREVIEW',flush=True)
