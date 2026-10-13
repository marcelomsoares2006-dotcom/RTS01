import bpy, os, math
from mathutils import Vector

blend = os.path.abspath('Assets/CityArchitecture/Generated/PremiumFortificationKit.blend')
out = os.path.abspath('TestResults/PremiumFortificationPreview.png')
bpy.ops.wm.open_mainfile(filepath=blend)
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=1200; scene.render.resolution_y=700; scene.render.resolution_percentage=100
if scene.world is None: scene.world=bpy.data.worlds.new('PreviewWorld')
scene.world.color=(0.035,0.045,0.065)
for o in list(scene.objects):
    if o.type in {'CAMERA','LIGHT'}: bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.camera_add(location=(15,11,18))
cam=bpy.context.object; scene.camera=cam
target=Vector((0,2.2,0)); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.lens=52
bpy.ops.object.light_add(type='AREA', location=(4,12,8)); key=bpy.context.object; key.data.energy=1800; key.data.shape='DISK'; key.data.size=8
key.rotation_euler=(Vector((0,2,0))-key.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='AREA', location=(-10,6,-8)); fill=bpy.context.object; fill.data.energy=900; fill.data.size=10; fill.rotation_euler=(Vector((0,2,0))-fill.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='AREA', location=(0,8,-6)); rim=bpy.context.object; rim.data.energy=1200; rim.data.size=6; rim.rotation_euler=(Vector((0,3,0))-rim.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=60, location=(0,-.05,0)); ground=bpy.context.object; ground.data.materials.append(bpy.data.materials.new('PreviewGround')); ground.data.materials[0].diffuse_color=(.025,.035,.045,1)
scene.render.filepath=out
os.makedirs(os.path.dirname(out),exist_ok=True)
bpy.ops.render.render(write_still=True)
print('PREVIEW_RENDERED',out)
