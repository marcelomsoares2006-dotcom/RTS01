import bpy, os
from mathutils import Vector
blend=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit_Detailed.blend'); out=os.path.abspath('TestResults/EraBuildingKitDetailedPreview_Framed.png')
bpy.ops.wm.open_mainfile(filepath=blend); scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE'; scene.render.resolution_x=1400; scene.render.resolution_y=800; scene.render.resolution_percentage=100
if scene.world is None: scene.world=bpy.data.worlds.new('DetailedPreviewWorld')
scene.world.color=(.02,.03,.045)
mesh_objects=[o for o in scene.objects if o.type=='MESH']
points=[o.matrix_world @ Vector(c) for o in mesh_objects for c in o.bound_box]
min_x,max_x=min(p.x for p in points),max(p.x for p in points)
min_y,max_y=min(p.y for p in points),max(p.y for p in points)
min_z,max_z=min(p.z for p in points),max(p.z for p in points)
center=Vector(((min_x+max_x)*.5,(min_y+max_y)*.5,(min_z+max_z)*.5))
span=max(max_x-min_x,max_y-min_y,max_z-min_z)
bpy.ops.object.camera_add(location=center+Vector((span*1.35,span*1.15,span*1.55))); cam=bpy.context.object; scene.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=span*2.05; cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
for loc,energy,size in [(center+Vector((span*.65,span*.9,span*.55)),1900,10),(center+Vector((-span*.8,span*.6,span*.1)),1000,12),(center+Vector((0,span*.55,-span*.8)),1300,8)]:
    bpy.ops.object.light_add(type='AREA',location=loc); l=bpy.context.object; l.data.energy=energy*2.5; l.data.size=size; l.rotation_euler=(center-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=span*3,location=(center.x,min_y-.05,center.z),rotation=(1.5708,0,0)); p=bpy.context.object; m=bpy.data.materials.new('PreviewGround'); m.diffuse_color=(.02,.03,.04,1); p.data.materials.append(m)
scene.render.filepath=out; os.makedirs(os.path.dirname(out),exist_ok=True); bpy.ops.render.render(write_still=True); print('DETAILED_PREVIEW_RENDERED',out)
