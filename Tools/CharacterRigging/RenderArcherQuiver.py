"""Check the quiver/baldric fit on the bind-pose Archer (back, side, front). Output: Docs/CharacterMeshAudit/Archer/quiver_fit_*.png"""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
ROOT = Path(bpy.data.filepath).parents[4]
acc = ROOT / "Tools/CharacterRigging/Working/Archer/Archer_Accessories.blend"
origin = json.loads((ROOT / "Docs/CharacterMeshAudit/Archer/accessories.json").read_text())["quiverOriginCharacterSpaceBlender"]
with bpy.data.libraries.load(str(acc)) as (src, dst):
    dst.objects = ["Archer_Quiver"]
q = dst.objects[0]
bpy.context.scene.collection.objects.link(q)
q.location = origin
rig = bpy.data.objects["Archer_Armature"]
for t in rig.animation_data.nla_tracks:
    t.mute = True
rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
bpy.context.view_layer.update()
for o in bpy.data.objects:
    if o.name in ("Archer_LOD1", "Archer_LOD2"):
        o.hide_render = True
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "MATERIAL"
sc.render.resolution_x, sc.render.resolution_y = 500, 700
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = 1.4
for name, loc, rot in (("back", (0, 6, 1.3), (math.pi / 2, 0, math.pi)), ("side", (6, 0, 1.3), (math.pi / 2, 0, math.pi / 2)), ("front", (0, -6, 1.3), (math.pi / 2, 0, 0))):
    cam.location, cam.rotation_euler = loc, rot
    sc.render.filepath = str(ROOT / f"Docs/CharacterMeshAudit/Archer/quiver_fit_{name}.png")
    bpy.ops.render.render(write_still=True)
print("QUIVER_RENDER_OK")
