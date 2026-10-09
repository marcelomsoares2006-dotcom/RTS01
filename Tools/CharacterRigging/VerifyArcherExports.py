"""Re-import every exported Archer FBX/GLB in a clean Blender session and verify them.
Run: blender -b --factory-startup --python-exit-code 1 --python Tools/CharacterRigging/VerifyArcherExports.py
Writes Docs/CharacterMeshAudit/Archer/export-verification.json and prints EXPORT_VERIFY_PASSED/FAILED."""
from pathlib import Path
import json
import bpy

ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / "Assets/CharacterRigging/Archer/Models"
ACC = ROOT / "Assets/CharacterRigging/Archer/Accessories"
OUT = ROOT / "Docs/CharacterMeshAudit/Archer/export-verification.json"
CLIPS = {"Idle": 8.4, "Walk": 1.0417, "Run": 0.625, "Aim": 3.7667, "DrawString": 3.2, "Fire": 1.4}
results, failures = [], []


def check(ok, message):
    results.append(("PASS " if ok else "FAIL ") + message)
    if not ok:
        failures.append(message)


def fresh(path, kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if kind == "fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    else:
        bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name != "Icosphere"]  # Icosphere = bone shape made by the glTF importer
    rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    return meshes, rigs


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def action_seconds(fps):
    if not bpy.data.actions:
        return None
    s, e = bpy.data.actions[0].frame_range
    return (e - s) / fps


# base model
meshes, rigs = fresh(MODELS / "Archer.fbx", "fbx")
names = sorted(m.name for m in meshes)
check(len(rigs) == 1 and len(rigs[0].data.bones) == 24, f"Archer.fbx: 1 armature com 24 ossos ({[len(r.data.bones) for r in rigs]})")
check(len(meshes) == 3 and all("Archer_LOD" in n for n in names), f"Archer.fbx: malhas LOD {names} com {[tris(m) for m in sorted(meshes, key=lambda m: m.name)]} triangulos")
check(all(len(m.vertex_groups) >= 22 for m in meshes), "Archer.fbx: grupos de vertices (pesos) presentes em todos os LODs")
check(all(any(mod.type == 'ARMATURE' for mod in m.modifiers) or m.parent in rigs for m in meshes), "Archer.fbx: malhas ligadas ao armature")
dims = max(meshes, key=tris).dimensions
check(1.7 < dims.z < 1.9 or 1.7 < dims.y < 1.9, f"Archer.fbx: altura ~1.78 m (dim {tuple(round(d, 2) for d in dims)})")
check(not bpy.data.actions, "Archer.fbx: bind pose sem animacao")

# clip files
for clip, seconds in CLIPS.items():
    meshes, rigs = fresh(MODELS / f"Archer@{clip}.fbx", "fbx")
    fps = bpy.context.scene.render.fps
    got = action_seconds(24 if clip in ("Walk", "Run") else 30)
    check(len(rigs) == 1 and len(rigs[0].data.bones) == 24 and len(bpy.data.actions) >= 1 and got is not None and abs(got - seconds) < 0.12,
          f"Archer@{clip}.fbx: 24 ossos, acao com ~{seconds}s (obtido {got})")

# GLB: every clip in one portable file
meshes, rigs = fresh(MODELS / "Archer.glb", "glb")
anims = sorted(a.name for a in bpy.data.actions)
check(len(rigs) == 1 and len(rigs[0].data.bones) == 24, "Archer.glb: 1 armature, 24 ossos")
check(len(meshes) == 1 and tris(meshes[0]) > 25000, f"Archer.glb: malha LOD0 ({[tris(m) for m in meshes]} triangulos)")
check(all(any(c in a for a in anims) for c in CLIPS), f"Archer.glb: animacoes {anims}")
check(any(i.size[0] >= 2048 for i in bpy.data.images), f"Archer.glb: textura 2K embutida ({[ (i.name, tuple(i.size)) for i in bpy.data.images]})")

# accessories
for name in ("Archer_Bow", "Archer_Quiver", "Archer_Arrow"):
    meshes, _ = fresh(ACC / f"{name}.fbx", "fbx")
    empties = [o.name for o in bpy.data.objects if o.type == "EMPTY"]
    check(len(meshes) == 1 and tris(meshes[0]) > 100, f"{name}.fbx: objeto separado ({tris(meshes[0])} triangulos, empties {empties})")

OUT.write_text(json.dumps({"results": results, "failures": failures}, indent=2), encoding="utf-8")
print("\n".join(results))
print("EXPORT_VERIFY_PASSED" if not failures else "EXPORT_VERIFY_FAILED " + str(failures))
