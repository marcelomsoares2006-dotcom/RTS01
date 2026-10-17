"""Constroi o corpo do Paladino curador (paladin_healer) para o Unity a partir do modelo Meshy image-to-3D + auto-rig.

Executar na raiz do projeto:
  blender -b --factory-startup --python-exit-code 1 --python Tools/CharacterRigging/BuildPaladinHealer.py

Entradas (Tools/CharacterRigging/Inputs/PaladinHealer, somente leitura):
  paladinhealer_rigged.glb / walking.glb / running.glb   personagem Meshy com rig e clipes nativos Walk/Run (24 fps)
  Mixamo/*.fbx                                           clipes Mixamo (30 fps) retargetados para o esqueleto Meshy
  tex_normal.png                                         normal map da mesma malha
Saidas:
  Assets/CharacterRigging/PaladinHealer/Models/PaladinHealer.fbx            rig + LOD0/LOD1/LOD2 (pose de ligacao)
  Assets/CharacterRigging/PaladinHealer/Models/PaladinHealer@<Clip>.fbx     um clipe baked por arquivo (mesmo esqueleto)
  Assets/CharacterRigging/PaladinHealer/Models/PaladinHealer.glb            LOD0 + rig + todos os clipes (copia portatil)
  Assets/CharacterRigging/PaladinHealer/Textures/*.png                       base, normal e metalico/suavidade
  Tools/CharacterRigging/Working/PaladinHealer/PaladinHealer.blend, Docs/CharacterMeshAudit/PaladinHealer/build-report.json

Clipes: Idle, Attack, Hit, Death vem do Mixamo (retarget por direcao absoluta dos membros; a pose A do rig Meshy contra a
pose T do Mixamo nao dobra os bracos). Walk e Run sao os nativos da Meshy. Heal e criado aqui, em Blender, sobre o
esqueleto Meshy: parte da pose do Idle, ergue os dois bracos, sustenta e volta (comeca e termina na pose do Idle).
Frente = -Y no Blender, exportado com axis_forward=-Z / axis_up=Y (olha para +Z no Unity).
"""
from pathlib import Path
import json
import math

import bpy
import numpy as np
from mathutils import Vector, Matrix, Quaternion

PROJECT = Path(__file__).resolve().parents[2]
SRC = PROJECT / "Tools/CharacterRigging/Inputs/PaladinHealer"
WORK = PROJECT / "Tools/CharacterRigging/Working/PaladinHealer"
UNITY = PROJECT / "Assets/CharacterRigging/PaladinHealer"
MODELS = UNITY / "Models"
TEXTURES = UNITY / "Textures"
REPORT = PROJECT / "Docs/CharacterMeshAudit/PaladinHealer"

SCALE = 0.01          # rig glTF da Meshy: centimetros sob escala de objeto 0.01
RIG_NAME, MAT_NAME = "PaladinHealer_Armature", "PaladinHealer_Mat"
# O tabardo tem paineis finos e triangulos alongados. A decimacao de 10% fazia
# o LOD2 abrir vazios visiveis em close-up; 20% preserva a superficie e ainda
# mantem a queda estrita de triangulos entre os LODs.
LOD_RATIOS = {1: 0.30, 2: 0.20}
METAL_SCALE, SMOOTH_SCALE = 0.30, 0.70   # a textura Meshy e muito brilhante; aplicamos escala ao mapa metalico/suavidade

# (clipe, arquivo-fonte, primeiro quadro, ultimo quadro, loop) -- clipes Mixamo a 30 fps
MIXAMO_CLIPS = [
    ("Idle", "SwordShield-Idle.fbx", 1, 77, True),
    ("Attack", "SwordShield-Slash.fbx", 1, 46, False),
    ("Hit", "React-Large-From-Left.fbx", 1, 32, False),
    ("Death", "Dying.fbx", 1, 174, False),
]
NATIVE_CLIPS = [("Walk", "walking.glb"), ("Run", "running.glb")]  # Meshy nativos, 24 fps
HEAL_FRAMES = 38  # 1,27 s a 30 fps

