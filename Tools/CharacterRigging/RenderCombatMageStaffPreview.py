"""Previa em Blender do cajado na mao direita (mesma regra do Unity: vertical no frame 1 do Cast, punho = cabeca do osso da mao - 7 cm).
Uso: blender -b Working/CombatMage/CombatMage.blend --python RenderCombatMageStaffPreview.py -- <pasta-saida-absoluta> <Clip:f1,f2> ..."""
import sys, math
from pathlib import Path
import bpy
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--") + 1:]
OUT = Path(args[0]); OUT.mkdir(parents=True, exist_ok=True)
shots = {a.split(":")[0]: [int(x) for x in a.split(":")[1].split(",")] for a in args[1:]}
root = Path(bpy.data.filepath).resolve().parents[0]
acc = root / "CombatMage_Accessories.blend"
with bpy.data.libraries.load(str(acc)) as (src, dst):
    dst.objects = [n for n in src.objects]
sc = bpy.context.scene
for o in dst.objects:
    sc.collection.objects.link(o)
staff = bpy.data.objects["CombatMage_Staff"]
rig = bpy.data.objects["CombatMage_Armature"]
cast = bpy.data.actions["Cast"]
rig.animation_data.action = cast
rig.animation_data.action_slot = cast.slots[0]
for t in rig.animation_data.nla_tracks:
    t.mute = True
sc.frame_set(1)
hand = rig.pose.bones["RightHand"]
grip = rig.matrix_world @ hand.head + Vector((0, 0, -0.07))
staff.parent = rig
staff.parent_type = "BONE"
staff.parent_bone = "RightHand"
staff.matrix_world = Matrix.Translation(grip)  # upright, identity rotation
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "MATERIAL"
sc.display.shading.show_specular_highlight = False
world = bpy.data.worlds.new("W"); sc.world = world; world.color = (0.62, 0.64, 0.66)
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = 360, 560
for o in bpy.data.objects:
    if o.name in ("CombatMage_LOD1", "CombatMage_LOD2"):
        o.hide_render = o.hide_viewport = True
body = bpy.data.objects["CombatMage_LOD0"]
sc.display.shading.color_type = "TEXTURE"
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = 2.7
for clip, frames in shots.items():
    act = bpy.data.actions[clip]
    rig.animation_data.action = act
    rig.animation_data.action_slot = act.slots[0]
    for f in frames:
        sc.frame_set(f)
        for view, loc, rot in (("front", (0, -6, 1.0), (math.pi / 2, 0, 0)), ("side", (-6, 0, 1.0), (math.pi / 2, 0, -math.pi / 2)), ("back", (0, 6, 1.0), (math.pi / 2, 0, math.pi))):
            cam.location, cam.rotation_euler = loc, rot
            sc.render.filepath = str(OUT / f"staff_{clip}_{f:03d}_{view}.png")
            bpy.ops.render.render(write_still=True)
print("RENDER_DONE")
