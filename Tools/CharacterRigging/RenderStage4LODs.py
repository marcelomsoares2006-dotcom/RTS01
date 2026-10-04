"""Render imported, accepted LODs for human visual review in Blender."""
from pathlib import Path
import bpy,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage4'
records=json.loads((REPORT/'lod-validation.json').read_text(encoding='utf-8'))
TEXTURES={
 'HeraldicKnight':'Assets/CharacterRigging/HeraldicKnight/Textures/HeraldicKnight_BaseColor.png',
 'Farmhand':'Assets/CharacterRigging/Stage2/Farmhand/Textures/Farmhand_BaseColor.png',
 'Spearman':'Assets/CharacterRigging/Stage2/Spearman/Textures/Spearman_BaseColor.png',
 'RedDeer':'Assets/CharacterRigging/Stage3/RedDeer/Textures/RedDeer_BaseColor.png'
}
for record in records:
    if not record.get('validated'):continue
    name=record['name'];level=record['lod']
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/record['path']))
    body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    image=bpy.data.images.load(str(ROOT/TEXTURES[name]),check_existing=True)
    mat=bpy.data.materials.new(name+'_QA');mat.use_nodes=True
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    mat.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color'])
    mat.node_tree.nodes.active=node
    body.data.materials.clear();body.data.materials.append(mat)
    points=[body.matrix_world@v.co for v in body.data.vertices]
    lo=Vector([min(p[i] for p in points) for i in range(3)])
    hi=Vector([max(p[i] for p in points) for i in range(3)])
    mid=(lo+hi)/2;size=max((hi-lo).x,(hi-lo).y,(hi-lo).z)
    camdata=bpy.data.cameras.new('QACamera');cam=bpy.data.objects.new('QACamera',camdata)
    bpy.context.scene.collection.objects.link(cam)
    cam.location=mid+Vector((2.4,-4,2.1))
    cam.rotation_euler=(mid-cam.location).to_track_quat('-Z','Y').to_euler()
    camdata.type='ORTHO';camdata.ortho_scale=size*1.65
    scene=bpy.context.scene;scene.camera=cam
    scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.color_type='TEXTURE'
    scene.display.shading.show_cavity=True;scene.render.resolution_x=640;scene.render.resolution_y=640
    scene.render.filepath=str(REPORT/(name+'-LOD'+str(level)+'.png'))
    scene.render.image_settings.file_format='PNG';bpy.ops.render.render(write_still=True)
    print('STAGE4_LOD_RENDER',name,level,flush=True)