# osso fonte, osso alvo, filho fonte (direcao absoluta), filho alvo ('' = eixo do osso, '^' = eixo do pai)
# None como filho fonte = apenas rotacao relativa ao repouso.
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
    body = next(o for o in new if o.type == "MESH" and len(o.data.vertices) > 1000)
    for pb in rig.pose.bones:
        pb.custom_shape = None
    for o in new:
        if o not in (rig, body):
            data = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if data is not None and data.users == 0 and isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
    return rig, body


def same_rest(a_rig, a_body, b_rig, b_body, label):
    assert [b.name for b in a_rig.data.bones] == [b.name for b in b_rig.data.bones], label
    for x, y in zip(a_rig.data.bones, b_rig.data.bones):
        d = max(abs(p - q) for ra, rb in zip(x.matrix_local, y.matrix_local) for p, q in zip(ra, rb))
        assert d < 1e-3, f"{label}: repouso difere em {x.name} ({d})"
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


def image_from(nodes, socket_owner, socket_name):
    """Imagem ligada (direta ou por Separate Color / Normal Map) a uma entrada do Principled BSDF."""
    link = socket_owner.inputs[socket_name].links[0]
    node = link.from_node
    while node.type != "TEX_IMAGE":
        node = node.inputs[0].links[0].from_node
    return node.image


