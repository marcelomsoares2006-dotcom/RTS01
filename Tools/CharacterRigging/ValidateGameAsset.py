"""Validate Blender-imported game assets before the Blender -> FBX -> Unity step.

Run with Blender's Python, for example:
  blender -b --python ValidateGameAsset.py -- <asset.fbx> <report.json>

The script never writes to the asset. It reports geometry, UV, skinning,
materials, scale and bounds so bad imports are rejected before Unity.
"""
from __future__ import annotations

import bpy
import json
import math
import sys
from pathlib import Path


def args_after_double_dash():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args:
        raise SystemExit("usage: blender -b --python ValidateGameAsset.py -- asset.fbx report.json")
    return Path(args[0]), Path(args[1]) if len(args) > 1 else Path("asset-validation.json")


def finite_vec(v):
    return all(math.isfinite(float(x)) for x in v)


def validate(asset_path: Path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    suffix = asset_path.suffix.lower()
    if suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(asset_path))
    elif suffix == ".glb" or suffix == ".gltf":
        bpy.ops.import_scene.gltf(filepath=str(asset_path))
    elif suffix == ".obj":
        bpy.ops.wm.obj_import(filepath=str(asset_path))
    elif suffix == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(asset_path))
    else:
        raise SystemExit(f"unsupported asset format: {asset_path.suffix}")

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    armatures = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    errors, warnings = [], []
    mesh_results = []
    for obj in meshes:
        mesh = obj.data
        mesh.calc_loop_triangles()
        tri_count = len(mesh.loop_triangles)
        uv_layers = len(mesh.uv_layers)
        if uv_layers == 0:
            errors.append(f"{obj.name}: missing UV layer")
        else:
            for uv_index, loop_uv in enumerate(mesh.uv_layers[0].data):
                if not (0.0 <= loop_uv.uv.x <= 1.0 and 0.0 <= loop_uv.uv.y <= 1.0):
                    warnings.append(f"{obj.name}: UV outside 0..1 at loop {uv_index}")
                    break
        if not mesh.materials or any(material is None for material in mesh.materials):
            errors.append(f"{obj.name}: missing material slot")
        edge_use = {}
        for poly in mesh.polygons:
            for a, b in zip(poly.vertices, poly.vertices[1:] + poly.vertices[:1]):
                edge = tuple(sorted((a, b)))
                edge_use[edge] = edge_use.get(edge, 0) + 1
            if not poly.use_smooth and poly.area > 1e-10:
                warnings.append(f"{obj.name}: flat-shaded face {poly.index}")
        non_manifold = sum(1 for uses in edge_use.values() if uses > 2)
        if non_manifold:
            errors.append(f"{obj.name}: {non_manifold} non-manifold edges")
        open_edges = sum(1 for uses in edge_use.values() if uses == 1)
        if open_edges:
            warnings.append(f"{obj.name}: {open_edges} open boundary edges")
        for poly in mesh.polygons:
            if len(poly.vertices) < 3 or poly.area <= 1e-10:
                errors.append(f"{obj.name}: degenerate face {poly.index}")
        for vertex in mesh.vertices:
            if not finite_vec(vertex.co):
                errors.append(f"{obj.name}: non-finite vertex {vertex.index}")
        if mesh.validate(verbose=False, clean_customdata=False):
            warnings.append(f"{obj.name}: Blender mesh validation found repairable data")
        armature = obj.find_armature()
        should_be_skinned = bool(armatures) and ("static" not in obj.name.lower())
        if should_be_skinned and armature is None:
            errors.append(f"{obj.name}: armature reference missing for skinned asset")
        if armature:
            bone_names = {bone.name for bone in armature.data.bones}
            unknown_groups = sorted({group.name for group in obj.vertex_groups
                                     if group.name not in bone_names})
            if unknown_groups:
                errors.append(f"{obj.name}: vertex groups without bones: {unknown_groups}")
            for vertex in mesh.vertices:
                weights = [g.weight for g in vertex.groups if g.weight > 1e-6]
                if not weights:
                    errors.append(f"{obj.name}: unweighted vertex {vertex.index}")
                elif len(weights) > 4:
                    errors.append(f"{obj.name}: vertex {vertex.index} has >4 influences")
                elif abs(sum(weights) - 1.0) > 0.02:
                    errors.append(f"{obj.name}: vertex {vertex.index} weights do not sum to 1")
        bounds = [obj.matrix_world @ v.co for v in mesh.vertices]
        if bounds:
            height = max(v.y for v in bounds) - min(v.y for v in bounds)
            if height <= 0.001 or height > 20:
                warnings.append(f"{obj.name}: unusual world height {height:.4f}m")
        mesh_results.append({
            "name": obj.name,
            "triangles": tri_count,
            "vertices": len(mesh.vertices),
            "uv_layers": uv_layers,
            "materials": [m.name if m else None for m in mesh.materials],
            "skinned": bool(obj.find_armature()),
        })

    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"}:
            if any(abs(float(s) - 1.0) > 1e-4 for s in obj.scale):
                warnings.append(f"{obj.name}: object scale is not applied {tuple(obj.scale)}")
            if any(abs(float(s)) < 1e-6 for s in obj.scale):
                errors.append(f"{obj.name}: zero scale component")
    for material in bpy.data.materials:
        if not material.use_nodes:
            warnings.append(f"material {material.name}: nodes disabled")

    result = {
        "asset": str(asset_path),
        "passed": not errors,
        "meshes": mesh_results,
        "armatures": [a.name for a in armatures],
        "errors": errors,
        "warnings": warnings,
    }
    return result


if __name__ == "__main__":
    asset, report = args_after_double_dash()
    result = validate(asset)
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result["passed"] else 2)
