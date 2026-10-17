"""Auditoria somente leitura dos LODs arquitetônicos e da cópia isolada.

Importa FBX em cenas temporárias, compara cada subobjeto do LOD com o LOD0
correspondente e grava evidência sem salvar Blender/FBX nem alterar Assets.
"""
from __future__ import annotations

import json
import math
import os
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Docs" / "AAA" / "VisualAudit"
OUT.mkdir(parents=True, exist_ok=True)

BUILDINGS = ("House_Medieval", "Barracks_Medieval", "Market_Classical", "Temple_Imperial")
PROD_ROOT = ROOT / "Assets" / "CityArchitecture" / "Generated"
TEST_ROOT = Path(os.environ.get("ERA_IMPERIAL_LOD_TEST_ROOT", r"D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedTest"))


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def import_fbx(path: Path):
    reset()
    if not path.is_file():
        return []
    bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=False)
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def finite(v):
    return all(math.isfinite(float(x)) for x in v)


def bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    lo = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    hi = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    center = (lo + hi) * 0.5
    size = hi - lo
    return {"min": [round(float(x), 6) for x in lo], "max": [round(float(x), 6) for x in hi],
            "center": [round(float(x), 6) for x in center], "size": [round(float(x), 6) for x in size]}


def tris(obj):
    return sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)


def snapshot(path: Path):
    objects = import_fbx(path)
    rows = []
    for obj in sorted(objects, key=lambda item: item.name):
        b = bounds(obj)
        rows.append({"name": obj.name, "triangles": tris(obj), "bounds": b,
                     "location": [round(float(x), 6) for x in obj.location],
                     "scale": [round(float(x), 6) for x in obj.scale],
                     "finite": finite(obj.location) and finite(obj.scale)})
    return {"path": str(path), "exists": path.is_file(), "objects": rows,
            "root_objects": sorted(obj.name for obj in bpy.context.scene.objects if obj.parent is None)}


def stem_name(name):
    return name[:-4] if name.endswith("_LOD") else name


def compare(base, lod):
    base_by_name = {row["name"]: row for row in base["objects"]}
    lod_by_name = {stem_name(row["name"]): row for row in lod["objects"]}
    rows = []
    for name, a in sorted(base_by_name.items()):
        b = lod_by_name.get(name)
        if not b:
            rows.append({"object": name, "status": "MISSING_IN_LOD"})
            continue
        ac = Vector(a["bounds"]["center"])
        bc = Vector(b["bounds"]["center"])
        asz = Vector(a["bounds"]["size"])
        bsz = Vector(b["bounds"]["size"])
        delta = bc - ac
        al = Vector(a["location"])
        bl = Vector(b["location"])
        pivot_delta = bl - al
        pivot_error = float(pivot_delta.length)
        # Decimate may shrink bounds, but a translation of a fragment is not
        # explained by reduction. Use a conservative 1 cm threshold.
        center_error = float(delta.length)
        rows.append({"object": name, "status": "PASS" if center_error <= 0.01 else "DISPLACED_FRAGMENT",
                     "base_triangles": a["triangles"], "lod_triangles": b["triangles"],
                     "center_delta": [round(float(x), 6) for x in delta],
                     "center_error": round(center_error, 6),
                     "pivot_delta": [round(float(x), 6) for x in pivot_delta],
                     "pivot_error": round(pivot_error, 6),
                     "pivot_aligned": pivot_error <= 0.00001,
                     "base_size": [round(float(x), 6) for x in asz],
                     "lod_size": [round(float(x), 6) for x in bsz]})
    return rows


def main():
    buildings = []
    for building in BUILDINGS:
        base_path = PROD_ROOT / "Buildings" / f"{building}.fbx"
        base = snapshot(base_path)
        lods = []
        for level in (1, 2):
            candidate = PROD_ROOT / "LOD" / f"{building}_LOD{level}.fbx"
            isolated = TEST_ROOT / f"{building}_LOD{level}.fbx"
            prod = snapshot(candidate)
            test = snapshot(isolated)
            lods.append({"level": level, "production": prod, "isolated": test,
                         "comparison_production": compare(base, prod),
                         "comparison_isolated": compare(base, test)})
        buildings.append({"building": building, "lod0": base, "lods": lods})
    result = {
        "audit": "Era Imperial architectural LOD audit",
        "mode": "read-only Blender FBX import; no asset saved",
        "thresholds": {"fragment_center_error_m": 0.01, "pivot_error_m": 0.00001, "promotion": "forbidden when popping/displacement remains"},
        "test_root": str(TEST_ROOT),
        "buildings": buildings,
    }
    out = OUT / "2026-10-17-uv-lod-audit.json"
    out.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    displaced = []
    for item in buildings:
        for lod in item["lods"]:
            for mode in ("comparison_production", "comparison_isolated"):
                displaced.extend((item["building"], lod["level"], mode, row)
                                 for row in lod[mode] if row["status"] == "DISPLACED_FRAGMENT")
    print(f"ARCH_LOD_AUDIT buildings={len(buildings)} displaced_fragments={len(displaced)} output={out}")


if __name__ == "__main__":
    main()
