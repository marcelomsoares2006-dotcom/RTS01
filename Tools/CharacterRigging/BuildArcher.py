"""Build the Archer character for Unity from the Meshy image-to-3D model + Meshy auto-rig.

Run from the project root:
  blender -b --factory-startup --python-exit-code 1 --python Tools/CharacterRigging/BuildArcher.py

Inputs (Tools/CharacterRigging/Inputs/Archer, read only):
  archer_rigged.glb / walking.glb / running.glb   Meshy rigged character and its native Walk/Run clips (24 fps)
  Mixamo/*.fbx                                    Mixamo archer clips (30 fps) retargeted onto the Meshy skeleton
  tex_normal.png                                  Meshy normal map of the same mesh
Outputs:
  Assets/CharacterRigging/Archer/Models/Archer.fbx            rig + LOD0/LOD1/LOD2 skinned meshes (bind pose)
  Assets/CharacterRigging/Archer/Models/Archer@<Clip>.fbx     one baked clip each (same skeleton)
  Assets/CharacterRigging/Archer/Models/Archer.glb            LOD0 + rig + all clips (portable)
  Assets/CharacterRigging/Archer/Textures/*.png
  Tools/CharacterRigging/Working/Archer/Archer.blend, Docs/CharacterMeshAudit/Archer/build-report.json

The Mixamo clips are retargeted rotation-wise: limbs use absolute world directions (so the A-pose rest of the
Meshy rig versus the T-pose rest of Mixamo does not bend the arms), spine/head use rest-relative rotation.
Facing is -Y in Blender, exported with axis_forward=-Z / axis_up=Y like the other characters (faces +Z in Unity).
"""
from pathlib import Path
import json
import math

import bpy
from mathutils import Vector, Matrix, Quaternion

PROJECT = Path(__file__).resolve().parents[2]
SRC = PROJECT / "Tools/CharacterRigging/Inputs/Archer"
WORK = PROJECT / "Tools/CharacterRigging/Working/Archer"
UNITY = PROJECT / "Assets/CharacterRigging/Archer"
MODELS = UNITY / "Models"
TEXTURES = UNITY / "Textures"
REPORT = PROJECT / "Docs/CharacterMeshAudit/Archer"

SCALE = 0.01          # Meshy glTF rig: centimetres under a 0.01 object scale
RIG_NAME, MAT_NAME = "Archer_Armature", "Archer_Mat"
LOD_RATIOS = {1: 0.30, 2: 0.10}

# (clip name, source file, first frame, last frame, loop)  -- Mixamo clips are 30 fps
MIXAMO_CLIPS = [
    ("Idle", "07-Idle.fbx", 1, 253, True),
    ("Aim", "09-Standing-Aim-Idle-01.fbx", 1, 114, True),
    ("DrawString", "01-Shooting-Arrow.fbx", 1, 97, False),
    ("Fire", "01-Shooting-Arrow.fbx", 109, 151, False),
]
NATIVE_CLIPS = [("Walk", "walking.glb"), ("Run", "running.glb")]  # Meshy native, 24 fps

# source bone, target bone, source child (for absolute direction), target child ('' = bone axis, '^' = parent axis)
# None as source child = rest-relative rotation only.
BONE_MAP = [
    ("Hips", "Hips", None, None), ("Spine", "Spine02", None, None), ("Spine1", "Spine01", None, None),
    ("Spine2", "Spine", None, None), ("Neck", "neck", None, None), ("Head", "Head", None, None),
]
for side in ("Left", "Right"):
    BONE_MAP += [
        (f"{side}Shoulder", f"{side}Shoulder", None, None),
        (f"{side}Arm", f"{side}Arm", f"{side}ForeArm", f"{side}ForeArm"),
        (f"{side}ForeArm", f"{side}ForeArm", f"{side}Hand", f"{side}Hand"),
        (f"{side}Hand", f"{side}Hand", f"{side}HandMiddle1", "^"),
        (f"{side}UpLeg", f"{side}UpLeg", f"{side}Leg", f"{side}Leg"),
        (f"{side}Leg", f"{side}Leg", f"{side}Foot", f"{side}Foot"),
        (f"{side}Foot", f"{side}Foot", f"{side}ToeBase", f"{side}ToeBase"),
        (f"{side}ToeBase", f"{side}ToeBase", f"{side}Toe_End", ""),
    ]


