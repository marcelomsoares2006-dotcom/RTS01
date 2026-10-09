"""Renders de conferencia da catapulta (Workbench, com textura). Uso:
blender -b Tools/CharacterRigging/Working/Catapult/Catapult.blend --python Tools/CharacterRigging/RenderCatapult.py -- [saida] [vistas|poses|lods]
Gera PNGs em Docs/CharacterMeshAudit/Catapult/blender-*.png (ou na pasta indicada)."""
import sys
from pathlib import Path
import bpy
import math
from mathutils import Vector

ROOT = Path(bpy.data.filepath).resolve().parents[4]
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = ROOT / args[0] if args else ROOT / 'Docs/CharacterMeshAudit/Catapult'
MODE = args[1] if len(args) > 1 else 'vistas'
OUT.mkdir(parents=True, exist_ok=True)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'
sh.color_type = 'TEXTURE'
sh.show_shadows = True
sh.show_cavity = False
sc.render.resolution_x, sc.render.resolution_y = 960, 720
sc.render.film_transparent = False
sc.world = bpy.data.worlds.new('W') if sc.world is None else sc.world
sc.world.color = (0.36, 0.40, 0.44)
cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 38
rig = bpy.data.objects['CatapultArmature']


def show_lod(n):
    for i in range(3):
        o = bpy.data.objects[f'Catapult_LOD{i}']
        o.hide_render = i != n
        o.hide_viewport = i != n


def aim(pos, target=(0, 0, 1.15)):
    cam.location = Vector(pos)
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def shot(name):
    sc.render.filepath = str(OUT / f'{name}.png')
    bpy.ops.render.render(write_still=True)


def set_pose(clip, frame):
    rig.animation_data_create()
    act = bpy.data.actions[clip]
    rig.animation_data.action = act
    if act.slots:
        rig.animation_data.action_slot = act.slots[0]
    sc.frame_set(frame)


VIEWS = {
    'front': (0, -7.2, 2.0),
    'back': (0, 7.2, 2.0),
    'side': (7.4, 0, 1.6),
    'q-front': (5.2, -5.6, 3.0),
    'q-rear': (-5.4, 5.6, 3.0),
}

if MODE == 'vistas':
    show_lod(0)
    rig.animation_data_create()
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    for k, p in VIEWS.items():
        aim(p)
        shot(f'blender-{k}')
elif MODE == 'lods':
    for n in range(3):
        show_lod(n)
        aim(VIEWS['q-front'])
        shot(f'blender-lod{n}')
elif MODE == 'poses':
    show_lod(0)
    poses = [('Fire', 1), ('Fire', 7), ('Fire', 12), ('Fire', 13), ('Fire', 16), ('Reload', 1), ('Reload', 14), ('Reload', 40), ('Reload', 66),
             ('Hit', 3), ('Destroy', 10), ('Destroy', 28), ('Destroy', 50), ('Destroy', 72), ('Move', 8)]
    for clip, f in poses:
        set_pose(clip, f)
        aim(VIEWS['side'] if clip in ('Fire', 'Reload') else VIEWS['q-front'])
        shot(f'blender-pose-{clip}-{f:03d}')
