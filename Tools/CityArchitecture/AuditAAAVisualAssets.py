"""Auditoria visual não destrutiva de malhas CityArchitecture e personagens.

Executa no Blender em modo background. Não salva nem altera os arquivos importados.
Gera somente evidência em Docs/AAA/VisualAudit.
"""
from __future__ import annotations

import bpy
import json
import math
import os
import sys
from pathlib import Path

ROOT = Path.cwd()
OUT = ROOT / "Docs" / "AAA" / "VisualAudit"
OUT.mkdir(parents=True, exist_ok=True)

CITY = ROOT / "Assets" / "CityArchitecture"
CHAR = ROOT / "Assets" / "CharacterRigging"


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(datablocks):
            if item.users == 0:
                datablocks.remove(item)


def import_file(path: Path) -> bool:
    before = set(bpy.data.objects)
    try:
        if path.suffix.lower() == ".fbx":
            bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=False)
        elif path.suffix.lower() == ".glb":
            bpy.ops.import_scene.gltf(filepath=str(path))
        else:
            return False
    except Exception:
        return False
    return len(set(bpy.data.objects) - before) > 0


def finite(v):
    return all(math.isfinite(float(x)) for x in v)


def audit_mesh(obj):
    mesh = obj.data
    uv0 = mesh.uv_layers[0] if len(mesh.uv_layers) else None
    uv2 = mesh.uv_layers[1] if len(mesh.uv_layers) > 1 else None
    def uv_info(layer):
        if layer is None or not layer.data:
            return {"present": False, "valid": False, "outside_01": 0}
        outside = sum(1 for u in layer.data if not (0.0 <= u.uv.x <= 1.0 and 0.0 <= u.uv.y <= 1.0))
        valid = all(finite((u.uv.x, u.uv.y)) for u in layer.data)
        return {"present": True, "valid": valid, "outside_01": outside}
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons if len(p.vertices) >= 3)
    return {
        "name": obj.name,
        "vertices": len(mesh.vertices),
        "triangles": tris,
        "uv0": uv_info(uv0),
        "uv2": uv_info(uv2),
        "materials": [m.name if m else None for m in obj.data.materials],
        "scale": [round(float(x), 6) for x in obj.scale],
        "location": [round(float(x), 6) for x in obj.location],
        "dimensions": [round(float(x), 6) for x in obj.dimensions],
    }


def audit_path(path: Path):
    reset()
    imported = import_file(path)
    objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshes = [audit_mesh(o) for o in objects]
    all_materials = sorted({m for row in meshes for m in row["materials"] if m})
    pink_names = [m for m in all_materials if any(k in m.lower() for k in ("pink", "magenta", "error"))]
    lod_names = sorted(o.name for o in objects if "lod" in o.name.lower())
    return {
        "path": str(path.relative_to(ROOT)).replace("\\", "/"),
        "imported": imported,
        "mesh_objects": len(objects),
        "meshes": meshes,
        "materials": all_materials,
        "pink_material_keywords": pink_names,
        "lod_objects": lod_names,
        "root_objects": sorted(o.name for o in bpy.context.scene.objects if o.parent is None),
        "has_non_unit_object_scale": any(any(abs(s - 1.0) > 0.0001 for s in o.scale) for o in objects),
        "has_zero_dimension_mesh": any(any(d <= 0.0001 for d in o.dimensions) for o in objects),
    }


def selected_files():
    city = sorted(CITY.glob("Generated/**/*.fbx"))
    # Personagens já existentes, somente os modelos principais; animações @ são ruído para esta auditoria.
    chars = sorted(p for p in CHAR.glob("**/*.fbx") if "@" not in p.name and "Accessories" not in p.parts)
    return city + chars


def main():
    rows = [audit_path(p) for p in selected_files()]
    failures = []
    for row in rows:
        if not row["imported"]:
            failures.append(row["path"] + ": importação falhou")
        if row["pink_material_keywords"]:
            failures.append(row["path"] + ": material com palavra rosa/magenta/error")
        if row["has_zero_dimension_mesh"]:
            failures.append(row["path"] + ": mesh com dimensão nula")
        for mesh in row["meshes"]:
            if not mesh["uv0"]["present"] or not mesh["uv0"]["valid"]:
                failures.append(row["path"] + "/" + mesh["name"] + ": UV0 ausente ou inválida")
            if mesh["uv0"]["outside_01"]:
                failures.append(row["path"] + "/" + mesh["name"] + ": UV0 fora de 0-1")
    result = {
        "audit": "Era Imperial AAA visual assets",
        "mode": "read-only Blender import; no asset saved",
        "files": len(rows),
        "failures": failures,
        "status": "PASS" if not failures else "REVIEW",
        "rows": rows,
    }
    (OUT / "visual-assets-audit.json").write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    lines = ["# Auditoria visual AAA — CityArchitecture e personagens", "", "Modo: somente leitura; nenhum asset foi salvo ou substituído.", "", f"Arquivos auditados: {len(rows)}", f"Resultado: **{result['status']}**", ""]
    if failures:
        lines += ["## Pontos para revisão", ""] + [f"- {x}" for x in failures] + [""]
    else:
        lines += ["Nenhum material rosa por nome, UV0 inválida ou mesh de dimensão nula foi encontrado.", ""]
    lines += ["## Limites", "", "Escala e pivô são reportados por objeto; a confirmação final do prefab/Unity depende da importação do editor.", "Colisores e enquadramento de câmera são auditados separadamente no Unity."]
    (OUT / "README.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"AAA_VISUAL_AUDIT_{result['status']} files={len(rows)} failures={len(failures)}")


if __name__ == "__main__":
    main()
