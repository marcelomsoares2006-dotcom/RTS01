"""Build the Heraldic Knight Unity assets from the Meshy biped ZIP (Etapa 1).

Run from the project root:
  blender -b --factory-startup --python Tools/CharacterRigging/BuildHeraldicKnight.py

What it does (the input ZIP is only read):
  1. Extracts copies of the four GLBs to Tools/CharacterRigging/Working/HeraldicKnight/source.
  2. Imports them, removes importer helper objects (bone display "Icosphere"), checks that the
     four files share the same rest skeleton and bind mesh, and keeps one rig + four actions.
  3. Converts the rig from centimetres (armature scale 0.01) to metres without changing poses:
     applies the object scale and scales every bone location curve by the same factor.
  4. Replaces the glTF material (metallic 1 by glTF default, emissive = base colour) with a plain
     base-colour material and writes the embedded texture as a PNG.
  5. Exports HeraldicKnight.fbx (mesh + rig, bind pose) and HeraldicKnight@<Clip>.fbx
     (same hierarchy, one baked clip each) to Assets/CharacterRigging/HeraldicKnight/Models.
  6. Saves an editable .blend outside Assets and a JSON report used by VerifyHeraldicKnightFbx.py.
"""

from pathlib import Path
import json
import math
import zipfile

import bpy
from mathutils import Vector

PROJECT = Path(__file__).resolve().parents[2]
ZIP = PROJECT / "Tools" / "CharacterRigging" / "Inputs" / "Meshy_AI_Heraldic_Knight_biped.zip"
WORK = PROJECT / "Tools" / "CharacterRigging" / "Working" / "HeraldicKnight"
SOURCE = WORK / "source"
UNITY = PROJECT / "Assets" / "CharacterRigging" / "HeraldicKnight"
MODELS = UNITY / "Models"
TEXTURES = UNITY / "Textures"

# Order matters only for reporting; Walking is used as the base rig/mesh.
CLIPS = ("Walking", "Running", "Attack", "Triple_Combo_Attack")
RIG_NAME = "HeraldicKnight_Armature"
MESH_NAME = "HeraldicKnight_Body"
MATERIAL_NAME = "HeraldicKnight_Mat"
TEXTURE_FILE = "HeraldicKnight_BaseColor.png"
SCALE = 0.01  # glTF rig is authored in centimetres under a 0.01 object scale
FPS = 24


def glb_path(clip):
    return SOURCE / f"Meshy_AI_Heraldic_Knight_biped_Animation_{clip}_withSkin.glb"


def extract_sources():
    SOURCE.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(ZIP) as archive:
        for clip in CLIPS:
            member = f"Meshy_AI_Heraldic_Knight_biped/{glb_path(clip).name}"
            glb_path(clip).write_bytes(archive.read(member))


def gltf_duration(path):
    import struct
    with open(path, "rb") as handle:
        handle.read(12)
        length, _ = struct.unpack("<II", handle.read(8))
        data = json.loads(handle.read(length))
    animation = data["animations"][0]
    return max(data["accessors"][s["input"]]["max"][0] for s in animation["samplers"])


def world_vertices(mesh_obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh_obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    matrix = mesh_obj.matrix_world.copy()
    result = [matrix @ v.co for v in mesh.vertices]
    evaluated.to_mesh_clear()
    return result


def assign_action(rig, action):
    """Blender 5 layered actions: the slot must be chosen explicitly or the rig stays static."""
    rig.animation_data.action = action
    if action is not None:
        rig.animation_data.action_slot = action.slots[0]


def import_clip(clip):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(glb_path(clip)))
    new = [o for o in bpy.data.objects if o not in before]
    rig = next(o for o in new if o.type == "ARMATURE")
    body = next(o for o in new if o.type == "MESH" and o.parent is rig)
    # Helper objects created by the importer only to display bones.
    helpers = [o for o in new if o not in (rig, body)]
    for pose_bone in rig.pose.bones:
        pose_bone.custom_shape = None
    for helper in helpers:
        bpy.data.objects.remove(helper, do_unlink=True)
    action = rig.animation_data.action
    action.name = clip
    return rig, body, action