# ----------------------------------------------------------------------------- helpers
def assign_action(rig, action):
    rig.animation_data_create()
    rig.animation_data.action = action
    if action is not None and action.slots:
        rig.animation_data.action_slot = action.slots[0]


def world_vertices(mesh_obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh_obj.evaluated_get(dg)
    mesh = ev.to_mesh()
    m = mesh_obj.matrix_world.copy()
    out = [m @ v.co for v in mesh.vertices]
    ev.to_mesh_clear()
    return out


def rot3(matrix):
    return matrix.to_3x3().normalized()


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    new = [o for o in bpy.data.objects if o not in before]
    rig = next(o for o in new if o.type == "ARMATURE")
    body = next(o for o in new if o.type == "MESH")
    for pb in rig.pose.bones:
        pb.custom_shape = None
    for o in new:
        if o not in (rig, body):
            bpy.data.objects.remove(o, do_unlink=True)
    return rig, body


def same_rest(a_rig, a_body, b_rig, b_body, label):
    assert [b.name for b in a_rig.data.bones] == [b.name for b in b_rig.data.bones], label
    for x, y in zip(a_rig.data.bones, b_rig.data.bones):
        d = max(abs(p - q) for ra, rb in zip(x.matrix_local, y.matrix_local) for p, q in zip(ra, rb))
        assert d < 1e-3, f"{label}: rest differs at {x.name} ({d})"
    assert len(a_body.data.vertices) == len(b_body.data.vertices), label


def convert_to_metres(rig, body, actions):
    for action in actions:
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        if curve.data_path.endswith(".location"):
                            for k in curve.keyframe_points:
                                k.co.y *= SCALE
                                k.handle_left.y *= SCALE
                                k.handle_right.y *= SCALE
    rig.animation_data_create()
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def fix_material(body):
    mat = body.data.materials[0]
    mat.name = MAT_NAME
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    image = next(n.image for n in nodes if n.type == "TEX_IMAGE")
    for l in list(bsdf.inputs["Emission Color"].links):
        links.remove(l)
    bsdf.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    bsdf.inputs["Emission Strength"].default_value = 0.0
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.75
    TEXTURES.mkdir(parents=True, exist_ok=True)
    target = TEXTURES / "Archer_BaseColor.png"
    image.name = "Archer_BaseColor"
    image.filepath_raw = str(target)
    image.file_format = "PNG"
    image.save()
    (TEXTURES / "Archer_Normal.png").write_bytes((SRC / "tex_normal.png").read_bytes())
    return list(image.size)


# ----------------------------------------------------------------------------- retargeting
def ordered_bones(rig):
    out, seen = [], set()
    def visit(b):
        if b.name in seen:
            return
        if b.parent:
            visit(b.parent)
        seen.add(b.name)
        out.append(b)
    for b in rig.data.bones:
        visit(b)
    return out


def retarget(src_rig, dst_rig, name, f0, f1):
    """Bake src frames f0..f1 into a new action on dst_rig (target frame = src frame - f0 + 1)."""
    scene = bpy.context.scene
    s_pref = "mixamorig:"
    mapping = [(s_pref + s, t, (s_pref + sc) if sc else None, tc) for s, t, sc, tc in BONE_MAP]
    tmap = {t: (s, sc, tc) for s, t, sc, tc in mapping}
    dbones = ordered_bones(dst_rig)
    Tw = rot3(dst_rig.matrix_world)
    rest = {b.name: b.matrix_local.copy() for b in dbones}
    Sw = src_rig.matrix_world
    src_rest = {b.name: Sw @ b.matrix_local for b in src_rig.data.bones}

    t_dir0, t_rest_rot = {}, {}
    for t, (s, sc, tc) in tmap.items():
        b = dst_rig.data.bones[t]
        t_rest_rot[t] = Tw @ rot3(rest[t])
        if sc is None:
            continue
        if tc == "^":
            v = rest[t].translation - rest[b.parent.name].translation
        elif tc == "":
            v = Vector(b.tail_local) - Vector(b.head_local)
            v = rest[t].to_3x3() @ Vector((0, (b.tail_local - b.head_local).length, 0))
        else:
            v = rest[tc].translation - rest[t].translation
        t_dir0[t] = (Tw @ v).normalized()

    hips_scale = rest["Hips"].translation.z / src_rest[s_pref + "Hips"].translation.z
    hips_rest_src = src_rest[s_pref + "Hips"].translation.copy()

    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    assign_action(dst_rig, action)
    for pb in dst_rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    prev_q = {}
    for src_frame in range(f0, f1 + 1):
        scene.frame_set(src_frame)
        dst_frame = src_frame - f0 + 1
        spose = {b.name: Sw @ src_rig.pose.bones[b.name].matrix for b in src_rig.data.bones}
        pose_arm = {}
        for b in dbones:
            if b.parent:
                rel = rest[b.parent.name].inverted() @ rest[b.name]
                nat = pose_arm[b.parent.name] @ rel
            else:
                nat = rest[b.name]
            pose = nat
            if b.name in tmap:
                s, sc, tc = tmap[b.name]
                dw = rot3(spose[s]) @ rot3(src_rest[s]).inverted()
                r = dw @ t_rest_rot[b.name]
                if sc is not None:
                    a = t_rest_rot[b.name].inverted() @ t_dir0[b.name]
                    d1 = r @ a
                    d_src = (spose[sc].translation - spose[s].translation).normalized()
                    r = d1.rotation_difference(d_src).to_matrix() @ r
                rot_arm = Tw.inverted() @ r
                if b.parent:
                    pos = nat.translation
                else:
                    pos = rest[b.name].translation + (spose[s].translation - hips_rest_src) * hips_scale
                pose = Matrix.Translation(pos) @ rot_arm.to_4x4()
            pose_arm[b.name] = pose
            basis = nat.inverted() @ pose
            pb = dst_rig.pose.bones[b.name]
            q = basis.to_quaternion()
            if b.name in prev_q and q.dot(prev_q[b.name]) < 0:
                q.negate()
            prev_q[b.name] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=dst_frame)
            if not b.parent:
                pb.location = basis.to_translation()
                pb.keyframe_insert("location", frame=dst_frame)
    return action


