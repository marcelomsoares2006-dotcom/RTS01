import bpy
from mathutils import Vector
sc=bpy.context.scene
sc.render.engine='BLENDER_WORKBENCH'
sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'
sc.render.resolution_x=1400; sc.render.resolution_y=800
cam=bpy.data.objects.new('Cam',bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.location=(0.0,-3.2,0.55); cam.rotation_euler=(1.5708,0,0)
cam.data.lens=40
sc.render.filepath=str(__import__('pathlib').Path(bpy.data.filepath).parents[4]/'Docs/CharacterMeshAudit/Archer/accessories_preview.png')
bpy.ops.render.render(write_still=True)