def same_rest(base_rig, base_body, rig, body, clip):
    assert [b.name for b in rig.data.bones] == [b.name for b in base_rig.data.bones], clip
    for a, b in zip(base_rig.data.bones, rig.data.bones):
        diff = max(abs(x - y) for ra, rb in zip(a.matrix_local, b.matrix_local) for x, y in zip(ra, rb))
        assert diff < 1e-4, f"{clip}: rest pose differs at {a.name} ({diff})"
    assert len(body.data.vertices) == len(base_body.data.vertices), clip
    diff = max((a.co - b.co).length for a, b in zip(base_body.data.vertices, body.data.vertices))
    assert diff < 1e-4, f"{clip}: bind mesh differs ({diff})"


def sample_frames(action):
    start, end = (int(round(x)) for x in action.frame_range)
    return [start, start + (end - start) // 3, start + 2 * (end - start) // 3, end]


def snapshot(rig, body, action):
    """World-space vertex positions at four frames, used to prove the unit change is lossless."""
    assign_action(rig, action)
    result = {}
    for frame in sample_frames(action):
        bpy.context.scene.frame_set(frame)
        result[frame] = world_vertices(body)
    return result


def convert_to_metres(rig, body, actions):
    for obj in (rig, body):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.context.scene.frame_set(0)
    for action in actions:
        # Blender 5 layered actions: walk every channelbag of every layer/strip.
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        if curve.data_path.endswith(".location"):
                            for key in curve.keyframe_points:
                                key.co.y *= SCALE
                                key.handle_left.y *= SCALE
                                key.handle_right.y *= SCALE
    rig.animation_data.action = None
    for pose_bone in rig.pose.bones:
        pose_bone.location = (0, 0, 0)
        pose_bone.rotation_quaternion = (1, 0, 0, 0)
        pose_bone.scale = (1, 1, 1)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def fix_material(body):
    material = body.data.materials[0]
    material.name = MATERIAL_NAME
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    image = next(n.image for n in nodes if n.type == "TEX_IMAGE")
    for link in list(bsdf.inputs["Emission Color"].links):
        links.remove(link)
    bsdf.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    bsdf.inputs["Emission Strength"].default_value = 0.0
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.75
    for node in [n for n in nodes if n.type == "TEX_IMAGE" and not n.outputs[0].links]:
        nodes.remove(node)
    TEXTURES.mkdir(parents=True, exist_ok=True)
    image.name = "HeraldicKnight_BaseColor"
    target = TEXTURES / TEXTURE_FILE
    if image.packed_file is not None:
        target.write_bytes(image.packed_file.data)
    else:
        image.save(filepath=str(target))
    # Keep the .blend self-contained (packed) but point it at the exported file too.
    image.filepath = str(target)
    return image.size[:]


def export(path, objects, action):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    rig = next(o for o in objects if o.type == "ARMATURE")
    bpy.context.view_layer.objects.active = rig
    assign_action(rig, action)
    scene = bpy.context.scene
    if action is not None:
        scene.frame_start, scene.frame_end = (int(round(x)) for x in action.frame_range)
    else:
        scene.frame_start = scene.frame_end = 0
        for pose_bone in rig.pose.bones:
            pose_bone.location = (0, 0, 0)
            pose_bone.rotation_quaternion = (1, 0, 0, 0)
            pose_bone.scale = (1, 1, 1)
    scene.frame_set(scene.frame_start)
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_armature_deform_only=False, add_leaf_bones=False,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE", use_tspace=False, path_mode="STRIP", embed_textures=False,
        bake_anim=action is not None, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    )


