"""Posiciona espada e escudo no esqueleto do PaladinHealer e grava Working/PaladinHealer/mounts.json.
O Unity usa mounts.json (transformacao MUNDIAL na pose de ligacao, espaco do personagem, eixos do Blender) para filiar as
pecas aos ossos; assim a relacao osso->peca e a mesma validada aqui, sem depender dos eixos de osso do FBX.
Espada: lamina no prolongamento do antebraco (inclinada SWORD_THETA_DEG), validada em Idle/Attack/Heal.
Escudo: orientado no quadro 1 do Idle (de pe, face para a frente, levemente para fora) e fixo ao antebraco esquerdo.
Tambem salva PaladinHealer_Preview.blend (corpo + pecas parentadas aos ossos) para RenderPaladinHealerPoses.py.
Uso: blender -b Tools/CharacterRigging/Working/PaladinHealer/PaladinHealer.blend --python Tools/CharacterRigging/MountPaladinHealerAccessories.py
"""
import json
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(bpy.data.filepath).parents[4]
WORK = ROOT / "Tools/CharacterRigging/Working/PaladinHealer"

SWORD_BONE = "RightHand"
SWORD_THETA_DEG = -20.0       # inclinacao da lamina em relacao ao eixo da mao (0 = prolongamento exato do antebraco)
SWORD_GRIP_ALONG = 0.085      # m ao longo do osso da mao a partir do pulso (centro da palma)
SHIELD_BONE = "LeftForeArm"
SHIELD_YAW_DEG = 25.0         # a face do escudo olha para a frente girada para fora (esquerda do personagem)
SHIELD_ARM_OFFSET = 0.070     # distancia do eixo do antebraco ate o verso do escudo
SHIELD_ALONG = 0.5            # fracao do antebraco (cotovelo -> pulso) onde fica o ponto de fixacao
SHIELD_LIFT = 0.04            # deslocamento vertical do escudo no quadro 1 do Idle (m)


def bind_world(rig, name):
    return rig.matrix_world @ rig.data.bones[name].matrix_local


def mount(obj, rig, bone, world):
    """Parenta obj ao osso preservando a transformacao mundial 'world' vigente (pose atual do rig)."""
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location, obj.rotation_euler, obj.scale = (0, 0, 0), (0, 0, 0), (1, 1, 1)
    bpy.context.view_layer.update()
    obj.matrix_parent_inverse = obj.matrix_world.copy().inverted() @ world


def to_list(m3):
    return [[round(v, 6) for v in row] for row in m3]


def main():
    rig = bpy.data.objects["PaladinHealer_Armature"]
    acc = WORK / "PaladinHealer_Accessories.blend"
    with bpy.data.libraries.load(str(acc), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in ("PaladinHealer_Sword", "PaladinHealer_Shield")]
    sword = shield = None
    for o in dst.objects:
        bpy.context.scene.collection.objects.link(o)
        if o.name.startswith("PaladinHealer_Sword"):
            sword = o
        else:
            shield = o
    sword.location = shield.location = (0, 0, 0)
    out = {}

    # ---- espada (pose de ligacao)
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion = (0, 0, 0), (1, 0, 0, 0)
    bpy.context.view_layer.update()
    hb = bind_world(rig, SWORD_BONE)
    hrot = hb.to_3x3().normalized()
    th = math.radians(SWORD_THETA_DEG)
    blade = Vector((math.sin(th), math.cos(th), 0))      # direcao da ponta, coordenadas do osso
    edge = Vector((math.cos(th), -math.sin(th), 0))
    ys = -blade
    zs = edge.cross(ys)
    M = Matrix((edge, ys, zs)).transposed()
    R = hrot @ M
    pos = hb.translation + hrot @ Vector((0, SWORD_GRIP_ALONG, 0))
    mount(sword, rig, SWORD_BONE, Matrix.Translation(pos) @ R.to_4x4())
    out["sword"] = {"bone": SWORD_BONE, "worldPosition": [round(v, 5) for v in pos], "worldRotation": to_list(R)}

    # ---- escudo: orientacao desejada no quadro 1 do Idle, convertida para a pose de ligacao
    idle = bpy.data.actions["Idle"]
    rig.animation_data.action = idle
    rig.animation_data.action_slot = idle.slots[0]
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    pose_fore = rig.matrix_world @ rig.pose.bones[SHIELD_BONE].matrix
    pose_hand = rig.matrix_world @ rig.pose.bones["LeftHand"].matrix
    arm_center = pose_fore.translation.lerp(pose_hand.translation, SHIELD_ALONG)
    Rd = Matrix.Rotation(math.radians(SHIELD_YAW_DEG), 3, "Z")        # objeto sem rotacao = face para -Y, topo para +Z
    shield_world_idle = Matrix.Translation(arm_center - Rd.col[1] * SHIELD_ARM_OFFSET + Vector((0, 0, SHIELD_LIFT))) @ Rd.to_4x4()
    mount(shield, rig, SHIELD_BONE, shield_world_idle)
    rel = pose_fore.inverted() @ shield_world_idle                      # escudo no espaco do osso
    bind_fore = bind_world(rig, SHIELD_BONE)
    shield_world_bind = bind_fore @ rel
    out["shield"] = {"bone": SHIELD_BONE, "worldPosition": [round(v, 5) for v in shield_world_bind.translation],
                     "worldRotation": to_list(shield_world_bind.to_3x3())}
    out["bindHeadHeightM"] = round((rig.matrix_world @ rig.data.bones["Head"].head_local).z, 4)
    for k in ("sword", "shield"):
        out[k]["rot9"] = [v for row in out[k]["worldRotation"] for v in row]
    text = json.dumps(out, indent=2)
    (WORK / "mounts.json").write_text(text, encoding="utf-8")
    (ROOT / "Docs/CharacterMeshAudit/PaladinHealer/mounts.json").write_text(text, encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "PaladinHealer_Preview.blend"), compress=True)
    print("MOUNTS_OK", json.dumps(out))


main()
