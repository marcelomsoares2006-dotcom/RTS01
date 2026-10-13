"""Generate an isolated 2K AAA-style concept preview, not a production Unity asset."""
import bpy, math, os
from mathutils import Vector

OUT = os.environ.get('ERA_AAA_PREVIEW_OUT', os.path.join(os.getcwd(), 'Docs', 'AAA', 'ScenarioAudit', 'preview'))
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(name, color, rough=0.8, metallic=0.0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Roughness'].default_value=rough; bs.inputs['Metallic'].default_value=metallic
    return m

grass=mat('AAA_Grass',(0.12,0.26,0.10)); dirt=mat('AAA_Dirt',(0.25,0.12,0.05)); stone=mat('AAA_Stone',(0.28,0.30,0.32),.65); wood=mat('AAA_Wood',(0.18,0.07,0.025)); water=mat('AAA_Water',(0.03,0.16,0.28),.18)

bpy.ops.mesh.primitive_plane_add(size=40, location=(0,0,0)); terrain=bpy.context.object; terrain.name='AAA_Preview_Terrain'; terrain.data.materials.append(grass)
for x,y,s in [(-5,3,3),(4,-2,2.5),(6,5,2)]:
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=s, depth=0.35, location=(x,y,.18)); bpy.context.object.data.materials.append(stone)
bpy.ops.mesh.primitive_cube_add(location=(0,1,0.8), scale=(3,2,.8)); fort=bpy.context.object; fort.name='AAA_Preview_Fort'; fort.data.materials.append(stone)
for x in (-2.5,2.5):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=.45, depth=3.0, location=(x,1,2)); bpy.context.object.data.materials.append(stone)
bpy.ops.mesh.primitive_cube_add(location=(0,-1.1,1.5), scale=(1.2,.15,.9)); bpy.context.object.data.materials.append(wood)
bpy.ops.mesh.primitive_plane_add(size=12, location=(-5,-5,.03)); bpy.context.object.data.materials.append(water)

bpy.ops.object.light_add(type='AREA', location=(2,-4,10)); key=bpy.context.object; key.data.energy=1800; key.data.shape='DISK'; key.data.size=8; key.rotation_euler=(math.radians(25),0,math.radians(25))
bpy.ops.object.light_add(type='AREA', location=(-8,2,5)); bpy.context.object.data.energy=700; bpy.context.object.data.size=6
bpy.ops.object.camera_add(location=(16,-20,14)); cam=bpy.context.object; bpy.context.scene.camera=cam
def track(obj, target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
track(cam,(0,0,0)); cam.data.lens=48
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE' if 'BLENDER_EEVEE' in {item.identifier for item in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items} else 'BLENDER_EEVEE_NEXT'
scene.render.resolution_x=2560; scene.render.resolution_y=1440; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.filepath=os.path.join(OUT,'era_imperial_aaa_concept_2560x1440.png')
if scene.world is None: scene.world=bpy.data.worlds.new('AAA_Preview_World')
scene.world.color=(0.04,0.07,0.12); bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'era_imperial_aaa_concept.blend')); bpy.ops.render.render(write_still=True)
print(scene.render.filepath)
