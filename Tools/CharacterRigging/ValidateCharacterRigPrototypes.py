"""Headless structural and deformation smoke tests for the Rigify fixtures."""

import math
import bpy


def evaluated_positions(mesh):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    evaluated_mesh = evaluated.to_mesh()
    result = [tuple(vertex.co) for vertex in evaluated_mesh.vertices]
    evaluated.to_mesh_clear()
    return result


def run_species(species):
    rig = bpy.data.objects.get(f"{species.title()}_RIG")
    mesh = bpy.data.objects.get(f"{species.title()}_SkinningTestMesh")
    assert rig and rig.type == "ARMATURE", f"{species}: missing armature"
    assert mesh and mesh.type == "MESH", f"{species}: missing skinned mesh"
    assert mesh.find_armature() is rig, f"{species}: mesh is not bound to its rig"
    assert len(mesh.data.vertices) > 0 and len(mesh.vertex_groups) > 0

    influence_counts = []
    weight_sums = []
    for vertex in mesh.data.vertices:
        influences = [entry.weight for entry in vertex.groups if entry.weight > 1e-6]
        assert influences, f"{species}: vertex {vertex.index} is unweighted"
        influence_counts.append(len(influences))
        weight_sums.append(sum(influences))
    assert max(influence_counts) <= 4, f"{species}: >4 influences remain"
    assert min(weight_sums) > 0.98 and max(weight_sums) < 1.02, (
        f"{species}: weights are not normalized: {min(weight_sums):.4f}..{max(weight_sums):.4f}"
    )

    deform_names = {group.name for group in mesh.vertex_groups}
    candidates = [bone for bone in rig.pose.bones
                  if bone.name in deform_names and not bone.name.endswith("Root")]
    assert candidates, f"{species}: no weighted pose bone found"
    before = evaluated_positions(mesh)
    moved = False
    for bone in candidates:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler[0] = math.radians(23)
        bpy.context.view_layer.update()
        after = evaluated_positions(mesh)
        delta = max(math.dist(a, b) for a, b in zip(before, after))
        bone.rotation_euler[0] = 0.0
        bpy.context.view_layer.update()
        if delta > 1e-4:
            moved = True
            break
    assert moved, f"{species}: changing weighted bones did not deform the mesh"
    return (len(mesh.data.vertices), len(mesh.vertex_groups), max(influence_counts),
            min(weight_sums), max(weight_sums), delta)


if __name__ == "__main__":
    for kind in ("human", "horse", "wolf"):
        result = run_species(kind)
        print(f"PASS {kind}: vertices={result[0]}, groups={result[1]}, "
              f"maxInfluences={result[2]}, weightSum={result[3]:.4f}..{result[4]:.4f}, "
              f"poseDelta={result[5]:.6f}")