def main():
    extract_sources()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    report = {"source": str(ZIP.relative_to(PROJECT)), "fps": FPS, "clips": {}}

    base_rig = base_body = None
    actions = {}
    for clip in CLIPS:
        rig, body, action = import_clip(clip)
        action.use_fake_user = True
        actions[clip] = action
        report["clips"][clip] = {"gltfDurationSeconds": round(gltf_duration(glb_path(clip)), 5),
                                 "blenderFrameRange": list(action.frame_range)}
        if base_rig is None:
            base_rig, base_body = rig, body
            continue
        same_rest(base_rig, base_body, rig, body, clip)
        mesh_data, arm_data = body.data, rig.data
        bpy.data.objects.remove(body, do_unlink=True)
        bpy.data.objects.remove(rig, do_unlink=True)
        bpy.data.meshes.remove(mesh_data)
        bpy.data.armatures.remove(arm_data)
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        if block.users == 0:
            bpy.data.materials.remove(block)
    for block in list(bpy.data.images):
        if block.users == 0:
            bpy.data.images.remove(block)

    base_rig.name = base_rig.data.name = RIG_NAME
    base_body.name = base_body.data.name = MESH_NAME

    before = {clip: snapshot(base_rig, base_body, action) for clip, action in actions.items()}
    convert_to_metres(base_rig, base_body, actions.values())
    worst = 0.0
    for clip, action in actions.items():
        after = snapshot(base_rig, base_body, action)
        for frame, positions in before[clip].items():
            delta = max((a - b).length for a, b in zip(positions, after[frame]))
            worst = max(worst, delta)
        report["clips"][clip]["worldSamples"] = {
            str(frame): {
                "minZ": round(min(p.z for p in pos), 4), "maxZ": round(max(p.z for p in pos), 4),
                "centroid": [round(sum(p[i] for p in pos) / len(pos), 4) for i in range(3)],
            } for frame, pos in after.items()}
        report["clips"][clip]["sampleFrames"] = list(after)
    for clip in actions:
        centroids = [s["centroid"] for s in report["clips"][clip]["worldSamples"].values()]
        spread = max(max(abs(a[i] - b[i]) for i in range(3)) for a in centroids for b in centroids)
        heights = [s["maxZ"] for s in report["clips"][clip]["worldSamples"].values()]
        assert spread > 1e-3 or max(heights) - min(heights) > 1e-3, f"{clip}: action does not animate the rig"
    assert worst < 1e-4, f"unit conversion changed world positions by {worst} m"
    report["unitConversionMaxDeltaMetres"] = worst
    assert tuple(base_rig.scale) == (1, 1, 1) and tuple(base_body.scale) == (1, 1, 1)

    report["textureSize"] = list(fix_material(base_body))
    base_rig.animation_data.action = None
    base_rig.data.display_type = "OCTAHEDRAL"
    for pose_bone in base_rig.pose.bones:
        pose_bone.location = (0, 0, 0)
        pose_bone.rotation_quaternion = (1, 0, 0, 0)
        pose_bone.scale = (1, 1, 1)
    bpy.context.view_layer.update()

    counts, sums = [], []
    for vertex in base_body.data.vertices:
        weights = [g.weight for g in vertex.groups if g.weight > 1e-6]
        counts.append(len(weights))
        sums.append(sum(weights))
    bind = world_vertices(base_body)
    report.update({
        "bones": [b.name for b in base_rig.data.bones],
        "vertices": len(base_body.data.vertices),
        "triangles": sum(len(p.vertices) - 2 for p in base_body.data.polygons),
        "maxInfluences": max(counts), "minWeightSum": min(sums), "maxWeightSum": max(sums),
        "bindHeightMetres": round(max(p.z for p in bind) - min(p.z for p in bind), 4),
        "bindMinZ": round(min(p.z for p in bind), 4),
    })

    MODELS.mkdir(parents=True, exist_ok=True)
    export(MODELS / "HeraldicKnight.fbx", [base_rig, base_body], None)
    for clip, action in actions.items():
        # The mesh is exported with each clip so the FBX root/hierarchy is identical to the base model
        # (Unity collapses a single-root armature-only file, which breaks "Copy From Other Avatar").
        export(MODELS / f"HeraldicKnight@{clip}.fbx", [base_rig, base_body], action)
        start, end = action.frame_range
        report["clips"][clip]["exportedDurationSeconds"] = round((end - start) / FPS, 5)
    assign_action(base_rig, actions["Walking"])
    scene.frame_start, scene.frame_end = (int(round(x)) for x in actions["Walking"].frame_range)

    WORK.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "HeraldicKnight.blend"), compress=True)
    (WORK / "build-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("BUILD_HERALDIC_OK", json.dumps({k: v for k, v in report.items() if k != "clips"}))


main()
