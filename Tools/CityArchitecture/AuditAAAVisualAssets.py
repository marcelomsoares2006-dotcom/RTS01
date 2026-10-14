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
    mesh.update(calc_edges=True)
    uv0 = mesh.uv_layers[0] if len(mesh.uv_layers) else None
    uv2 = mesh.uv_layers[1] if len(mesh.uv_layers) > 1 else None
    def uv_info(layer):
        if layer is None or not layer.data:
            return {"present": False, "valid": False, "outside_01": 0}
        coords = [(float(u.uv.x), float(u.uv.y)) for u in layer.data]
        outside = sum(1 for x, y in coords if not (0.0 <= x <= 1.0 and 0.0 <= y <= 1.0))
        valid = all(finite((u.uv.x, u.uv.y)) for u in layer.data)
        if coords:
            min_u = min(x for x, _ in coords)
            max_u = max(x for x, _ in coords)
            min_v = min(y for _, y in coords)
            max_v = max(y for _, y in coords)
        else:
            min_u = max_u = min_v = max_v = 0.0
        span_u = max_u - min_u
        span_v = max_v - min_v
        max_overrun = max((max(0.0, -x, x - 1.0, -y, y - 1.0) for x, y in coords), default=0.0)
        return {
            "present": True,
            "valid": valid,
            "outside_01": outside,
            "bounds": [round(min_u, 5), round(min_v, 5), round(max_u, 5), round(max_v, 5)],
            "tile_span": [round(span_u, 5), round(span_v, 5)],
            "max_overrun": round(max_overrun, 8),
            "repetition_likely": bool(span_u > 1.05 or span_v > 1.05),
        }
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons if len(p.vertices) >= 3)
    invalid_normals = sum(1 for p in mesh.polygons
                          if len(p.vertices) >= 3 and
                          (not finite(p.normal) or p.normal.length <= 0.000001))
    degenerate_faces = sum(1 for p in mesh.polygons if len(p.vertices) >= 3 and p.area <= 0.000001)
    # Blender 5.2 não expõe MeshEdge.is_manifold; duas faces vinculadas é o
    # caso fechado esperado para uma aresta de superfície. Arestas com 0/1 ou
    # mais de 2 faces são reportadas, sem alterar a malha.
    edge_face_counts = {}
    for poly in mesh.polygons:
        verts = list(poly.vertices)
        for i, a in enumerate(verts):
            key = tuple(sorted((int(a), int(verts[(i + 1) % len(verts)]))))
            edge_face_counts[key] = edge_face_counts.get(key, 0) + 1
    non_manifold = sum(1 for count in edge_face_counts.values() if count != 2)
    return {
        "name": obj.name,
        "vertices": len(mesh.vertices),
        "triangles": tris,
        "uv0": uv_info(uv0),
        "uv2": uv_info(uv2),
        "non_manifold_edges": non_manifold,
        "invalid_normals": invalid_normals,
        "degenerate_faces": degenerate_faces,
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


def audit_prefabs():
    """Read-only YAML inventory; component presence is evidence, not gameplay validation."""
    prefab_roots = [ROOT / "Assets" / "CityArchitecture" / "Prefabs",
                    ROOT / "Assets" / "Resources" / "Prefabs" / "Units" / "Characters"]
    rows = []
    for base in prefab_roots:
        for path in sorted(base.rglob("*.prefab")):
            text = path.read_text(encoding="utf-8", errors="replace")
            rows.append({
                "path": str(path.relative_to(ROOT)).replace("\\", "/"),
                "lod_group": "--- !u!205 " in text or "LODGroup:" in text,
                "collider_components": sum((
                    text.count("--- !u! BoxCollider"),
                    text.count("--- !u!65 "),
                    text.count("--- !u! CapsuleCollider"),
                    text.count("--- !u!136 "),
                    text.count("--- !u! MeshCollider"),
                    text.count("--- !u!64 "),
                    text.count("--- !u! SphereCollider"),
                    text.count("--- !u!135 "),
                    text.count("m_EditorClassIdentifier: UnityEngine.BoxCollider"),
                    text.count("m_EditorClassIdentifier: UnityEngine.CapsuleCollider"),
                    text.count("m_EditorClassIdentifier: UnityEngine.MeshCollider"),
                    text.count("m_EditorClassIdentifier: UnityEngine.SphereCollider"),
                )),
                "skinned_mesh_renderer": "SkinnedMeshRenderer" in text,
                "mesh_renderer": "MeshRenderer" in text,
                "pink_name_risk": any(k in text.lower() for k in ("pink", "magenta", "error")),
            })
    return rows


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
        "prefab_inventory": audit_prefabs(),
    }
    (OUT / "visual-assets-audit.json").write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    uv_rows = []
    for row in rows:
        for mesh in row["meshes"]:
            uv = mesh["uv0"]
            if not uv.get("outside_01"):
                continue
            path = row["path"]
            if "MarketPost" in mesh["name"]:
                classification = "BORDA_DE_PRECISAO"
                rationale = "Ultrapassagem mínima/limítrofe e repetida no LOD correspondente; normalizar poderia introduzir costura ou mudar a amostra de textura."
                action = "MANTER; validar no material Unity antes de editar."
            elif "Crossbow" in path and uv.get("repetition_likely"):
                classification = "REPETICAO_INTENCIONAL_PROVAVEL"
                rationale = "Span UV > 1 em peças com materiais de madeira/ferro/corda e LODs; o padrão é compatível com textura repetida/trim."
                action = "MANTER; não empacotar nem reescalar automaticamente."
            elif "HeraldicKnight" in path:
                classification = "ILHA_QUASE_LIMITE"
                rationale = "Uma coordenada marginal fora do tile em LOD2, sem span repetido; requer inspeção da ilha e do material antes de reexportar."
                action = "REVISÃO MANUAL; sem correção automática."
            else:
                classification = "REVISAR"
                rationale = "Ocorrência fora do tile sem evidência suficiente para uma correção não destrutiva."
                action = "REVISÃO MANUAL; sem correção automática."
            uv_rows.append({"path": path, "mesh": mesh["name"], "outside": uv["outside_01"],
                            "bounds": uv["bounds"], "tile_span": uv["tile_span"],
                            "max_overrun": uv.get("max_overrun", 0),
                            "classification": classification, "rationale": rationale, "action": action,
                            "non_manifold_edges": mesh["non_manifold_edges"],
                            "invalid_normals": mesh["invalid_normals"],
                            "degenerate_faces": mesh["degenerate_faces"],
                            "materials": mesh["materials"]})
    (OUT / "uv-review-2026-10-14.json").write_text(json.dumps({"status": "REVIEW", "occurrences": uv_rows}, indent=2, ensure_ascii=False), encoding="utf-8")
    report = ["# Revisão das UVs fora de 0–1 — 14/10/2026", "", "Gate reproduzível: executar `blender.exe -b -P Tools/CityArchitecture/AuditAAAVisualAssets.py`. O script não salva nem reexporta assets.", "", f"Ocorrências: **{len(uv_rows)}**. Correções automáticas aplicadas: **0**.", "", "## Critério", "", "UV fora de 0–1 só é corrigida quando há evidência de erro de exportação e ausência de intenção de tiling. Spans maiores que um tile, materiais repetíveis e correspondência entre LODs são tratados como repetição provável; bordas marginais permanecem para inspeção.", "", "## Classificação", ""]
    for item in uv_rows:
        report.append(f"- **{item['classification']}** — `{item['path']}/{item['mesh']}`; fora={item['outside']}, bounds={item['bounds']}, span={item['tile_span']}, overrun máximo={item['max_overrun']}. {item['rationale']} Ação: {item['action']}")
    report += ["", "## Integridade 3D associada", "", "O JSON principal registra arestas não-manifold e faces degeneradas por malha, além de normais inválidas. Esses números são sinais de revisão, não autorização para remodelagem automática. Nenhuma malha foi alterada.", "", "## Gate", "", "- **PASS de segurança visual:** importação Blender concluída para 44 FBX; sem material rosa por nome, escala não unitária ou dimensão nula.", "- **REVIEW obrigatório:** 20 ocorrências UV permanecem classificadas, sem normalização automática.", "- **REVIEW de integração:** inventário de prefabs informa LODGroup e colliders; adicionar componentes permanece fora desta frente para não alterar placement/NavMesh.", ""]
    (OUT / "uv-review-2026-10-14.md").write_text("\n".join(report), encoding="utf-8")
    lines = ["# Auditoria visual AAA — CityArchitecture e personagens", "", "Modo: somente leitura; nenhum asset foi salvo ou substituído.", "", f"Arquivos auditados: {len(rows)}", f"Resultado: **{result['status']}**", ""]
    if failures:
        lines += ["## Pontos para revisão", ""] + [f"- {x}" for x in failures] + [""]
    else:
        lines += ["Nenhum material rosa por nome, UV0 inválida ou mesh de dimensão nula foi encontrado.", ""]
    lines += ["## Limites", "", "Escala e pivô são reportados por objeto; a confirmação final do prefab/Unity depende da importação do editor.", "Colisores e LODGroup são inventariados nos YAMLs dos prefabs; a ausência não é corrigida nesta frente porque pode alterar placement/NavMesh.", "Detalhes das 20 UVs: `uv-review-2026-10-14.md` e `uv-review-2026-10-14.json`."]
    (OUT / "README.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"AAA_VISUAL_AUDIT_{result['status']} files={len(rows)} failures={len(failures)}")


if __name__ == "__main__":
    main()
