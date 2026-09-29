"""Reimport the generated FBXs into Blender and assert usable skinning."""

from pathlib import Path
import math
import bpy


PROJECT = Path(__file__).resolve().parents[2]
FBX_DIR = PROJECT / "Assets" / "CharacterRigging" / "Prototypes"


def evaluated_positions(mesh):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    result_mesh = evaluated.to_mesh()
    positions = [tuple(v.co) for v in result_mesh.vertices]
    evaluated.to_mesh_clear()
    return positions


def verify(species):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX_DIR / f"{species}_SkinnedPrototype.fbx"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    armatures = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    assert len(meshes) == 1, f"{species}: expected one mesh, got {len(meshes)}"
    assert len(armatures) == 1, f"{species}: expected one armature, got {len(armatures)}"
    mesh, rig = meshes[0], armatures[0]
    assert mesh.find_armature() is rig, f"{species}: imported mesh is not bound to armature"
    assert len(rig.data.bones) > 0 and len(mesh.vertex_groups) > 0

    counts, sums = [], []
    for vertex in mesh.data.vertices:
        weights = [entry.weight for entry in vertex.groups if entry.weight > 1e-6]
        assert weights, f"{species}: imported vertex {vertex.index} has no weights"
        counts.append(len(weights))
        sums.append(sum(weights))
    assert max(counts) <= 4, f"{species}: exported FBX has more than 4 influences"
    assert min(sums) > 0.98 and max(sums) < 1.02, f"{species}: exported weights not normalized"

    groups = {group.name for group in mesh.vertex_groups}
    before = evaluated_positions(mesh)
    delta = 0.0
    for bone in rig.pose.bones:
        if bone.name not in groups:
            continue
        bone.rotation_mode = "XYZ"
        bone.rotation_euler[0] = math.radians(23)
        bpy.context.view_layer.update()
        after = evaluated_positions(mesh)
        delta = max(math.dist(a, b) for a, b in zip(before, after))
        bone.rotation_euler[0] = 0.0
        bpy.context.view_layer.update()
        if delta > 1e-4:
            break
    assert delta > 1e-4, f"{species}: imported armature does not deform FBX mesh"
    print(f"PASS FBX {species}: bones={len(rig.data.bones)}, vertices={len(mesh.data.vertices)}, "
          f"maxInfluences={max(counts)}, weightSum={min(sums):.4f}..{max(sums):.4f}, "
          f"poseDelta={delta:.6f}")


for species in ("Human", "Horse", "Wolf"):
    verify(species)
