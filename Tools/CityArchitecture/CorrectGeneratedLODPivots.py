"""Corrige LODs isolados no espaço da malha, preservando os pivôs dos objetos.

Uso em pasta de teste:
  $env:ERA_IMPERIAL_LOD_INPUT='TestResults/LOD-Generation-Round3'
  $env:ERA_IMPERIAL_LOD_OUTPUT='TestResults/LOD-Generation-Round4'
  blender.exe -b -P Tools/CityArchitecture/CorrectGeneratedLODPivots.py

O script nunca escreve em Assets/CityArchitecture/Generated/LOD.
"""
from __future__ import annotations

import os
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
INPUT = Path(os.environ.get("ERA_IMPERIAL_LOD_INPUT", "TestResults/LOD-Generation-Round3"))
OUTPUT = Path(os.environ.get("ERA_IMPERIAL_LOD_OUTPUT", "TestResults/LOD-Generation-Round4"))
if not INPUT.is_absolute():
    INPUT = ROOT / INPUT
if not OUTPUT.is_absolute():
    OUTPUT = ROOT / OUTPUT

BUILDINGS = ("House_Medieval", "Barracks_Medieval", "Temple_Imperial", "Market_Classical")
PROD = ROOT / "Assets" / "CityArchitecture" / "Generated"


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def import_fbx(path: Path):
    reset()
    bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=False)
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    lo = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    hi = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    return (lo + hi) * 0.5


def base_map(path: Path):
    return {obj.name: bounds(obj) for obj in import_fbx(path)}


def candidate_name(name: str) -> str:
    return name[:-4] if name.endswith("_LOD") else name


def export(path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = next((o for o in bpy.context.scene.objects if o.type == "EMPTY"), None)
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False, mesh_smooth_type="FACE", add_leaf_bones=False,
        bake_anim=False, path_mode="COPY", embed_textures=False,
    )
    bpy.ops.object.select_all(action="DESELECT")


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    corrected = 0
    for building in BUILDINGS:
        base = base_map(PROD / "Buildings" / f"{building}.fbx")
        for level in (1, 2):
            source = INPUT / f"{building}_LOD{level}.fbx"
            if not source.is_file():
                raise FileNotFoundError(source)
            candidate = import_fbx(source)
            for obj in candidate:
                target = base.get(candidate_name(obj.name))
                if target is None:
                    continue
                delta_world = target - bounds(obj)
                delta_local = obj.matrix_world.inverted().to_3x3() @ delta_world
                for vertex in obj.data.vertices:
                    vertex.co += delta_local
                obj.data.update()
                corrected += 1
            export(OUTPUT / source.name)
            print(f"LOD_VERTEX_ALIGNMENT {source.name} fragments={len(candidate)}")
    print(f"LOD_VERTEX_ALIGNMENT_COMPLETE corrected_fragments={corrected} output={OUTPUT}")


if __name__ == "__main__":
    main()