def retarget_error(src_rig, dst_rig, action, f0, f1, frames):
    """Hand-vs-head world offsets of the retargeted rig compared with the source (scaled by height)."""
    s_pref = "mixamorig:"
    Sw = src_rig.matrix_world
    assign_action(dst_rig, action)
    ratio = (dst_rig.matrix_world @ dst_rig.data.bones["Head"].head_local).z / \
            (Sw @ src_rig.data.bones[s_pref + "Head"].head_local).z
    worst = 0.0
    names = ("LeftHand", "RightHand", "LeftFoot", "RightFoot")
    for fr in frames:
        # source and target actions live on different time lines (target starts at frame 1)
        assign_action(src_rig, src_rig.animation_data.action)
        bpy.context.scene.frame_set(f0 + fr)
        hips_s = Sw @ src_rig.pose.bones[s_pref + "Hips"].head
        sp = {n: ((Sw @ src_rig.pose.bones[s_pref + n].head) - hips_s) * ratio for n in names}
        bpy.context.scene.frame_set(1 + fr)
        hips_t = dst_rig.matrix_world @ dst_rig.pose.bones["Hips"].head
        for n in names:
            tp = (dst_rig.matrix_world @ dst_rig.pose.bones[n].head) - hips_t
            worst = max(worst, (sp[n] - tp).length)
    return worst


# ----------------------------------------------------------------------------- export
def export_fbx(path, objects, action, fps):
    scene = bpy.context.scene
    scene.render.fps = fps
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    rig = next(o for o in objects if o.type == "ARMATURE")
    bpy.context.view_layer.objects.active = rig
    assign_action(rig, action)
    if action is not None:
        scene.frame_start, scene.frame_end = (int(round(x)) for x in action.frame_range)
    else:
        scene.frame_start = scene.frame_end = 0
        for pb in rig.pose.bones:
            pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    scene.frame_set(scene.frame_start)
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"ARMATURE", "MESH"},
        use_armature_deform_only=False, add_leaf_bones=False, axis_forward="-Z", axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", use_tspace=False, path_mode="STRIP",
        embed_textures=False, bake_anim=action is not None, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0)


