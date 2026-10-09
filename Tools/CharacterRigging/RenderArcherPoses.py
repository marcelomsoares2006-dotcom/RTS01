"""Render retargeted poses of Archer.blend (Workbench) to Docs/CharacterMeshAudit/Archer/poses_*.png."""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT = Path(bpy.data.filepath).parents[4]
OUT = ROOT / "Docs/CharacterMeshAudit/Archer"
OUT.mkdir(parents=True, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 420, 620
rig = bpy.data.objects["Archer_Armature"]
for o in bpy.data.objects:
    if o.name in ("Archer_LOD1", "Archer_LOD2"):
        o.hide_render = True
        o.hide_viewport = True
rig.animation_data.nla_tracks.foreach_set if False else None
for t in rig.animation_data.nla_tracks:
    t.mute = True
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = 2.4
SHOTS = [("Idle", [1, 120]), ("Aim", [1, 60]), ("DrawString", [1, 25, 45, 70, 97]), ("Fire", [1, 12, 25, 43]),
         ("Walk", [1, 8, 16]), ("Run", [1, 8])]
import math
for clip, frames in SHOTS:
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
