"""FBX round-trip check for the Heraldic Knight (Etapa 1, verification 1).

Run after BuildHeraldicKnight.py:
  blender -b --factory-startup --python Tools/CharacterRigging/VerifyHeraldicKnightFbx.py

For the base FBX: one mesh bound to one armature, bone names, vertex/triangle counts, weights
(<= 4 influences, normalised), height in metres. For each clip FBX: duration against the source
glTF, and per-vertex world positions of the FBX mesh driven by the FBX clip compared with the
original GLB evaluated at the same frames (proves scale, orientation, skinning and poses).
Writes Tools/CharacterRigging/Working/HeraldicKnight/fbx-roundtrip.txt.
"""

from pathlib import Path
import json

import bpy

PROJECT = Path(__file__).resolve().parents[2]
WORK = PROJECT / "Tools" / "CharacterRigging" / "Working" / "HeraldicKnight"
MODELS = PROJECT / "Assets" / "CharacterRigging" / "HeraldicKnight" / "Models"
REPORT = json.loads((WORK / "build-report.json").read_text(encoding="utf-8"))
CLIPS = ("Walking", "Running", "Attack", "Triple_Combo_Attack")
FPS = REPORT["fps"]
TOLERANCE_M = 0.002
lines = []


def log(message):
    print(message)
    lines.append(message)


def world_vertices(mesh_obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh_obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    result = [mesh_obj.matrix_world @ v.co for v in mesh.vertices]
    evaluated.to_mesh_clear()
    return result


def new_objects(before):
    return [o for o in bpy.data.objects if o not in before]


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return new_objects(before)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    failures = 0
    objects = import_fbx(MODELS / "HeraldicKnight.fbx")
    meshes = [o for o in objects if o.type == "MESH"]
    rigs = [o for o in objects if o.type == "ARMATURE"]
    assert len(meshes) == 1 and len(rigs) == 1, f"base FBX objects: {[o.name for o in objects]}"
    body, rig = meshes[0], rigs[0]
    assert len(body.modifiers) == 1 and body.modifiers[0].type == "ARMATURE"
    assert body.find_armature() is rig, "base mesh is not bound to the armature"
    bones = [b.name for b in rig.data.bones]
    assert sorted(bones) == sorted(REPORT["bones"]), f"bones differ: {bones}"
    counts, sums = [], []
    for vertex in body.data.vertices:
        weights = [g.weight for g in vertex.groups if g.weight > 1e-6]
        assert weights, f"vertex {vertex.index} has no weight"
        counts.append(len(weights))
        sums.append(sum(weights))
    triangles = sum(len(p.vertices) - 2 for p in body.data.polygons)
    rest = world_vertices(body)
    height = max(p.z for p in rest) - min(p.z for p in rest)
    ok = (len(body.data.vertices) == REPORT["vertices"] and triangles == REPORT["triangles"]
          and max(counts) <= 4 and min(sums) > 0.99 and max(sums) < 1.01
          and abs(height - REPORT["bindHeightMetres"]) < 0.005
          and tuple(round(s, 4) for s in rig.matrix_world.to_scale()) == (1.0, 1.0, 1.0))
    failures += not ok
    log(f"{'PASS' if ok else 'FAIL'} base HeraldicKnight.fbx: bones={len(bones)}, "
        f"vertices={len(body.data.vertices)} (expected {REPORT['vertices']}), triangles={triangles} "
        f"(expected {REPORT['triangles']}), maxInfluences={max(counts)}, weightSum={min(sums):.4f}..{max(sums):.4f}, "
        f"height={height:.4f} m (expected {REPORT['bindHeightMetres']}), rigWorldScale={tuple(round(s, 4) for s in rig.matrix_world.to_scale())}, "
        f"materials={[m.name for m in body.data.materials]}")

    for clip in CLIPS:
        clip_objects = import_fbx(MODELS / f"HeraldicKnight@{clip}.fbx")
        clip_rig = next(o for o in clip_objects if o.type == "ARMATURE")
        extra = [o.name for o in clip_objects if o is not clip_rig and not (o.type == "MESH" and o.parent is clip_rig)]
        action = clip_rig.animation_data.action
        start, end = action.frame_range
        duration = (end - start) / FPS
        expected = REPORT["clips"][clip]["gltfDurationSeconds"]
        rest_diff = max(max(abs(x - y) for ra, rb in zip(clip_rig.data.bones[b.name].matrix_local, b.matrix_local)
                            for x, y in zip(ra, rb)) for b in rig.data.bones)

        glb_before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(WORK / "source" /
                                  f"Meshy_AI_Heraldic_Knight_biped_Animation_{clip}_withSkin.glb"))
        glb_objects = new_objects(glb_before)
        glb_rig = next(o for o in glb_objects if o.type == "ARMATURE")
        glb_body = next(o for o in glb_objects if o.type == "MESH" and o.parent is glb_rig)
        glb_start = int(round(glb_rig.animation_data.action.frame_range[0]))

        worst, motion, hips_path = 0.0, 0.0, []
        frames = sorted({int(start), int(start + (end - start) / 3), int(start + 2 * (end - start) / 3), int(end)})
        first = None
        for frame in frames:
            bpy.context.scene.frame_set(frame)
            # Copy the clip skeleton's armature-space bone matrices onto the base rig (parents first),
            # which is what an engine does when it plays the clip on the base model's transforms.
            for bone in rig.data.bones:  # Blender lists parents before children
                rig.pose.bones[bone.name].matrix = clip_rig.pose.bones[bone.name].matrix.copy()
                bpy.context.view_layer.update()
            fbx_positions = world_vertices(body)
            bpy.context.scene.frame_set(frame - int(start) + glb_start)
            glb_positions = world_vertices(glb_body)
            worst = max(worst, max((a - b).length for a, b in zip(fbx_positions, glb_positions)))
            if first is None:
                first = fbx_positions
            else:
                motion = max(motion, max((a - b).length for a, b in zip(first, fbx_positions)))
            hips = clip_rig.matrix_world @ clip_rig.pose.bones["mixamorig:Hips"].head
            hips_path.append(tuple(round(x, 3) for x in hips))
        ok = abs(duration - expected) < 0.5 / FPS and worst < TOLERANCE_M and motion > 0.01 and not extra
        failures += not ok
        log(f"{'PASS' if ok else 'FAIL'} clip {clip}: frames={start:.0f}..{end:.0f} duration={duration:.4f}s "
            f"(glTF {expected:.4f}s), maxVertexDeviationVsGLB={worst * 1000:.3f} mm over frames {frames}, "
            f"maxMotionFromFirstSample={motion:.3f} m, clipRestVsBaseRest={rest_diff:.5f}, hipsWorld(m)={hips_path}, extraObjects={extra}")
        for pose_bone in rig.pose.bones:
            pose_bone.matrix_basis.identity()
        for obj in glb_objects + clip_objects:
            bpy.data.objects.remove(obj, do_unlink=True)

    log(f"FBX_ROUNDTRIP {'PASSED' if failures == 0 else 'FAILED'}: failures={failures}")
    (WORK / "fbx-roundtrip.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    if failures:
        raise SystemExit(1)


main()
