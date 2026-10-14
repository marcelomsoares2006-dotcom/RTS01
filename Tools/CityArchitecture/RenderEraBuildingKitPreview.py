import bpy, os
from mathutils import Vector
blend=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit.blend'); out=os.path.abspath('TestResults/EraBuildingKitPreview.png')
bpy.ops.wm.open_mainfile(filepath=blend); scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'; scene.render.resolution_x=1400; scene.render.resolution_y=800; scene.render.resolution_percentage=100
if scene.world is None: scene.world=bpy.data.worlds.new('BuildingPreviewWorld')
scene.world.color=(.025,.035,.05)
bpy.ops.object.camera_add(location=(58,48,76)); cam=bpy.context.object; scene.camera=cam; cam.data.lens=58; cam.rotation_euler=(Vector((0,2,8))-cam.location).to_track_quat('-Z','Y').to_euler()
for loc,energy,size in [((8,24,14),1900,10),((-20,15,2),1000,12),((0,18,-20),1300,8)]:
    bpy.ops.object.light_add(type='AREA',location=loc); l=bpy.context.object; l.data.energy=energy; l.data.size=size; l.rotation_euler=(Vector((0,2,8))-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=80,location=(0,-.1,8)); p=bpy.context.object; m=bpy.data.materials.new('PreviewGround'); m.diffuse_color=(.025,.035,.045,1); p.data.materials.append(m)
scene.render.filepath=out; os.makedirs(os.path.dirname(out),exist_ok=True); bpy.ops.render.render(write_still=True); print('BUILDING_PREVIEW_RENDERED',out)