def clip_stats(rig, body, action, fps):
    assign_action(rig, action)
    s, e = (int(round(x)) for x in action.frame_range)
    frames = sorted({s, s + (e - s) // 3, s + 2 * (e - s) // 3, e})
    bpy.context.scene.frame_set(0)
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion = (0, 0, 0), (1, 0, 0, 0)
    ref = None
    maxd = 0.0
    zs = []
    for f in frames:
        bpy.context.scene.frame_set(f)
        v = world_vertices(body)
        zs += [p.z for p in v]
        if ref is None:
            ref = v
        maxd = max(maxd, max((a - b).length for a, b in zip(ref, v)))
    return {"frames": [s, e], "seconds": round((e - s + 1) / fps if False else (e - s) / fps, 4), "fps": fps,
            "maxVertexDisplacementM": round(maxd, 4), "minZ": round(min(zs), 4), "maxZ": round(max(zs), 4)}


def main():
    for d in (MODELS, TEXTURES, WORK, REPORT):
        d.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    report = {"license": {}, "clips": {}}

    base_rig, base_body = import_glb(SRC / "archer_rigged.glb")
    base_rig.name = base_rig.data.name = RIG_NAME
    base_body.name = base_body.data.name = "Archer_LOD0"

    native = {}
    for clip, fn in NATIVE_CLIPS:
        rig, body = import_glb(SRC / fn)
        same_rest(base_rig, base_body, rig, body, clip)
        act = rig.animation_data.action
        act.name = clip
        act.use_fake_user = True
        native[clip] = act
        md, ad = body.data, rig.data
        bpy.data.objects.remove(body, do_unlink=True)
        bpy.data.objects.remove(rig, do_unlink=True)
        bpy.data.meshes.remove(md)
        bpy.data.armatures.remove(ad)

    convert_to_metres(base_rig, base_body, list(native.values()))
    assert tuple(base_rig.scale) == (1, 1, 1) and tuple(base_body.scale) == (1, 1, 1)
    report["textureSize"] = fix_material(base_body)
    base_rig.data.display_type = "OCTAHEDRAL"
    bpy.context.view_layer.update()

    # --- retarget Mixamo clips onto the Meshy skeleton (mesh hidden to keep evaluation cheap)
    base_body.hide_viewport = True
    actions = {}
    src_cache = {}
    for clip, fn, f0, f1, loop in MIXAMO_CLIPS:
        if fn not in src_cache:
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=str(SRC / "Mixamo" / fn))
            new = [o for o in bpy.data.objects if o not in before]
            rig = next(o for o in new if o.type == "ARMATURE")
            src_cache[fn] = (rig, new)
            assign_action(rig, rig.animation_data.action)
            for o in new:
                if o.type == "MESH":
                    o.hide_viewport = True
        src_rig, _ = src_cache[fn]
        actions[clip] = retarget(src_rig, base_rig, clip, f0, f1)
        err = retarget_error(src_rig, base_rig, actions[clip], f0, f1, [0, (f1 - f0) // 2, f1 - f0])
        report["clips"][clip] = {"source": fn, "sourceFrames": [f0, f1], "loop": loop, "retargetMaxHandFootErrorM": round(err, 4)}
    for clip, act in native.items():
        actions[clip] = act
        report["clips"][clip] = {"source": "Meshy native", "loop": True}
    for _, objs in src_cache.values():
        for o in objs:
            data = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if data is not None and data.users == 0:
                (bpy.data.meshes if isinstance(data, bpy.types.Mesh) else bpy.data.armatures).remove(data)
    for img in list(bpy.data.images):
        if img.users == 0 and img.name != "Archer_BaseColor":
            bpy.data.images.remove(img)
    for mat in list(bpy.data.materials):
        if mat.users == 0:
            bpy.data.materials.remove(mat)
    base_body.hide_viewport = False

    # --- bind sanity + stats
    assign_action(base_rig, None)
    for pb in base_rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    bpy.context.view_layer.update()
    counts, sums = [], []
    for v in base_body.data.vertices:
        w = [g.weight for g in v.groups if g.weight > 1e-6]
        counts.append(len(w))
        sums.append(sum(w))
    bind = world_vertices(base_body)
    report.update({"bones": [b.name for b in base_rig.data.bones], "vertices": len(base_body.data.vertices),
                   "trianglesLOD0": tris(base_body), "maxInfluences": max(counts), "minWeightSum": round(min(sums), 5),
                   "maxWeightSum": round(max(sums), 5), "bindHeightM": round(max(p.z for p in bind) - min(p.z for p in bind), 4),
                   "bindMinZ": round(min(p.z for p in bind), 4)})
    assert min(counts) >= 1, "vertex without weights"

    # --- LODs (decimated copies skinned to the same rig)
    bodies = {0: base_body}
    for lod, ratio in LOD_RATIOS.items():
        copy = base_body.copy()
        copy.data = base_body.data.copy()
        copy.name = copy.data.name = f"Archer_LOD{lod}"
        bpy.context.scene.collection.objects.link(copy)
        copy.parent = base_rig
        copy.matrix_parent_inverse = base_body.matrix_parent_inverse.copy()
        for m in copy.modifiers:
            pass
        bpy.ops.object.select_all(action="DESELECT")
        copy.select_set(True)
        bpy.context.view_layer.objects.active = copy
        tri = copy.modifiers.new("t", "TRIANGULATE")
        bpy.ops.object.modifier_apply(modifier=tri.name)
        dec = copy.modifiers.new("RTS_LOD", "DECIMATE")
        dec.ratio = ratio
        dec.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=dec.name)
        bpy.ops.object.vertex_group_limit_total(limit=4)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
        # re-attach armature modifier if the copy lost it
        if not any(m.type == "ARMATURE" for m in copy.modifiers):
            mod = copy.modifiers.new("Armature", "ARMATURE")
            mod.object = base_rig
        bodies[lod] = copy
        report[f"trianglesLOD{lod}"] = tris(copy)
    for lod, o in bodies.items():
        for p in o.data.polygons:
            p.use_smooth = True

    # --- export
    export_fbx(MODELS / "Archer.fbx", [base_rig] + list(bodies.values()), None, 30)
    for clip, act in actions.items():
        fps = 24 if clip in dict(NATIVE_CLIPS) else 30
        report["clips"][clip].update(clip_stats(base_rig, base_body, act, fps))
        # the clip files carry the smallest mesh only to keep the hierarchy; name it so Unity does not warn about LOD naming
        bodies[2].name = "Archer_ClipMesh"
        export_fbx(MODELS / f"Archer@{clip}.fbx", [base_rig, bodies[2]], act, fps)
        bodies[2].name = "Archer_LOD2"

    # GLB: LOD0 + rig + every clip (portable copy for other engines / DCCs)
    scene.render.fps = 30
    assign_action(base_rig, None)
    for pb in base_rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    for o in bodies.values():
        o.select_set(False)
    bpy.ops.object.select_all(action="DESELECT")
    base_rig.select_set(True)
    base_body.select_set(True)
    for lod in (1, 2):
        bodies[lod].hide_viewport = True
        bodies[lod].hide_render = True
    # park each action in an NLA track so the glTF exporter writes them all
    base_rig.animation_data_create()
    for clip, act in actions.items():
        track = base_rig.animation_data.nla_tracks.new()
        track.name = clip
        strip = track.strips.new(clip, 1, act)
        strip.name = clip
    base_rig.animation_data.action = None
    bpy.context.view_layer.objects.active = base_rig
    bpy.ops.export_scene.gltf(filepath=str(MODELS / "Archer.glb"), export_format="GLB", use_selection=True,
                              export_animations=True, export_animation_mode="NLA_TRACKS", export_yup=True,
                              export_apply=False)
    for lod in (1, 2):
        bodies[lod].hide_viewport = False
        bodies[lod].hide_render = False

    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "Archer.blend"), compress=True)
    (REPORT / "build-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("BUILD_ARCHER_OK", json.dumps({k: v for k, v in report.items() if k not in ("clips", "bones")}))
    print("CLIPS", json.dumps(report["clips"]))


main()
