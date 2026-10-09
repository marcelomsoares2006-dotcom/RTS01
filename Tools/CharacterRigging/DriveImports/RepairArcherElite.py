import bpy
from pathlib import Path

src = Path(__file__).with_name("ArcherElite.glb")
dst = Path(__file__).with_name("ArcherElite_Repaired.glb")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))

body = bpy.data.objects.get("Archer")
if body is None or body.type != "MESH":
    raise RuntimeError("Archer mesh not found")

# The imported Icosphere is a helper object, not part of the character asset.
for obj in list(bpy.data.objects):
    if obj.type == "MESH" and obj.name != body.name:
        bpy.data.objects.remove(obj, do_unlink=True)
for armature in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
    for pose_bone in armature.pose.bones:
        pose_bone.custom_shape = None

# Generate a UV layer while preserving material, armature modifier and weights.
bpy.context.view_layer.objects.active = body
body.select_set(True)
for obj in bpy.context.selected_objects:
    if obj != body:
        obj.select_set(False)
if not body.data.uv_layers:
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    body.data.uv_layers[0].name = "UVMap"

bpy.ops.wm.save_as_mainfile(filepath=str(dst.with_suffix(".blend")))
bpy.ops.wm.open_mainfile(filepath=str(dst.with_suffix(".blend")))
body = bpy.data.objects["Archer"]
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
for obj in bpy.data.objects:
    if obj.type == "ARMATURE":
        obj.select_set(True)
bpy.ops.export_scene.gltf(
    filepath=str(dst),
    export_format="GLB",
    use_selection=True,
    export_apply=False,
    export_materials="EXPORT",
)
print(f"WROTE {dst}")
