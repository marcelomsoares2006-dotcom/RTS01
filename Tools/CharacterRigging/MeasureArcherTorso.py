"""Radial torso profile of the Archer bind mesh (character space, Blender axes: +Z up, front = -Y,
character right = -X) -> Working/Archer/torso.json. Only vertices dominated by torso bones are used, so arms,
hands and legs never contaminate the profile.
Run: blender -b Tools/CharacterRigging/Working/Archer/Archer.blend --python Tools/CharacterRigging/MeasureArcherTorso.py"""
import bpy, json, math
from pathlib import Path
TORSO = {"Hips", "Spine02", "Spine01", "Spine", "neck", "LeftShoulder", "RightShoulder"}
body = bpy.data.objects["Archer_LOD0"]
rig = bpy.data.objects["Archer_Armature"]
rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.location, pb.rotation_quaternion = (0, 0, 0), (1, 0, 0, 0)
bpy.context.view_layer.update()
names = {i: g.name for i, g in enumerate(body.vertex_groups)}
pts = []
for v in body.data.vertices:
    if not v.groups:
        continue
    top = max(v.groups, key=lambda g: g.weight)
    if names[top.group] in TORSO:
        pts.append(body.matrix_world @ v.co)
BINS = 36
out = {"levels": {}, "torsoVertices": len(pts)}
z = 0.90
while z <= 1.62:
    sl = [p for p in pts if abs(p.z - z) < 0.02]
    if len(sl) > 20:
        cx = (min(p.x for p in sl) + max(p.x for p in sl)) / 2
        cy = (min(p.y for p in sl) + max(p.y for p in sl)) / 2
        radii = [0.0] * BINS
        for p in sl:
            a = math.atan2(p.y - cy, p.x - cx) % (2 * math.pi)
            b = int(a / (2 * math.pi) * BINS) % BINS
            radii[b] = max(radii[b], math.hypot(p.x - cx, p.y - cy))
        # fill empty bins from neighbours
        for _ in range(BINS):
            for i in range(BINS):
                if radii[i] == 0 and (radii[(i - 1) % BINS] or radii[(i + 1) % BINS]):
                    radii[i] = max(radii[(i - 1) % BINS], radii[(i + 1) % BINS])
        out["levels"][f"{z:.3f}"] = {"cx": round(cx, 4), "cy": round(cy, 4), "radii": [round(r, 4) for r in radii]}
    z += 0.02
out["bones"] = {}
for b in ("Hips", "Spine02", "Spine01", "Spine", "neck", "Head", "LeftArm", "RightArm", "LeftHand", "RightHand"):
    h = rig.matrix_world @ rig.data.bones[b].head_local
    out["bones"][b] = [round(h.x, 4), round(h.y, 4), round(h.z, 4)]
Path(bpy.data.filepath).with_name("torso.json").write_text(json.dumps(out, indent=1))
print("TORSO_OK", len(pts))
