#!/usr/bin/env python3
"""Deterministic, reversible static authoring for architectural prefab candidates.

This tool deliberately does not edit production prefabs.  It snapshots each source
into Backups, writes a candidate prefab into Candidates, and emits a JSON audit.
The generated YAML is Unity 6 prefab YAML and uses only existing MeshFilter meshes.

Optional promotion is explicit and guarded by the audit, source hash, and backup
manifest.  The normal invocation never promotes anything.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path


PREFABS = (
    ("Barracks_Medieval", "Assets/CityArchitecture/Prefabs/EraBuildings/Barracks_Medieval.prefab"),
    ("House_Medieval", "Assets/CityArchitecture/Prefabs/EraBuildings/House_Medieval.prefab"),
    ("Market_Classical", "Assets/CityArchitecture/Prefabs/EraBuildings/Market_Classical.prefab"),
    ("Temple_Imperial", "Assets/CityArchitecture/Prefabs/EraBuildings/Temple_Imperial.prefab"),
    ("EraImperialCityBlock", "Assets/CityArchitecture/Prefabs/EraImperialCityBlock.prefab"),
    ("Gatehouse", "Assets/CityArchitecture/Prefabs/Gatehouse.prefab"),
    ("WallCorner", "Assets/CityArchitecture/Prefabs/WallCorner.prefab"),
    ("WallStraight", "Assets/CityArchitecture/Prefabs/WallStraight.prefab"),
    ("WallTowerConnector", "Assets/CityArchitecture/Prefabs/WallTowerConnector.prefab"),
)


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def stable_id(seed: str) -> int:
    # Unity fileIDs are signed 64-bit integers. Keep the generated ID positive.
    return int.from_bytes(hashlib.sha256(seed.encode("utf-8")).digest()[:8], "big") & 0x7FFF_FFFF_FFFF_FFFF


def blocks(text: str, marker: str) -> list[tuple[int, int, str]]:
    starts = [m.start() for m in re.finditer(r"^--- !u!" + re.escape(marker) + r" &", text, re.MULTILINE)]
    result = []
    for index, start in enumerate(starts):
        end = starts[index + 1] if index + 1 < len(starts) else len(text)
        result.append((start, end, text[start:end]))
    return result


def field(block: str, key: str) -> str | None:
    match = re.search(rf"^\s*{re.escape(key)}:\s*(.+)$", block, re.MULTILINE)
    return match.group(1).strip() if match else None


def file_id(value: str | None) -> int | None:
    if not value:
        return None
    match = re.search(r"fileID:\s*(-?\d+)", value)
    return int(match.group(1)) if match else None


def component_id(block: str) -> int | None:
    match = re.match(r"^--- !u!\d+ &(-?\d+)", block)
    return int(match.group(1)) if match else None


def make_mesh_collider(go_id: int, collider_id: int, mesh_ref: str) -> str:
    return f"""--- !u!64 &{collider_id}
MeshCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers: 0
  m_ExcludeLayers: 0
  m_LayerOverridePriority: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 4
  m_Convex: 0
  m_CookingOptions: 30
  m_Mesh: {mesh_ref}
"""


def make_box_collider(go_id: int, collider_id: int) -> str:
    return f"""--- !u!65 &{collider_id}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Enabled: 0
  serializedVersion: 3
  m_Size: {{x: 1, y: 1, z: 1}}
  m_Center: {{x: 0, y: 0, z: 0}}
"""


def make_lod_group(go_id: int, lod_id: int, renderers: list[int]) -> str:
    refs = "\n".join(f"   - {{fileID: {renderer}}}" for renderer in renderers)
    if not refs:
        refs = "   []"
    return f"""--- !u!205 &{lod_id}
LODGroup:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LODs:
  - screenRelativeTransitionHeight: 0.6
    fadeTransitionWidth: 0
    renderers:
{refs}
  m_LocalReferencePoint: {{x: 0, y: 0, z: 0}}
  m_Size: 1
  m_FadeMode: 0
  m_AnimateCrossFading: 0
  m_UseLightProbe: 0
  m_LastLODIsBillboard: 0
