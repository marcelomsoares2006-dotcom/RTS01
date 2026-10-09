"""Repara uma cópia da espada do Drive para uso estático em RTS.

Entrada: FBX original extraído em DriveImports/Extracted/WoodSword.
Saída: DriveImports/Repaired/WoodSword_Repaired.fbx e .glb.
O original nunca é alterado.
"""
from pathlib import Path
import bpy

ROOT = Path(__file__).resolve().parents[2]
INPUT = ROOT / "Tools/CharacterRigging/DriveImports/Extracted/WoodSword"
FBX = next(INPUT.rglob("*.fbx"))
OUT = ROOT / "Tools/CharacterRigging/DriveImports/Repaired"
OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FBX))
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not meshes:
    raise RuntimeError("A espada não contém malha após a importação FBX")

bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.object
obj.name = "WoodSword_Repaired"

bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.remove_doubles(threshold=0.0001)
bpy.ops.mesh.delete_loose(use_verts=True, use_edges=True, use_faces=True)
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.mesh.select_all(action="DESELECT")
bpy.ops.mesh.select_non_manifold()
bpy.ops.mesh.fill_holes(sides=32)
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode="OBJECT")

dec = obj.modifiers.new("RTS_optimized", "DECIMATE")
dec.ratio = 0.12
bpy.context.view_layer.objects.active = obj
bpy.ops.object.modifier_apply(modifier=dec.name)

bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.03)
bpy.ops.object.mode_set(mode="OBJECT")
for poly in obj.data.polygons:
    poly.use_smooth = False

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "WoodSword_Repaired.blend"))
bpy.ops.export_scene.fbx(filepath=str(OUT / "WoodSword_Repaired.fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL")
bpy.ops.wm.gltf_export(filepath=str(OUT / "WoodSword_Repaired.glb"), export_format="GLB")
print(f"REPAIRED_WOOD_SWORD: tris={len(obj.data.loop_triangles)} out={OUT}")