def fix_material(body):
    mat = body.data.materials[0]
    mat.name = MAT_NAME
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    base = image_from(nodes, bsdf, "Base Color")
    orm = image_from(nodes, bsdf, "Metallic")   # G = rugosidade, B = metalico (glTF)
    for l in list(bsdf.inputs["Emission Color"].links):
        links.remove(l)
    bsdf.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    bsdf.inputs["Emission Strength"].default_value = 0.0
    TEXTURES.mkdir(parents=True, exist_ok=True)
    base.name = "PaladinHealer_BaseColor"
    base.filepath_raw = str(TEXTURES / "PaladinHealer_BaseColor.png")
    base.file_format = "PNG"
    base.save()
    (TEXTURES / "PaladinHealer_Normal.png").write_bytes((SRC / "tex_normal.png").read_bytes())

    # mapa metalico/suavidade no formato do Standard do Unity: R = metalico, A = suavidade
    w, h = orm.size
    px = np.empty(w * h * 4, dtype=np.float32)
    orm.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    metal = np.clip(px[:, :, 2] * METAL_SCALE, 0.0, 1.0)
    smooth = np.clip((1.0 - px[:, :, 1]) * SMOOTH_SCALE, 0.0, 1.0)
    out = np.zeros((h, w, 4), dtype=np.float32)
    out[:, :, 0] = metal
    out[:, :, 1] = metal
    out[:, :, 2] = metal
    out[:, :, 3] = smooth
    img = bpy.data.images.new("PaladinHealer_MetallicSmooth", w, h, alpha=True, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    img.alpha_mode = "STRAIGHT"
    img.pixels.foreach_set(out.ravel())
    img.filepath_raw = str(TEXTURES / "PaladinHealer_MetallicSmooth.png")
    img.file_format = "PNG"
    img.save()
    stats = {"metalMean": round(float(metal.mean()), 4), "metalMax": round(float(metal.max()), 4),
             "smoothMean": round(float(smooth.mean()), 4)}
    bpy.data.images.remove(img)
    return list(base.size), stats


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
    """Grava os quadros f0..f1 da fonte numa nova action em dst_rig (quadro alvo = quadro fonte - f0 + 1)."""
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
    """Deslocamentos mao/pe em relacao ao quadril, do rig retargetado contra a fonte (escalados pela altura)."""
    s_pref = "mixamorig:"
    Sw = src_rig.matrix_world
    assign_action(dst_rig, action)
    ratio = (dst_rig.matrix_world @ dst_rig.data.bones["Head"].head_local).z / \
            (Sw @ src_rig.data.bones[s_pref + "Head"].head_local).z
    worst = 0.0
    names = ("LeftHand", "RightHand", "LeftFoot", "RightFoot")
    for fr in frames:
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


# ----------------------------------------------------------------------------- Heal (clipe criado em Blender)
def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def heal_weight(frame):
    """Envelope 0..1: sobe (1-11), sustenta com leve pulsacao (11-27), desce (27-38)."""
    if frame <= 11:
        return smooth((frame - 1) / 10.0)
    if frame <= 27:
        return 1.0 + 0.03 * math.sin((frame - 11) / 16.0 * math.pi * 2.0)
    return smooth((HEAL_FRAMES - frame) / 11.0)


# direcoes-alvo no espaco do armature (Blender: +Z cima, frente = -Y, direita do personagem = -X)
HEAL_TARGETS = {
    "RightArm": Vector((-0.30, -0.35, 0.89)),
    "RightForeArm": Vector((-0.12, -0.30, 0.95)),
    "RightHand": Vector((-0.05, -0.25, 0.97)),
}
HEAL_PITCH = {"Spine01": -7.0, "Spine": -5.0, "neck": -4.0, "Head": -7.0}  # graus; negativo = inclina para tras / olha para cima


def build_heal(rig, idle_action):
    """Action 'Heal' (30 fps): parte do quadro 1 do Idle, ergue os dois bracos e volta."""
    assign_action(rig, idle_action)
    bpy.context.scene.frame_set(1)
    dbones = ordered_bones(rig)
    rest = {b.name: b.matrix_local.copy() for b in dbones}
    base_q = {pb.name: pb.rotation_quaternion.copy() for pb in rig.pose.bones}
    base_loc = rig.pose.bones["Hips"].location.copy()

    def solve(w, targets):
        pose_arm = {}
        for b in dbones:
            if b.parent:
                nat = pose_arm[b.parent.name] @ (rest[b.parent.name].inverted() @ rest[b.name])
            else:
                nat = rest[b.name]
            basis = base_q[b.name].to_matrix().to_4x4()
            if not b.parent:
                basis.translation = base_loc
            pose = nat @ basis
            if targets and b.name in HEAL_TARGETS and w > 0:
                cur = rot3(pose) @ Vector((0, 1, 0))
                tgt = HEAL_TARGETS[b.name].normalized()
                d = (cur * (1 - w) + tgt * w).normalized()
                pose = Matrix.Translation(pose.translation) @ (cur.rotation_difference(d).to_matrix() @ rot3(pose)).to_4x4()
            if targets and b.name in HEAL_PITCH and w > 0:
                ang = math.radians(HEAL_PITCH[b.name]) * w
                R = Matrix.Rotation(ang, 3, "X")
                pose = Matrix.Translation(pose.translation) @ (R @ rot3(pose)).to_4x4()
            pose_arm[b.name] = pose
        return pose_arm

    action = bpy.data.actions.new("Heal")
    action.use_fake_user = True
    assign_action(rig, action)
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    prev_q = {}
    for frame in range(1, HEAL_FRAMES + 1):
        w = heal_weight(frame)
        pose_arm = solve(w, True)
        for b in dbones:
            if b.parent:
                nat = pose_arm[b.parent.name] @ (rest[b.parent.name].inverted() @ rest[b.name])
            else:
                nat = rest[b.name]
            basis = nat.inverted() @ pose_arm[b.name]
            pb = rig.pose.bones[b.name]
            q = basis.to_quaternion()
            if b.name in prev_q and q.dot(prev_q[b.name]) < 0:
                q.negate()
            prev_q[b.name] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            if not b.parent:
                pb.location = basis.to_translation()
                pb.keyframe_insert("location", frame=frame)
    return action


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
    return {"frames": [s, e], "seconds": round((e - s) / fps, 4), "fps": fps,
            "maxVertexDisplacementM": round(maxd, 4), "minZ": round(min(zs), 4), "maxZ": round(max(zs), 4)}


def main():
    for d in (MODELS, TEXTURES, WORK, REPORT):
        d.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    report = {"license": "Meshy via chave de API do usuario (ver Docs/CharacterMeshAudit/PaladinHealer/LICENSE.md)", "clips": {}}

    base_rig, base_body = import_glb(SRC / "paladinhealer_rigged.glb")
    base_rig.name = base_rig.data.name = RIG_NAME
    base_body.name = base_body.data.name = "PaladinHealer_LOD0"

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
    report["textureSize"], report["metalSmoothStats"] = fix_material(base_body)
    base_rig.data.display_type = "OCTAHEDRAL"
    bpy.context.view_layer.update()

    # --- retarget dos clipes Mixamo para o esqueleto Meshy (malha oculta para avaliar mais rapido)
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
        report["clips"][clip] = {"source": "Meshy nativo", "loop": True}
    for _, objs in src_cache.values():
        for o in objs:
            data = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if data is not None and data.users == 0:
                (bpy.data.meshes if isinstance(data, bpy.types.Mesh) else bpy.data.armatures).remove(data)
    for img in list(bpy.data.images):
        if img.users == 0 and img.name not in ("PaladinHealer_BaseColor",):
            bpy.data.images.remove(img)
    for mat in list(bpy.data.materials):
        if mat.users == 0:
            bpy.data.materials.remove(mat)
    base_body.hide_viewport = False

    # --- Heal criado em Blender
    base_body.hide_viewport = True
    actions["Heal"] = build_heal(base_rig, actions["Idle"])
    report["clips"]["Heal"] = {"source": "criado em Blender (build_heal)", "loop": False}
    base_body.hide_viewport = False

    # --- verificacao do bind + estatisticas
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
    assert min(counts) >= 1, "vertice sem pesos"

    # --- LODs (copias decimadas com skin no mesmo rig)
    bodies = {0: base_body}
    for lod, ratio in LOD_RATIOS.items():
        copy = base_body.copy()
        copy.data = base_body.data.copy()
        copy.name = copy.data.name = f"PaladinHealer_LOD{lod}"
        bpy.context.scene.collection.objects.link(copy)
        copy.parent = base_rig
        copy.matrix_parent_inverse = base_body.matrix_parent_inverse.copy()
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
        if not any(m.type == "ARMATURE" for m in copy.modifiers):
            mod = copy.modifiers.new("Armature", "ARMATURE")
            mod.object = base_rig
        bodies[lod] = copy
        report[f"trianglesLOD{lod}"] = tris(copy)
    for lod, o in bodies.items():
        for p in o.data.polygons:
            p.use_smooth = True
    assert report["trianglesLOD0"] > report["trianglesLOD1"] > report["trianglesLOD2"], "LODs nao decrescem"
    assert report["trianglesLOD0"] <= 31000, "LOD0 acima do orcamento humanoide (31 k)"

    # --- exportacao
    export_fbx(MODELS / "PaladinHealer.fbx", [base_rig] + list(bodies.values()), None, 30)
    for clip, act in actions.items():
        fps = 24 if clip in dict(NATIVE_CLIPS) else 30
        report["clips"][clip].update(clip_stats(base_rig, base_body, act, fps))
        bodies[2].name = "PaladinHealer_ClipMesh"
        export_fbx(MODELS / f"PaladinHealer@{clip}.fbx", [base_rig, bodies[2]], act, fps)
        bodies[2].name = "PaladinHealer_LOD2"

    # GLB: LOD0 + rig + todos os clipes
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
    base_rig.animation_data_create()
    for clip, act in actions.items():
        track = base_rig.animation_data.nla_tracks.new()
        track.name = clip
        strip = track.strips.new(clip, 1, act)
        strip.name = clip
    base_rig.animation_data.action = None
    bpy.context.view_layer.objects.active = base_rig
    bpy.ops.export_scene.gltf(filepath=str(MODELS / "PaladinHealer.glb"), export_format="GLB", use_selection=True,
                              export_animations=True, export_animation_mode="NLA_TRACKS", export_yup=True,
                              export_apply=False)
    for lod in (1, 2):
        bodies[lod].hide_viewport = False
        bodies[lod].hide_render = False

    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "PaladinHealer.blend"), compress=True)
    (REPORT / "build-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("BUILD_PALADINHEALER_OK", json.dumps({k: v for k, v in report.items() if k not in ("clips", "bones")}))
    print("CLIPS", json.dumps(report["clips"]))


main()