"""


def author(source: Path, candidate: Path, backup: Path) -> dict:
    original = source.read_text(encoding="utf-8")
    source_hash = sha256(source)
    backup.parent.mkdir(parents=True, exist_ok=True)
    candidate.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, backup)

    game_objects = blocks(original, "1")
    if not game_objects:
        raise ValueError(f"no GameObject blocks: {source}")
    root = next((block for _, _, block in game_objects if field(block, "m_Father") == "{fileID: 0}"), game_objects[0][2])
    root_id = component_id(root)
    assert root_id is not None

    mesh_filters = blocks(original, "33")
    mesh_renderers = blocks(original, "23")
    mesh_by_go: dict[int, str] = {}
    for _, _, block in mesh_filters:
        go = file_id(field(block, "m_GameObject"))
        mesh = field(block, "m_Mesh")
        if go is not None and mesh and "fileID: 0" not in mesh:
            mesh_by_go[go] = mesh
    renderer_ids = [component_id(block) for _, _, block in mesh_renderers]
    renderer_ids = [value for value in renderer_ids if value is not None]

    lod_id = stable_id(source_hash + ":LODGroup")
    collider_records = []
    suffix = ""
    # Add one MeshCollider beside each existing MeshFilter. No mesh is duplicated.
    for go in sorted(mesh_by_go):
        collider_id = stable_id(source_hash + f":MeshCollider:{go}")
        suffix += make_mesh_collider(go, collider_id, mesh_by_go[go])
        collider_records.append({"gameObjectFileID": go, "kind": "MeshCollider", "fileID": collider_id, "mesh": mesh_by_go[go]})
    empty_marker = not mesh_by_go
    if empty_marker:
        collider_id = stable_id(source_hash + ":BoxCollider:empty-marker")
        suffix += make_box_collider(root_id, collider_id)
        collider_records.append({"gameObjectFileID": root_id, "kind": "BoxCollider", "fileID": collider_id, "enabled": False, "reason": "source prefab has no MeshFilter"})

    # Attach LODGroup to the source root without changing any existing component.
    root_start = next(start for start, _, block in game_objects if block == root)
    root_end = root_start + len(root)
    root_with_component = root.replace("  m_Component:\n", f"  m_Component:\n  - component: {{fileID: {lod_id}}}\n", 1)
    candidate_text = original[:root_start] + root_with_component + original[root_end:] + make_lod_group(root_id, lod_id, renderer_ids) + suffix
    candidate.write_text(candidate_text, encoding="utf-8", newline="\n")
    return {
        "name": source.stem,
        "source": str(source).replace("\\", "/"),
        "sourceSha256": source_hash,
        "backup": str(backup).replace("\\", "/"),
        "candidate": str(candidate).replace("\\", "/"),
        "candidateSha256": sha256(candidate),
        "rootGameObjectFileID": root_id,
        "lodGroupFileID": lod_id,
        "rendererCount": len(renderer_ids),
        "meshFilterCount": len(mesh_by_go),
        "colliderCount": len(collider_records),
        "colliders": collider_records,
        "emptyMarker": empty_marker,
        "status": "BLOCKED_EMPTY_SOURCE" if empty_marker else "CANDIDATE_READY",
    }


def audit(row: dict) -> list[str]:
    failures = []
    candidate = Path(row["candidate"])
    text = candidate.read_text(encoding="utf-8")
    if text.count("LODGroup:") != 1:
        failures.append("LODGroup count is not exactly one")
    if row["meshFilterCount"] and text.count("MeshCollider:") != row["meshFilterCount"]:
        failures.append("MeshCollider count does not match mesh-bearing objects")
    if row["emptyMarker"] and "m_Enabled: 0" not in text:
        failures.append("empty source marker collider must be disabled")
    if sha256(Path(row["backup"])) != row["sourceSha256"]:
        failures.append("backup hash differs from source hash")
    if sha256(Path(row["candidate"])) != row["candidateSha256"]:
        failures.append("candidate hash is not stable")
    return failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--label", default="2026-10-18")
    parser.add_argument("--promote", action="store_true", help="copy only fully passing candidates over production")
    args = parser.parse_args()
    root = args.root.resolve()
    base = root / "Tools" / "CityArchitecture" / "RuntimeIntegration" / args.label
    candidates = base / "Candidates"
    backups = base / "Backups"
    manifest = {"tool": "AuthorArchitecturalRuntimeIntegration.py", "label": args.label, "promoted": False, "prefabs": []}

    for name, relative in PREFABS:
        source = root / relative
        candidate = candidates / relative
        backup = backups / relative
        if not source.is_file():
            raise FileNotFoundError(source)
        row = author(source, candidate, backup)
        row["sourceSha256AfterAuthoring"] = sha256(source)
        row["sourceUnchanged"] = row["sourceSha256AfterAuthoring"] == row["sourceSha256"]
        row["auditFailures"] = audit(row)
        if not row["sourceUnchanged"]:
            row["auditFailures"].append("production source hash changed during authoring")
        if row["auditFailures"]:
            row["status"] = "AUDIT_FAILED"
        manifest["prefabs"].append(row)

    if args.promote:
        if any(row["auditFailures"] or row["emptyMarker"] for row in manifest["prefabs"]):
            raise SystemExit("promotion refused: audit failures or empty-source marker present")
        for row in manifest["prefabs"]:
            source = root / Path(row["source"])
            if sha256(source) != row["sourceSha256"]:
                raise SystemExit(f"promotion refused: source changed after authoring: {source}")
            shutil.copy2(row["candidate"], source)
        manifest["promoted"] = True

    out = root / "Docs" / "QA" / f"{args.label}-architectural-runtime-integration.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"audit": str(out), "prefabs": len(manifest["prefabs"]), "promoted": manifest["promoted"]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
