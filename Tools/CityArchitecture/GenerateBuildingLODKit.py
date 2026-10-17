"""Exporta LOD1/LOD2 não destrutivos a partir de EraBuildingKit.blend.

Uso:
    blender.exe -b -P Tools/CityArchitecture/GenerateBuildingLODKit.py

O script não altera o .blend de origem nem os FBX existentes. Cada prédio é
duplicado em uma cena temporária, recebe Decimate e é exportado para
Assets/CityArchitecture/Generated/LOD/.
"""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets" / "CityArchitecture" / "Generated" / "EraBuildingKit.blend"
_output_value = os.environ.get("ERA_IMPERIAL_LOD_OUTPUT", "Assets/CityArchitecture/Generated/LOD")
OUTPUT = Path(_output_value)
if not OUTPUT.is_absolute():
    OUTPUT = ROOT / OUTPUT

BUILDINGS = {
    "House_Medieval": (0.55, 0.30),
    "Barracks_Medieval": (0.55, 0.30),
    "Temple_Imperial": (0.60, 0.35),
    "Market_Classical": (0.55, 0.30),
}


def log(message: str) -> None:
    print(f"[GenerateBuildingLODKit] {message}")


def safe_name(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9_]+", "_", value).strip("_")


def object_roots(name: str):
    """Find a source root by exact/prefix name, tolerating Blender suffixes."""
    candidates = []
    for obj in bpy.data.objects:
        if obj.parent is not None:
            continue
        if obj.name == name or obj.name.startswith(name + ".") or obj.name.startswith(name + "_"):
            candidates.append(obj)
    if not candidates:
        for obj in bpy.data.objects:
            if obj.name == name or obj.name.startswith(name + ".") or obj.name.startswith(name + "_"):
                candidates.append(obj)
    return candidates[:1]


def descendants(root):
    result = [root]
    result.extend(child for child in root.children_recursive)
    return [obj for obj in result if obj.type == "MESH"]


def duplicate_hierarchy(root, collection):
    mapping = {}
    source_objects = [root] + list(root.children_recursive)
    for source in source_objects:
        copy = source.copy()
        if source.data is not None:
            copy.data = source.data.copy()
        copy.name = f"{source.name}_LOD"
        collection.objects.link(copy)
        mapping[source] = copy
    for source, copy in mapping.items():
        if source.parent in mapping:
            copy.parent = mapping[source.parent]
            copy.matrix_parent_inverse = source.matrix_parent_inverse.copy()
    clone_root = mapping[root]
    clone_objects = list(mapping.values())

    # O kit-fonte mantém quatro prédios afastados do ponto de origem comum.
    # Criamos uma raiz neutra por prédio e centralizamos a geometria no piso,
    # sem alterar o .blend de origem. Isso mantém LOD0/LOD1/LOD2 alinhados.
    bounds_min = Vector((float("inf"), float("inf"), float("inf")))
    bounds_max = Vector((float("-inf"), float("-inf"), float("-inf")))
    has_mesh = False
    for obj in clone_objects:
        if obj.type != "MESH" or obj.data is None:
            continue
        has_mesh = True
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            bounds_min.x = min(bounds_min.x, point.x)
            bounds_min.y = min(bounds_min.y, point.y)
            bounds_min.z = min(bounds_min.z, point.z)
            bounds_max.x = max(bounds_max.x, point.x)
            bounds_max.y = max(bounds_max.y, point.y)
            bounds_max.z = max(bounds_max.z, point.z)
    if not has_mesh:
        raise RuntimeError(f"Nenhuma malha encontrada para {root.name}")

    visual_center = Vector(((bounds_min.x + bounds_max.x) * 0.5, bounds_min.y, (bounds_min.z + bounds_max.z) * 0.5))
    export_root = bpy.data.objects.new(f"{root.name}_ExportRoot", None)
    collection.objects.link(export_root)
    root_world = clone_root.matrix_world.copy()
    clone_root.parent = export_root
    clone_root.matrix_world = root_world
    clone_root.matrix_world.translation -= visual_center
    return export_root, clone_objects


def add_decimate(mesh_objects, ratio: float):
    for obj in mesh_objects:
        if obj.type != "MESH" or obj.data is None:
            continue
        modifier = obj.modifiers.new(name="LOD_Decimate", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        try:
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        finally:
            obj.select_set(False)


def export_fbx(root, path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    selected = [root] + list(root.children_recursive)
    for obj in selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=False,
    )
    bpy.ops.object.select_all(action="DESELECT")


def main() -> int:
    if not SOURCE.is_file():
        log(f"ERRO: arquivo-fonte não encontrado: {SOURCE}")
        return 2
    OUTPUT.mkdir(parents=True, exist_ok=True)
    # O script abre explicitamente a fonte, inclusive quando chamado apenas
    # com `blender -b -P`. Nunca salva sobre ela.
    if not bpy.data.filepath or Path(bpy.data.filepath).resolve() != SOURCE.resolve():
        log(f"abrindo fonte: {SOURCE}")
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    if Path(bpy.data.filepath).resolve() != SOURCE.resolve():
        log(f"ERRO: arquivo aberto não é o esperado: {bpy.data.filepath}")
        return 3

    exported = 0
    for building, (lod1_ratio, lod2_ratio) in BUILDINGS.items():
        roots = object_roots(building)
        if not roots:
            log(f"AVISO: prédio não encontrado, ignorado: {building}")
            continue
        for lod_name, ratio in (("LOD1", lod1_ratio), ("LOD2", lod2_ratio)):
            temp_collection = bpy.data.collections.new(f"__TEMP_{building}_{lod_name}")
            bpy.context.scene.collection.children.link(temp_collection)
            clone_root, clone_objects = duplicate_hierarchy(roots[0], temp_collection)
            add_decimate([obj for obj in clone_objects if obj.type == "MESH"], ratio)
            output_path = OUTPUT / f"{safe_name(building)}_{lod_name}.fbx"
            export_fbx(clone_root, output_path)
            exported += 1
            for obj in list(temp_collection.objects):
                bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.collections.remove(temp_collection)
            log(f"exportado: {output_path.name} (ratio={ratio:.2f})")

    log(f"LOD_BUILDING_EXPORT_COMPLETE count={exported} output={OUTPUT}")
    return 0 if exported == len(BUILDINGS) * 2 else 1


if __name__ == "__main__":
    sys.exit(main())
