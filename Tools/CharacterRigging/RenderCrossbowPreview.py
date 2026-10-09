"""Renders de inspecao da Balestra no Blender (Workbench, texturas visiveis), a partir de Working/Crossbow/Crossbow.blend.
Uso: blender -b Working/Crossbow/Crossbow.blend --python RenderCrossbowPreview.py -- [--out pasta] [--shots nome,nome] [--human]
Cada shot = (nome, clipe, frame, camera). Cameras: 3q (frente-esquerda), side, front, back, top, close_head, close_winch.
"""
import bpy, sys, math
from pathlib import Path
from mathutils import Vector

ROOT = Path(bpy.data.filepath).resolve().parents[4]
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = ROOT / 'Docs/CharacterMeshAudit/Crossbow'
want = None
human = '--human' in args
lod = 0
for i, a in enumerate(args):
    if a == '--out':
        out = ROOT / args[i + 1]
    if a == '--shots':
        want = args[i + 1].split(',')
    if a == '--lod':
        lod = int(args[i + 1])
out.mkdir(parents=True, exist_ok=True)

SHOTS = [
    ('3q-loaded', 'Idle', 0, '3q'), ('side-loaded', 'Idle', 0, 'side'), ('front-loaded', 'Idle', 0, 'front'), ('back-loaded', 'Idle', 0, 'back'),
    ('top-loaded', 'Idle', 0, 'top'), ('close-head', 'Idle', 0, 'close_head'), ('close-rail', 'Idle', 0, 'close_rail'), ('close-winch', 'Idle', 0, 'close_winch'),
    ('fire-06', 'Fire', 6, '3q'), ('fire-08', 'Fire', 8, '3q'), ('fire-12', 'Fire', 12, '3q'), ('relaxed-top', 'Fire', 24, 'top'),
    ('reload-14', 'Reload', 14, '3q'), ('reload-30', 'Reload', 30, '3q'),
    ('move-30', 'Move', 30, '3q'), ('hit-03', 'Hit', 3, '3q'),
    ('destroy-00', 'Destroy', 0, '3q'), ('destroy-14', 'Destroy', 14, '3q'), ('destroy-30', 'Destroy', 30, '3q'), ('destroy-60', 'Destroy', 60, '3q'),
    ('destroy-60-top', 'Destroy', 60, 'top'), ('destroy-60-back', 'Destroy', 60, 'back'),
]

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.display.shading.show_shadows = True
sc.display.shading.show_cavity = False
sc.render.resolution_x, sc.render.resolution_y = 1280, 900
sc.render.film_transparent = False
sc.world = bpy.data.worlds.new('W') if sc.world is None else sc.world
sc.world.color = (0.36, 0.38, 0.41)
sc.render.image_settings.file_format = 'PNG'

rig = bpy.data.objects['CrossbowRig']
# LOD visibility
for o in bpy.data.objects:
    if o.type == 'MESH' and '_LOD' in o.name:
        o.hide_render = not o.name.endswith(f'_LOD{lod}')
        o.hide_viewport = o.hide_render
    if o.name == 'Crossbow_Bolt':
        o.hide_render = True

# chao + soldado de referencia
bpy.ops.mesh.primitive_plane_add(size=14, location=(0, 0, 0))
ground = bpy.context.active_object
ground.name = 'Ground'
if human:
    bpy.ops.mesh.primitive_cylinder_add(radius=0.22, depth=1.45, location=(2.2, -0.6, 0.725))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.12, location=(2.2, -0.6, 1.67))
    bpy.ops.mesh.primitive_cylinder_add(radius=0.28, depth=0.01, location=(2.2, -0.6, 0.005))

cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 38


def aim(loc, target):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()


def place(kind):
    cam.data.type = 'PERSP'
    if kind == '3q':
        aim((-3.3, -4.0, 2.2), (0, 0.0, 0.75))
    elif kind == 'side':
        aim((-5.6, 0.05, 0.9), (0, 0.05, 0.75))
        cam.data.lens = 34
    elif kind == 'front':
        aim((0, -5.8, 1.0), (0, 0, 0.75))
        cam.data.lens = 36
    elif kind == 'back':
        aim((2.6, 4.6, 2.0), (0, 0.1, 0.75))
    elif kind == 'top':
        aim((0, 0.05, 6.2), (0, 0.05, 0.5))
        cam.rotation_euler = (0, 0, 0)
        cam.data.lens = 30
    elif kind == 'close_head':
        aim((-1.6, -2.2, 1.9), (0, -0.7, 1.1))
    elif kind == 'close_rail':
        aim((0.35, 0.55, 1.75), (0, -0.75, 1.05))
    elif kind == 'close_winch':
        aim((1.2, 2.2, 2.0), (0, 0.55, 1.05))


for name, clip, frame, kind in SHOTS:
    if want and name not in want:
        continue
    act = bpy.data.actions[clip]
    rig.animation_data.action = act
    if act.slots:
        rig.animation_data.action_slot = act.slots[0]
    sc.frame_set(frame)
    bpy.context.view_layer.update()
    place(kind)
    sc.render.filepath = str(out / f'blender-{name}.png')
    bpy.ops.render.render(write_still=True)
print('RENDER_OK', lod)
