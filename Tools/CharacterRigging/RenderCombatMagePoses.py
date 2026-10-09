"""Renderiza poses dos clipes retargetados de CombatMage.blend (Workbench) e monta folhas de contato.
Uso: blender -b Tools/CharacterRigging/Working/CombatMage/CombatMage.blend --python Tools/CharacterRigging/RenderCombatMagePoses.py -- <saida> [Clip:f1,f2,...] ...
Sem pares Clip:frames usa o conjunto padrao. Gera <saida>/poses_<Clip>_<frame>_<front|side>.png (folha montada por MontagePoses.py).
"""
import sys, math
from pathlib import Path
import bpy
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = Path(args[0]).resolve() if args else Path(bpy.data.filepath).parents[3] / "Working_poses"
OUT.mkdir(parents=True, exist_ok=True)
shots = {}
for a in args[1:]:
    c, fr = a.split(":")
    shots[c] = [int(x) for x in fr.split(",")]
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_specular_highlight = False
world = bpy.data.worlds.new("W") if not sc.world else sc.world
sc.world = world
world.color = (0.62, 0.64, 0.66)
sc.view_settings.view_transform = "Standard"
sc.render.film_transparent = False
sc.render.resolution_x, sc.render.resolution_y = 360, 520
rig = bpy.data.objects["CombatMage_Armature"]
for o in bpy.data.objects:
    if o.name in ("CombatMage_LOD1", "CombatMage_LOD2"):
        o.hide_render = o.hide_viewport = True
for t in rig.animation_data.nla_tracks:
    t.mute = True
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = 2.6
for clip, frames in shots.items():
    act = bpy.data.actions[clip]
    rig.animation_data.action = act
    rig.animation_data.action_slot = act.slots[0]
    for f in frames:
        sc.frame_set(f)
        for view, loc, rot in (("front", (0, -6, 0.95), (math.pi / 2, 0, 0)), ("side", (-6, 0, 0.95), (math.pi / 2, 0, -math.pi / 2))):
            cam.location, cam.rotation_euler = loc, rot
            sc.render.filepath = str(OUT / f"poses_{clip}_{f:03d}_{view}.png")
            bpy.ops.render.render(write_still=True)
print("RENDER_DONE")
