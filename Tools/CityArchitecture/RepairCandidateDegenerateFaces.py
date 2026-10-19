"""Remove faces degeneradas somente de uma cópia candidata de LOD2.

Não altera Assets/ nem promove o resultado. A geometria é reexportada para
uma pasta TestResults e deve passar novamente pelo auditor antes de qualquer
decisão de produção.
"""
from __future__ import annotations

import os
from pathlib import Path

import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / os.environ.get("ERA_IMPERIAL_LOD_INPUT", "TestResults/LOD-Generation-Round7")
OUTPUT = ROOT / os.environ.get("ERA_IMPERIAL_LOD_OUTPUT", "TestResults/LOD-Generation-Round8")
BUILDINGS = ("House_Medieval", "Barracks_Medieval", "Market_Classical", "Temple_Imperial")
AREA_EPSILON = 1e-10


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def export(path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [o for o in bpy.context.scene.objects if o.type in {"MESH", "EMPTY"}]
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        path_mode="COPY", embed_textures=False)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    total = 0
    for building in BUILDINGS:
        source = SOURCE / f"{building}_LOD2.fbx"
        if not source.is_file():
            raise FileNotFoundError(source)
        reset()
        bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=False)
        removed = 0
        for obj in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            mesh = obj.data
            bm = bmesh.new()
            bm.from_mesh(mesh)
            bad = [face for face in bm.faces if face.calc_area() <= AREA_EPSILON]
            if bad:
                bmesh.ops.delete(bm, geom=bad, context="FACES")
                bm.to_mesh(mesh)
                mesh.update()
                removed += len(bad)
            bm.free()
        out = OUTPUT / source.name
        export(out)
        total += removed
        print(f"LOD2_DEGENERATE_REPAIR building={building} removed={removed} output={out}")
    print(f"LOD2_DEGENERATE_REPAIR_COMPLETE removed={total} output={OUTPUT}")


if __name__ == "__main__":
    main()
