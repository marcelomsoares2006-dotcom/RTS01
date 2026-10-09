"""Renderiza poses do PaladinHealer.blend (Workbench) e monta folhas de contato em Docs/CharacterMeshAudit/PaladinHealer.
Uso: blender -b Tools/CharacterRigging/Working/PaladinHealer/PaladinHealer.blend --python Tools/CharacterRigging/RenderPaladinHealerPoses.py -- [sheet:Clip:f1,f2,...]...
Sem argumentos usa as folhas padrao. Cada quadro gera frente e lado (colunas); cada linha e um quadro."""
import math
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(bpy.data.filepath).parents[4]
OUT = ROOT / "Docs/CharacterMeshAudit/PaladinHealer"
TMP = ROOT / "Tools/CharacterRigging/Working/PaladinHealer/render"
OUT.mkdir(parents=True, exist_ok=True)
TMP.mkdir(parents=True, exist_ok=True)

DEFAULT = [
    ("poses_Idle", "Idle", [1, 40]),
    ("poses_Attack", "Attack", [1, 10, 18, 26, 36, 46]),
    ("poses_Heal", "Heal", [1, 8, 14, 22, 30, 38]),
    ("poses_Hit", "Hit", [1, 8, 16, 32]),
    ("poses_Death", "Death", [1, 30, 60, 90, 130, 174]),
    ("poses_Walk", "Walk", [1, 8, 16]),
    ("poses_Run", "Run", [1, 6, 11]),
]
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
shots = []
for a in args:
    name, clip, frames = a.split(":")
    shots.append((name, clip, [int(x) for x in frames.split(",")]))
shots = shots or DEFAULT

sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 360, 520
sc.render.image_settings.file_format = "PNG"
sc.render.film_transparent = False
world = bpy.data.worlds.new("W")
world.color = (0.34, 0.36, 0.39)
sc.world = world
sc.display.shading.background_type = "WORLD"
rig = bpy.data.objects["PaladinHealer_Armature"]
for o in bpy.data.objects:
    if o.name in ("PaladinHealer_LOD1", "PaladinHealer_LOD2"):
        o.hide_render = True
        o.hide_viewport = True
if rig.animation_data:
    for t in rig.animation_data.nla_tracks:
        t.mute = True
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = 2.5


def load(path):
    img = bpy.data.images.load(str(path))
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)


def save(array, path):
    h, w = array.shape[:2]
    img = bpy.data.images.new("sheet", w, h, alpha=True)
    img.pixels.foreach_set(array.ravel())
    img.filepath_raw = str(path)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


for sheet, clip, frames in shots:
    act = bpy.data.actions[clip]
    rig.animation_data.action = act
    rig.animation_data.action_slot = act.slots[0]
    rows = []
    for f in frames:
        sc.frame_set(f)
        tiles = []
        for view, loc, rot in (("front", (0, -6, 0.95), (math.pi / 2, 0, 0)), ("side", (-6, 0, 0.95), (math.pi / 2, 0, -math.pi / 2))):
            cam.location, cam.rotation_euler = loc, rot
            p = TMP / f"{sheet}_{f:03d}_{view}.png"
            sc.render.filepath = str(p)
            bpy.ops.render.render(write_still=True)
            tiles.append(load(p))
        rows.append(np.concatenate(tiles, axis=1))
    per_col = 3
    while len(rows) % per_col:
        rows.append(np.ones_like(rows[0]))
    columns = [np.concatenate(rows[i:i + per_col][::-1], axis=0) for i in range(0, len(rows), per_col)]
    grid = np.concatenate(columns, axis=1)
    save(grid, OUT / f"{sheet}.png")
    print("SHEET", sheet, frames)
print("RENDER_DONE")
