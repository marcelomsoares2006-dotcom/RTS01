"""Catapulta de cerco (mangonel/onagro de torcao) modelada proceduralmente para o Era Imperial.

Obra do projeto: nenhuma malha, textura ou animacao de terceiros (ver Docs/CharacterMeshAudit/Catapult/LICENSE.md).
Gera: texturas procedurais (madeira, ferro, corda, pedra), 3 LODs com pesos rigidos por osso, esqueleto generico,
6 clipes (Idle, Move, Reload, Fire, Hit, Destroy) e os FBX para o Unity.

Convencoes (as mesmas do Arqueiro): metros; Blender +Z cima; frente da unidade = -Y (vira +Z no Unity com axis_forward=-Z,
axis_up=Y); origem no chao, no centro da pegada. Esquerda/direita da unidade: direita = -X do Blender (+X no Unity).

Uso: blender -b --factory-startup --python-exit-code 1 --python Tools/CharacterRigging/BuildCatapult.py
Saidas: Assets/CharacterRigging/Catapult/{Models,Textures}, Tools/CharacterRigging/Working/Catapult, Docs/CharacterMeshAudit/Catapult/build-report.json
"""
from pathlib import Path
import bpy
import bmesh
import json
import math
import random
import zlib

import numpy as np
from mathutils import Vector, Matrix, Euler, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/CharacterRigging/Catapult'
MODELS = OUT / 'Models'
TEXTURES = OUT / 'Textures'
WORK = ROOT / 'Tools/CharacterRigging/Working/Catapult'
REPORT = ROOT / 'Docs/CharacterMeshAudit/Catapult'

FPS = 30
WOOD, IRON, ROPE, STONE = 0, 1, 2, 3
MAT_NAMES = ['Catapult_Wood', 'Catapult_Iron', 'Catapult_Rope', 'Catapult_Stone']
TEX_FILES = ['Catapult_Wood.png', 'Catapult_Iron.png', 'Catapult_Rope.png', 'Catapult_Stone.png']
UV_SCALE = {WOOD: 3.4, IRON: 1.0, ROPE: 1.5, STONE: 0.8}   # metros por unidade de UV
WOOD_BAND = 0.125                                            # altura de uma "tabua" na textura de madeira (7 tabuas + faixa de topo)

# ---------------------------------------------------------------------------------------------------- geometria (metros)
WHEEL_R, WHEEL_T = 0.46, 0.15
WHEEL_X, WHEEL_Y, WHEEL_Z = 1.025, 1.00, 0.46
RUNNER_X = 0.80
BEAM_Z = 0.66                      # eixo vertical das longarinas
PIVOT = Vector((0.0, -0.10, 1.20))  # pivo do braco (centro do feixe de torcao)
ARM_REST_DEG = -12.0               # angulo do braco armado, a partir de +Y rumo a +Z (negativo = abaixo da horizontal, para tras)
ARM_STOP_DEG = 116.0               # rotacao (phi) em que o braco encosta na trava
ARM_RELEASE_DEG = 100.0            # phi em que a pedra sai da concha
ARM_TOP = 1.62                     # comprimento do braco acima do pivo
ARM_TAIL = 0.32                    # cauda abaixo do pivo
CUP_Z = 1.42                       # centro da concha ao longo do braco
CUP_R = 0.21
STONE_R = 0.19
HOOK_S = 0.80                      # posicao do olhal do braco (distancia do pivo)
DRUM_C = Vector((0.0, 1.12, 0.77))
DRUM_R = 0.085
SKEIN_HALF = 0.70
FRAME_X = 0.76
APEX = Vector((0.0, -0.30, 2.00))
CLIP_FRAMES = {'Idle': 90, 'Move': 34, 'Reload': 78, 'Fire': 34, 'Hit': 15, 'Destroy': 72}
RELEASE_FRAME = 12                 # quadro (30 fps) do Fire em que a pedra sai


def crc(name):
    return zlib.crc32(name.encode('utf-8')) & 0xffffffff


def smooth(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


# ========================================================================================================== texturas
def value_noise(h, w, cy, cx, seed):
    rng = np.random.default_rng(seed)
    g = rng.random((cy, cx))
    ys = np.linspace(0, cy, h, endpoint=False)
    xs = np.linspace(0, cx, w, endpoint=False)
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    fy = ys - y0
    fx = xs - x0
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    y1 = (y0 + 1) % cy
    x1 = (x0 + 1) % cx
    y0 %= cy
    x0 %= cx
    a = g[y0][:, x0]
    b = g[y0][:, x1]
    c = g[y1][:, x0]
    d = g[y1][:, x1]
    top = a * (1 - fx)[None, :] + b * fx[None, :]
    bot = c * (1 - fx)[None, :] + d * fx[None, :]
    return top * (1 - fy)[:, None] + bot * fy[:, None]


def fbm(h, w, cy, cx, octaves, seed):
    out = np.zeros((h, w))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        out += amp * value_noise(h, w, cy * 2 ** o, cx * 2 ** o, seed + 101 * o)
        tot += amp
        amp *= 0.5
    return out / tot


def save_png(path, rgb):
    h, w, _ = rgb.shape
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb, 0, 1)
    img = bpy.data.images.new(path.stem, w, h, alpha=False)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def tex_wood(size=1024):
    """Sete tabuas de 128 px (veios ao longo de U) + faixa de topo com o fim de fibra (aneis) no canto esquerdo."""
    h = w = size
    img = np.zeros((h, w, 3))
    tones = [(0.44, 0.29, 0.16), (0.37, 0.24, 0.13), (0.49, 0.34, 0.20), (0.32, 0.21, 0.12),
             (0.52, 0.40, 0.25), (0.41, 0.31, 0.21), (0.35, 0.23, 0.14), (0.42, 0.28, 0.16)]
    band = h // 8
    rng = np.random.default_rng(7)
    for k in range(8):
        y0 = k * band
        tone = np.array(tones[k])
        streak = fbm(band, w, 26 + 3 * k, 5, 4, 11 + k)                # veios
        fine = fbm(band, w, 110, 14, 2, 31 + k)                         # fibras finas
        weather = fbm(band, w, 2, 3, 2, 51 + k)
        v = 0.62 + 0.55 * streak + 0.22 * (fine - 0.5) + 0.18 * (weather - 0.5)
        col = tone[None, None, :] * v[:, :, None]
        # veios escuros mais marcados
        dark = np.clip((streak - 0.62) * 3.0, 0, 1)[:, :, None] * 0.28
        col = col * (1 - dark)
        # bordas da tabua
        col[:3, :, :] *= 0.55
        col[-3:, :, :] *= 0.62
        # nos
        for _ in range(rng.integers(1, 3)):
            cx = rng.integers(60, w - 60)
            cy = rng.integers(26, band - 26)
            yy, xx = np.mgrid[0:band, 0:w]
            dx = (xx - cx) / 34.0
            dy = (yy - cy) / 11.0
            r = np.sqrt(dx * dx + dy * dy)
            ring = 0.5 + 0.5 * np.sin(r * 18.0)
            knot = np.clip(1.0 - r, 0, 1)[:, :, None]
            col = col * (1 - 0.55 * knot * (0.5 + 0.5 * ring[:, :, None]))
        img[y0:y0 + band] = col
    # fim de fibra: aneis concentricos no canto inferior esquerdo da faixa de topo (V 0.875..1, U 0..0.125)
    yy, xx = np.mgrid[0:band, 0:band]
    cx, cy = band * 0.42, band * 0.55
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    rings = 0.62 + 0.38 * np.sin(r * 0.55 + 6.0 * value_noise(band, band, 3, 3, 5))
    patch = np.array([0.40, 0.26, 0.14])[None, None, :] * rings[:, :, None]
    img[7 * band:8 * band, 0:band] = patch
    # leve granulado geral
    grain = (np.random.default_rng(3).random((h, w)) - 0.5) * 0.05
    img += grain[:, :, None]
    return img


def tex_iron(size=256):
    n = fbm(size, size, 12, 12, 4, 21)
    fine = fbm(size, size, 64, 64, 2, 22)
    rust = np.clip((fbm(size, size, 5, 5, 3, 23) - 0.5) * 3.0, 0, 1)
    base = np.array([0.20, 0.21, 0.23])
    col = base[None, None, :] * (0.75 + 0.55 * n[:, :, None] + 0.25 * (fine[:, :, None] - 0.5))
    rust_c = np.array([0.36, 0.18, 0.09])
    col = col * (1 - 0.45 * rust[:, :, None]) + rust_c[None, None, :] * (0.45 * rust[:, :, None]) * (0.7 + 0.6 * fine[:, :, None])
    scratch = (value_noise(size, size, 2, 90, 24) > 0.86).astype(float) * 0.10
    return col + scratch[:, :, None]


def tex_rope(size=512):
    yy, xx = np.mgrid[0:size, 0:size]
    u = xx / size
    v = yy / size
    phase = 6.0 * v + 15.0 * u + 0.35 * fbm(size, size, 4, 4, 2, 41)
    s = 0.5 + 0.5 * np.sin(2 * np.pi * phase)
    fib = fbm(size, size, 90, 90, 2, 42)
    base = np.array([0.62, 0.50, 0.32])
    shade = 0.45 + 0.55 * s + 0.18 * (fib - 0.5)
    col = base[None, None, :] * shade[:, :, None]
    dirt = fbm(size, size, 3, 3, 3, 43)
    col *= (0.85 + 0.25 * dirt)[:, :, None]
    return col


def tex_stone(size=256):
    n = fbm(size, size, 10, 10, 5, 61)
    fine = fbm(size, size, 60, 60, 2, 62)
    base = np.array([0.37, 0.36, 0.34])
    col = base[None, None, :] * (0.65 + 0.6 * n[:, :, None] + 0.25 * (fine[:, :, None] - 0.5))
    speck = (np.random.default_rng(63).random((size, size)) > 0.985).astype(float) * 0.25
    vein = np.clip((value_noise(size, size, 3, 7, 64) - 0.78) * 6.0, 0, 1)[:, :, None] * 0.25
    return col * (1 - vein) + speck[:, :, None] - 0.0


def build_textures():
    TEXTURES.mkdir(parents=True, exist_ok=True)
    save_png(TEXTURES / TEX_FILES[WOOD], tex_wood())
    save_png(TEXTURES / TEX_FILES[IRON], tex_iron())
    save_png(TEXTURES / TEX_FILES[ROPE], tex_rope())
    save_png(TEXTURES / TEX_FILES[STONE], tex_stone())


def make_materials():
    mats = []
    params = [(0.85, 0.0), (0.5, 0.55), (0.9, 0.0), (0.9, 0.0)]
    for i, name in enumerate(MAT_NAMES):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nodes = m.node_tree.nodes
        bsdf = nodes['Principled BSDF']
        tex = nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(TEXTURES / TEX_FILES[i]), check_existing=True)
        m.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
        bsdf.inputs['Roughness'].default_value = params[i][0]
        bsdf.inputs['Metallic'].default_value = params[i][1]
        mats.append(m)
    return mats


# ========================================================================================================== construtor
class Part:
    """Um objeto nomeado, em um osso, construido com bmesh. `xf` transforma as coordenadas locais das primitivas."""

    def __init__(self, name, bone):
        self.name = name
        self.bone = bone
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.rng = random.Random(crc(name))
        self.xf = Matrix.Identity(4)

    def vert(self, co):
        return self.bm.verts.new(self.xf @ Vector(co))

    def direction(self, d):
        return (self.xf.to_3x3() @ Vector(d)).normalized()

    def tris(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)


def finish_faces(P, faces, centroid, mat, smooth_flag=False):
    """Corrige o sentido (normais para fora do centro da primitiva convexa) e marca material/suavizacao."""
    for f in faces:
        f.normal_update()
        c = f.calc_center_median()
        if f.normal.dot(c - centroid) < 0:
            f.normal_flip()
        f.material_index = mat
        f.smooth = smooth_flag


def uv_planar(P, faces, grain, mat):
    """UV planar por face. Madeira: U ao longo do veio (`grain`), tabua sorteada; fim de fibra usa a faixa de topo."""
    rng = P.rng
    scale = UV_SCALE[mat]
    side, end = [], []
    raw = {}
    for f in faces:
        n = f.normal
        is_end = mat == WOOD and grain is not None and abs(n.dot(grain)) > 0.9
        if grain is not None and abs(n.dot(grain)) < 0.9:
            ux = (grain - n * grain.dot(n)).normalized()
        else:
            ref = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
            ux = (ref - n * ref.dot(n)).normalized()
        vx = n.cross(ux)
        o = f.verts[0].co
        for l in f.loops:
            d = l.vert.co - o
            raw[l] = (d.dot(ux) / scale, d.dot(vx) / scale)
        (end if is_end else side).append(f)

    def window(group, u_lo, u_hi, v_lo, v_hi, fit_u=False):
        if not group:
            return
        us = [raw[l][0] for f in group for l in f.loops]
        vs = [raw[l][1] for f in group for l in f.loops]
        eu, ev = max(us) - min(us), max(vs) - min(vs)
        k = 1.0
        if eu > (u_hi - u_lo) or ev > (v_hi - v_lo):
            k = min((u_hi - u_lo) / max(eu, 1e-6), (v_hi - v_lo) / max(ev, 1e-6)) * 0.98
        ou = u_lo + rng.uniform(0, max(0.0, (u_hi - u_lo) - eu * k))
        ov = v_lo + rng.uniform(0, max(0.0, (v_hi - v_lo) - ev * k))
        for f in group:
            for l in f.loops:
                u, v = raw[l]
                l[P.uv].uv = (ou + (u - min(us)) * k, ov + (v - min(vs)) * k)

    if mat == WOOD:
        band = rng.randrange(0, 7)
        window(side, 0.0, 1.0, band * WOOD_BAND + 0.004, (band + 1) * WOOD_BAND - 0.004)
        window(end, 0.0, WOOD_BAND - 0.004, 7 * WOOD_BAND + 0.004, 1.0 - 0.004)
    else:
        window(side + end, 0.0, 1.0, 0.0, 1.0)


def box(P, center, size, mat, rot=None, grain=None):
    """Caixa. `size` nos eixos locais (x,y,z); `rot` Matrix3 opcional; veio ao longo do maior lado (ou `grain` local)."""
    rot = rot if rot is not None else Matrix.Identity(3)
    hx, hy, hz = (s / 2 for s in size)
    vs = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                vs.append(P.vert(Vector(center) + rot @ Vector((sx * hx, sy * hy, sz * hz))))
    idx = lambda sx, sy, sz: ((sx > 0) << 2) | ((sy > 0) << 1) | (sz > 0)
    quads = [
        (idx(-1, -1, -1), idx(-1, -1, 1), idx(-1, 1, 1), idx(-1, 1, -1)),
        (idx(1, -1, -1), idx(1, 1, -1), idx(1, 1, 1), idx(1, -1, 1)),
        (idx(-1, -1, -1), idx(1, -1, -1), idx(1, -1, 1), idx(-1, -1, 1)),
        (idx(-1, 1, -1), idx(-1, 1, 1), idx(1, 1, 1), idx(1, 1, -1)),
        (idx(-1, -1, -1), idx(-1, 1, -1), idx(1, 1, -1), idx(1, -1, -1)),
        (idx(-1, -1, 1), idx(1, -1, 1), idx(1, 1, 1), idx(-1, 1, 1)),
    ]
    faces = [P.bm.faces.new([vs[i] for i in q]) for q in quads]
    centroid = P.xf @ Vector(center)
    finish_faces(P, faces, centroid, mat)
    if grain is None:
        longest = max(range(3), key=lambda i: size[i])
        gl = Vector((1 if longest == 0 else 0, 1 if longest == 1 else 0, 1 if longest == 2 else 0))
        grain = rot @ gl
    uv_planar(P, faces, P.direction(grain), mat)
    return faces


def section_points(w, h, c):
    w2, h2 = w / 2, h / 2
    if c <= 0:
        return [(w2, h2), (-w2, h2), (-w2, -h2), (w2, -h2)]
    c = min(c, w2 * 0.9, h2 * 0.9)
    return [(w2 - c, h2), (-w2 + c, h2), (-w2, h2 - c), (-w2, -h2 + c),
            (-w2 + c, -h2), (w2 - c, -h2), (w2, -h2 + c), (w2, h2 - c)]


def beam(P, p0, p1, w0, h0, mat, w1=None, h1=None, up=None, chamfer=0.0):
    """Viga de secao retangular (com chanfro opcional) de p0 a p1, w = lado ('side'), h = lado vertical ('up')."""
    p0, p1 = Vector(p0), Vector(p1)
    w1 = w0 if w1 is None else w1
    h1 = h0 if h1 is None else h1
    d = (p1 - p0).normalized()
    up = Vector((0, 0, 1)) if up is None else Vector(up)
    if abs(d.dot(up)) > 0.95:
        up = Vector((0, 1, 0))
    side = d.cross(up).normalized()
    upv = side.cross(d).normalized()
    s0 = section_points(w0, h0, chamfer)
    s1 = section_points(w1, h1, chamfer)
    r0 = [P.vert(p0 + side * a + upv * b) for a, b in s0]
    r1 = [P.vert(p1 + side * a + upv * b) for a, b in s1]
    n = len(r0)
    faces = [P.bm.faces.new((r0[i], r0[(i + 1) % n], r1[(i + 1) % n], r1[i])) for i in range(n)]
    cap0 = P.bm.faces.new([P.vert(p0 + side * a + upv * b) for a, b in s0][::-1])
    cap1 = P.bm.faces.new([P.vert(p1 + side * a + upv * b) for a, b in s1])
    centroid = P.xf @ ((p0 + p1) / 2)
    finish_faces(P, faces + [cap0, cap1], centroid, mat)
    uv_planar(P, faces + [cap0, cap1], P.direction(d), mat)
    return faces


def basis_for(axis):
    """(e1, e2, d) destro: e1 x e2 = d."""
    d = Vector(axis).normalized()
    ref = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    e1 = ref.cross(d).normalized()
    e2 = d.cross(e1).normalized()
    return e1, e2, d


def ring_points(center, e1, e2, r, n, twist=0.0):
    return [center + (e1 * math.cos(2 * math.pi * i / n + twist) + e2 * math.sin(2 * math.pi * i / n + twist)) * r for i in range(n)]


def cylinder(P, p0, p1, r0, r1, sides, mat, caps=(True, True), smooth_flag=True, wrap_v=None, twist=0.0):
    """Cilindro/cone de p0 a p1. UV: U ao longo do eixo, V em volta (corda e cilindros grandes: 0..1; demais: comprimento do arco)."""
    p0, p1 = Vector(p0), Vector(p1)
    length = (p1 - p0).length
    e1, e2, d = basis_for(p1 - p0)
    ring0 = [P.vert(c) for c in ring_points(p0, e1, e2, r0, sides, twist)]
    ring1 = [P.vert(c) for c in ring_points(p1, e1, e2, r1, sides, twist)]
    side_faces = [P.bm.faces.new((ring0[i], ring0[(i + 1) % sides], ring1[(i + 1) % sides], ring1[i])) for i in range(sides)]
    cap_faces = []
    if caps[0] and r0 > 1e-5:
        cap_faces.append(P.bm.faces.new([P.vert(c) for c in ring_points(p0, e1, e2, r0, sides, twist)][::-1]))
    if caps[1] and r1 > 1e-5:
        cap_faces.append(P.bm.faces.new([P.vert(c) for c in ring_points(p1, e1, e2, r1, sides, twist)]))
    centroid = P.xf @ ((p0 + p1) / 2)
    finish_faces(P, side_faces, centroid, mat, smooth_flag)
    finish_faces(P, cap_faces, centroid, mat, False)
    if wrap_v is None:
        wrap_v = mat == ROPE or max(r0, r1) > 0.12
    scale = UV_SCALE[mat]
    rmean = (r0 + r1) / 2
    ulen = length / scale
    vlen = 1.0 if wrap_v else 2 * math.pi * rmean / scale
    rng = P.rng
    u_off = rng.uniform(0, max(0.0, 1.0 - ulen))
    if mat == WOOD and not wrap_v:
        band = rng.randrange(0, 7)
        v_lo = band * WOOD_BAND + 0.004
        v_off = v_lo + rng.uniform(0, max(0.0, WOOD_BAND - 0.008 - vlen)) if vlen < WOOD_BAND - 0.008 else rng.uniform(0, max(0.0, 1.0 - vlen))
    else:
        v_off = rng.uniform(0, max(0.0, 1.0 - vlen)) if not wrap_v else 0.0
    for i, f in enumerate(side_faces):
        j = (i + 1) % sides
        uv = {ring0[i]: (u_off, v_off + vlen * i / sides), ring0[j]: (u_off, v_off + vlen * (i + 1) / sides),
              ring1[j]: (u_off + ulen, v_off + vlen * (i + 1) / sides), ring1[i]: (u_off + ulen, v_off + vlen * i / sides)}
        for l in f.loops:
            l[P.uv].uv = uv[l.vert]
    if cap_faces:
        uv_planar(P, cap_faces, None, mat)
    return side_faces + cap_faces


def gear(P, center, axis, r_in, r_out, teeth, thickness, mat=IRON):
    """Disco dentado (catraca) de espessura `thickness` ao longo de `axis`, centrado em `center`."""
    center = Vector(center)
    e1, e2, ax = basis_for(axis)
    outline = []
    pitch = 2 * math.pi / teeth
    for t in range(teeth):
        for frac, r in ((0.0, r_in), (0.12, r_out), (0.45, r_out), (0.62, r_in)):
            a = t * pitch + frac * pitch
            outline.append((e1 * math.cos(a) + e2 * math.sin(a)) * r)
    n = len(outline)
    off = ax * thickness / 2
    bot = [P.vert(center + o - off) for o in outline]
    top = [P.vert(center + o + off) for o in outline]
    faces = [P.bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i])) for i in range(n)]
    ctop, cbot = P.vert(center + off), P.vert(center - off)
    tv = [P.vert(center + o + off) for o in outline]
    bv = [P.vert(center + o - off) for o in outline]
    caps = []
    for i in range(n):
        j = (i + 1) % n
        caps.append(P.bm.faces.new((ctop, tv[i], tv[j])))
        caps.append(P.bm.faces.new((cbot, bv[j], bv[i])))
    for f in faces + caps:
        f.material_index = mat
        f.smooth = False
        f.normal_update()
    uv_planar(P, faces + caps, None, mat)


def torus(P, center, axis, R, r, segs, sides, mat=IRON):
    center = Vector(center)
    e1, e2, ax = basis_for(axis)
    rings = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        c = e1 * math.cos(a) + e2 * math.sin(a)
        rings.append([P.vert(center + c * (R + r * math.cos(2 * math.pi * j / sides)) + ax * (r * math.sin(2 * math.pi * j / sides))) for j in range(sides)])
    faces = []
    for i in range(segs):
        k = (i + 1) % segs
        for j in range(sides):
            m = (j + 1) % sides
            faces.append(P.bm.faces.new((rings[i][j], rings[k][j], rings[k][m], rings[i][m])))
    for f in faces:
        f.material_index = mat
        f.smooth = True
        f.normal_update()
    uv_planar(P, faces, None, mat)


def icosphere(P, center, radius, subdiv, mat, jitter=0.0, squash=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    rng = P.rng
    for v in bm.verts:
        v.co = Vector((v.co.x * squash[0], v.co.y * squash[1], v.co.z * squash[2]))
        if jitter:
            v.co *= 1.0 + rng.uniform(-jitter, jitter)
    mapping = {v: P.vert(Vector(center) + v.co) for v in bm.verts}
    faces = [P.bm.faces.new([mapping[v] for v in f.verts]) for f in bm.faces]
    bm.free()
    finish_faces(P, faces, P.xf @ Vector(center), mat, False)
    uv_planar(P, faces, None, mat)
    return faces


def bowl(P, rim_center, axis, r, thick, segs, rings, mat=WOOD):
    """Concha: casca hemisferica aberta para `axis` (borda em rim_center, polo atras), com espessura."""
    rim_center = Vector(rim_center)
    e1, e2, ax = basis_for(axis)

    def surf(rad):
        rows = []
        for k in range(rings + 1):
            phi = (math.pi / 2) * k / rings
            h = -rad * math.sin(phi)
            if k == rings:
                rows.append([P.vert(rim_center + ax * h)])
            else:
                rows.append([P.vert(c) for c in ring_points(rim_center + ax * h, e1, e2, rad * math.cos(phi), segs)])
        return rows

    outer, inner = surf(r), surf(r - thick)
    groups = {'o': [], 'i': [], 'r': []}
    for key, rows in (('o', outer), ('i', inner)):
        for k in range(rings):
            a, b = rows[k], rows[k + 1]
            for i in range(segs):
                j = (i + 1) % segs
                groups[key].append(P.bm.faces.new((a[i], a[j], b[0])) if k == rings - 1 else P.bm.faces.new((a[i], a[j], b[j], b[i])))
    for i in range(segs):
        j = (i + 1) % segs
        groups['r'].append(P.bm.faces.new((outer[0][i], outer[0][j], inner[0][j], inner[0][i])))
    center_w = P.xf @ rim_center
    axis_w = P.xf.to_3x3() @ ax
    for f in groups['o'] + groups['i'] + groups['r']:
        f.normal_update()
    for f in groups['o']:
        if f.normal.dot(f.calc_center_median() - center_w) < 0:
            f.normal_flip()
    for f in groups['i']:
        if f.normal.dot(f.calc_center_median() - center_w) > 0:
            f.normal_flip()
    for f in groups['r']:
        if f.normal.dot(axis_w) < 0:
            f.normal_flip()
    allf = groups['o'] + groups['i'] + groups['r']
    for f in allf:
        f.material_index = mat
        f.smooth = f not in groups['r']
    uv_planar(P, allf, (P.xf.to_3x3() @ e1).normalized(), mat)


def annulus_prism(P, r_in, r_out, z0, z1, sides, mat, outer_smooth=True):
    """Anel (z0<z1) com paredes externa/interna e duas faces planas; sentido de face construido (eixo local Z, sem espelhos)."""
    def ring(r, z):
        return [P.vert(Vector((r * math.cos(2 * math.pi * i / sides), r * math.sin(2 * math.pi * i / sides), z))) for i in range(sides)]
    ob, ot, ib, it = ring(r_out, z0), ring(r_out, z1), ring(r_in, z0), ring(r_in, z1)
    fs = {'o': [], 'i': [], 't': [], 'b': []}
    for i in range(sides):
        j = (i + 1) % sides
        fs['o'].append(P.bm.faces.new((ob[i], ob[j], ot[j], ot[i])))
        fs['i'].append(P.bm.faces.new((it[i], it[j], ib[j], ib[i])))
        fs['t'].append(P.bm.faces.new((ot[i], ot[j], it[j], it[i])))
        fs['b'].append(P.bm.faces.new((ib[i], ib[j], ob[j], ob[i])))
    for k, lst in fs.items():
        for f in lst:
            f.material_index = mat
            f.smooth = outer_smooth and k in ('o', 'i')
            f.normal_update()
    uv_planar(P, [f for lst in fs.values() for f in lst], None, mat)


def wheel(P, lod):
    """Roda com eixo local Z (raio 0.46, largura 0.15); o chamador orienta o eixo ao longo de X pelo `P.xf`."""
    T = WHEEL_T
    if lod == 0:
        annulus_prism(P, WHEEL_R - 0.035, WHEEL_R, -T / 2, T / 2, 20, IRON)                  # pneu de ferro
        n = 10
        r1, r2 = 0.335, WHEEL_R - 0.037
        for k in range(n):                                                                    # gomos do aro (madeira)
            a0, a1 = 2 * math.pi * k / n + 0.02, 2 * math.pi * (k + 1) / n - 0.02
            am = (a0 + a1) / 2
            ht = T * 0.45
            rows = [[Vector((r1 * math.cos(a0), r1 * math.sin(a0), z)), Vector((r2 * math.cos(a0), r2 * math.sin(a0), z)),
                     Vector((r2 * math.cos(a1), r2 * math.sin(a1), z)), Vector((r1 * math.cos(a1), r1 * math.sin(a1), z))] for z in (-ht, ht)]
            vs = [[P.vert(p) for p in row] for row in rows]
            fcs = [P.bm.faces.new((vs[0][3], vs[0][2], vs[0][1], vs[0][0])), P.bm.faces.new((vs[1][0], vs[1][1], vs[1][2], vs[1][3]))]
            for a, b in ((0, 1), (1, 2), (2, 3), (3, 0)):
                fcs.append(P.bm.faces.new((vs[0][a], vs[0][b], vs[1][b], vs[1][a])))
            cen = P.xf @ Vector((((r1 + r2) / 2) * math.cos(am), ((r1 + r2) / 2) * math.sin(am), 0))
            finish_faces(P, fcs, cen, WOOD)
            uv_planar(P, fcs, P.direction((-math.sin(am), math.cos(am), 0)), WOOD)
        for k in range(8):                                                                    # raios
            a = 2 * math.pi * k / 8 + 0.1
            rad = Vector((math.cos(a), math.sin(a), 0))
            beam(P, rad * 0.07, rad * 0.345, 0.055, 0.075, WOOD, w1=0.045, h1=0.065, up=Vector((0, 0, 1)))
        cylinder(P, (0, 0, -0.105), (0, 0, 0.105), 0.092, 0.092, 10, WOOD)                    # cubo
        for z in (-0.095, 0.075):
            cylinder(P, (0, 0, z), (0, 0, z + 0.04), 0.101, 0.101, 10, IRON)
        cylinder(P, (0, 0, 0.105), (0, 0, 0.145), 0.052, 0.045, 8, IRON)
    elif lod == 1:
        cylinder(P, (0, 0, -T / 2), (0, 0, T / 2), WHEEL_R, WHEEL_R, 12, WOOD)
        cylinder(P, (0, 0, -T / 2 - 0.004), (0, 0, T / 2 + 0.004), WHEEL_R + 0.004, WHEEL_R + 0.004, 12, IRON, caps=(False, False))
        for k in range(2):
            a = math.pi * k / 2 + 0.2
            rad = Vector((math.cos(a), math.sin(a), 0))
            beam(P, -rad * 0.40, rad * 0.40, 0.06, 0.09, WOOD, up=Vector((0, 0, 1)))
        cylinder(P, (0, 0, -0.1), (0, 0, 0.12), 0.09, 0.09, 8, WOOD)
    else:
        cylinder(P, (0, 0, -T / 2), (0, 0, T / 2), WHEEL_R, WHEEL_R, 8, WOOD)


# ========================================================================================================== modelo
def arm_frame_matrix():
    """L (braco vertical, frente = -Y) -> mundo (armado), em torno do pivo."""
    a = math.radians(ARM_REST_DEG - 90.0)
    return Matrix.Translation(PIVOT) @ Matrix.Rotation(a, 4, 'X')


def rope_endpoints():
    xf = arm_frame_matrix()
    hook = xf @ Vector((0, 0.10, HOOK_S))
    drum_top = DRUM_C + Vector((0, -0.02, DRUM_R))
    return drum_top, hook


def build_parts(lod):
    """Devolve lista de Part (ordem estavel). `lod` 0/1/2 escolhe o nivel de detalhe."""
    parts = []

    def part(name, bone):
        p = Part(f'{name}', bone)
        parts.append(p)
        return p

    ch = 0.012 if lod == 0 else 0.0

    # ---- chassi: longarinas, travessas, tabuas
    chassis = part('Chassis', 'Root')
    for s in (-1, 1):
        beam(chassis, (s * RUNNER_X, -1.6, BEAM_Z), (s * RUNNER_X, 1.6, BEAM_Z), 0.16, 0.20, WOOD, chamfer=ch)
    cross_y = [-1.50, -0.55, 0.45, 1.12, 1.50] if lod == 0 else ([-1.50, 1.50] if lod == 2 else [-1.50, -0.55, 0.45, 1.50])
    for y in cross_y:
        beam(chassis, (-0.72, y, BEAM_Z - 0.02), (0.72, y, BEAM_Z - 0.02), 0.17, 0.16, WOOD, up=Vector((0, 0, 1)), chamfer=ch * 0.8)
    if lod == 0:
        for y in (-1.45, -1.355, -1.26, -1.165, -1.07):
            beam(chassis, (-0.84, y, 0.78), (0.84, y, 0.78), 0.085, 0.04, WOOD)
        for y in (0.62, 0.715, 0.81, 0.905, 1.0):
            beam(chassis, (-0.84, y, 0.78), (0.84, y, 0.78), 0.085, 0.04, WOOD)
    elif lod == 1:
        box(chassis, (0, -1.27, 0.78), (1.68, 0.46, 0.04), WOOD)
        box(chassis, (0, 0.81, 0.78), (1.68, 0.44, 0.04), WOOD)
    else:
        box(chassis, (0, -1.27, 0.78), (1.68, 0.46, 0.04), WOOD)
    if lod <= 1:
        # eixos
        for y in (-WHEEL_Y, WHEEL_Y):
            cylinder(chassis, (-1.07, y, WHEEL_Z), (1.07, y, WHEEL_Z), 0.045, 0.045, 8 if lod == 0 else 6, WOOD)
    if lod == 0:
        # calcos do eixo sob as longarinas + cintas de ferro
        for s in (-1, 1):
            for y in (-WHEEL_Y, WHEEL_Y):
                box(chassis, (s * RUNNER_X, y, 0.545), (0.17, 0.24, 0.07), WOOD)
                box(chassis, (s * RUNNER_X, y - 0.14, 0.58), (0.19, 0.025, 0.28), IRON)
                box(chassis, (s * RUNNER_X, y + 0.14, 0.58), (0.19, 0.025, 0.28), IRON)
            # cintas das longarinas
            for y in (-1.45, -0.55, 0.45, 1.45):
                box(chassis, (s * RUNNER_X, y, BEAM_Z), (0.185, 0.03, 0.225), IRON)
            # reforco de ferro nos cantos da frente
            box(chassis, (s * RUNNER_X, -1.6, BEAM_Z), (0.18, 0.025, 0.215), IRON)
            box(chassis, (s * RUNNER_X, 1.6, BEAM_Z), (0.18, 0.025, 0.215), IRON)

    # ---- quadros laterais em A, apoio do feixe, travessas do topo e da trava
    frame = part('Frame', 'Root')
    for s in (-1, 1):
        x = s * FRAME_X
        beam(frame, (x, -0.95, 0.76), (x, APEX.y, APEX.z), 0.12, 0.15, WOOD, up=Vector((1, 0, 0)), chamfer=ch)
        beam(frame, (x, 0.55, 0.76), (x, APEX.y, APEX.z), 0.12, 0.15, WOOD, up=Vector((1, 0, 0)), chamfer=ch)
        box(frame, (x, -0.24, PIVOT.z), (0.13, 1.00, 0.17), WOOD)
        if lod == 0:
            box(frame, (x + s * 0.067, -0.72, PIVOT.z), (0.012, 0.19, 0.30), IRON)
            box(frame, (x + s * 0.067, 0.24, PIVOT.z), (0.012, 0.19, 0.30), IRON)
            box(frame, (x, APEX.y, APEX.z - 0.04), (0.15, 0.20, 0.07), IRON)
            for y, z in ((-0.95, 0.80), (0.55, 0.80)):
                box(frame, (x, y, z), (0.14, 0.18, 0.06), IRON)
            # escora diagonal traseira
            beam(frame, (x, 1.00, 0.76), (x, 0.28, 1.12), 0.09, 0.10, WOOD, up=Vector((1, 0, 0)))
    if lod >= 1:
        pass
    beam(frame, (-0.84, APEX.y, APEX.z - 0.02), (0.84, APEX.y, APEX.z - 0.02), 0.13, 0.14, WOOD, chamfer=ch)
    # trava (batente) acolchoada
    stop_y, stop_z = stop_position()
    beam(frame, (-0.80, stop_y, stop_z), (0.80, stop_y, stop_z), 0.12, 0.12, WOOD, chamfer=ch)
    pad = part('StopPad', 'Root')
    cylinder(pad, (-0.34, stop_y, stop_z), (0.34, stop_y, stop_z), 0.095, 0.095, 10 if lod == 0 else 6, ROPE, wrap_v=True)
    if lod == 0:
        for x in (-0.36, 0.36):
            cylinder(pad, (x - 0.012, stop_y, stop_z), (x + 0.012, stop_y, stop_z), 0.10, 0.10, 10, IRON)

    # ---- feixe de torcao (cordas) e catracas
    skein = part('Skein', 'Root')
    if lod == 0:
        for k in range(6):
            a = math.radians(18 + 60 * k)
            c = Vector((0, PIVOT.y + 0.16 * math.cos(a), PIVOT.z + 0.16 * math.sin(a)))
            cylinder(skein, (-SKEIN_HALF, c.y, c.z), (SKEIN_HALF, c.y, c.z), 0.075, 0.075, 8, ROPE, wrap_v=True)
        for x in (-0.46, -0.16, 0.16, 0.46):
            cylinder(skein, (x - 0.025, PIVOT.y, PIVOT.z), (x + 0.025, PIVOT.y, PIVOT.z), 0.245, 0.245, 12, ROPE, caps=(False, False), wrap_v=True)
    elif lod == 1:
        cylinder(skein, (-SKEIN_HALF, PIVOT.y, PIVOT.z), (SKEIN_HALF, PIVOT.y, PIVOT.z), 0.23, 0.23, 8, ROPE, wrap_v=True)
    else:
        cylinder(skein, (-SKEIN_HALF, PIVOT.y, PIVOT.z), (SKEIN_HALF, PIVOT.y, PIVOT.z), 0.23, 0.23, 6, ROPE, wrap_v=True)
    ratchet = part('Ratchets', 'Root')
    for s in (-1, 1):
        x = s * (FRAME_X + 0.067 + 0.03)
        if lod == 0:
            gear(ratchet, (x, PIVOT.y, PIVOT.z), (1, 0, 0), 0.25, 0.31, 12, 0.05)
            cylinder(ratchet, (x, PIVOT.y, PIVOT.z), (x + s * 0.06, PIVOT.y, PIVOT.z), 0.07, 0.06, 8, IRON)
            # fecho (trava): alavanca de ferro apoiada nos dentes
            beam(ratchet, (x, PIVOT.y + 0.40, PIVOT.z + 0.30), (x, PIVOT.y + 0.12, PIVOT.z + 0.31), 0.035, 0.05, IRON)
            box(ratchet, (x, PIVOT.y + 0.43, PIVOT.z + 0.30), (0.06, 0.07, 0.10), IRON)
        elif lod == 1:
            cylinder(ratchet, (x - 0.02, PIVOT.y, PIVOT.z), (x + 0.02, PIVOT.y, PIVOT.z), 0.30, 0.30, 10, IRON)

    # ---- braco (osso Arm): construido vertical e levado a pose armada
    arm = part('Arm', 'Arm')
    arm.xf = arm_frame_matrix()
    beam(arm, (0, 0, -ARM_TAIL), (0, 0, ARM_TOP), 0.19, 0.16, WOOD, w1=0.14, h1=0.12, up=Vector((0, 1, 0)), chamfer=ch)
    if lod == 0:
        for z, wd in ((0.30, 0.06), (0.80, 0.06), (1.28, 0.06)):
            box(arm, (0, 0, z), (0.22, 0.19, wd), IRON)
        box(arm, (0, 0, -ARM_TAIL + 0.03), (0.165, 0.14, 0.06), IRON)
        # olhal do gancho (recarga)
        torus(arm, (0, 0.10, HOOK_S), (1, 0, 0), 0.032, 0.010, 10, 6)
        box(arm, (0, 0.082, HOOK_S), (0.05, 0.035, 0.04), IRON)
    cup = Vector((0, -0.06 - CUP_R, CUP_Z))
    bowl(arm, cup, (0, -1, 0), CUP_R, 0.028, 10 if lod == 0 else 6, 3 if lod == 0 else 2, WOOD)
    if lod == 0:
        # aro de ferro da concha e amarras de corda
        torus(arm, cup, (0, 1, 0), CUP_R + 0.008, 0.012, 14, 5)
        for sx in (-1, 1):
            for dz in (-0.12, 0.12):
                beam(arm, (sx * 0.155, cup.y + 0.03, CUP_Z + dz * 1.4), (sx * 0.058, -0.05, CUP_Z + dz * 0.5), 0.028, 0.028, ROPE)
    elif lod == 1:
        pass

    # ---- pedra carregada (osso Ammo)
    ammo = part('LoadedStone', 'Ammo')
    ammo.xf = arm_frame_matrix()
    icosphere(ammo, cup, STONE_R, 2 if lod == 0 else 1, STONE, jitter=0.06, squash=(1.0, 0.95, 1.0))

    # ---- guincho (osso Winch) e apoios
    winch = part('Winch', 'Winch')
    drum_segs = 10 if lod == 0 else 6
    cylinder(winch, (-0.43, DRUM_C.y, DRUM_C.z), (0.43, DRUM_C.y, DRUM_C.z), DRUM_R, DRUM_R, drum_segs, WOOD, wrap_v=False)
    if lod <= 1:
        for x in (-0.43, 0.43):
            cylinder(winch, (x - 0.015, DRUM_C.y, DRUM_C.z), (x + 0.015, DRUM_C.y, DRUM_C.z), 0.15, 0.15, 12 if lod == 0 else 8, WOOD, wrap_v=True)
    if lod == 0:
        cylinder(winch, (-0.30, DRUM_C.y, DRUM_C.z), (0.30, DRUM_C.y, DRUM_C.z), DRUM_R + 0.012, DRUM_R + 0.012, 10, ROPE, wrap_v=False, caps=(False, False))
        gear(winch, (-0.37, DRUM_C.y, DRUM_C.z), (1, 0, 0), 0.11, 0.15, 10, 0.04)
        # manivelas (duas, opostas) com punhos
        for sgn in (1, -1):
            beam(winch, (0.50, DRUM_C.y, DRUM_C.z), (0.50, DRUM_C.y + sgn * 0.0, DRUM_C.z + sgn * 0.26), 0.05, 0.05, WOOD, up=Vector((1, 0, 0)))
            cylinder(winch, (0.46, DRUM_C.y, DRUM_C.z + sgn * 0.26), (0.62, DRUM_C.y, DRUM_C.z + sgn * 0.26), 0.028, 0.028, 6, WOOD)
        cylinder(winch, (0.43, DRUM_C.y, DRUM_C.z), (0.52, DRUM_C.y, DRUM_C.z), 0.05, 0.05, 8, IRON)
    elif lod == 1:
        beam(winch, (0.52, DRUM_C.y, DRUM_C.z - 0.26), (0.52, DRUM_C.y, DRUM_C.z + 0.26), 0.05, 0.05, WOOD, up=Vector((1, 0, 0)))
    winch_posts = part('WinchPosts', 'Root')
    for s in (-1, 1):
        if lod <= 1:
            box(winch_posts, (s * 0.50, DRUM_C.y, 0.865), (0.09, 0.13, 0.28), WOOD)
        if lod == 0:
            box(winch_posts, (s * 0.50, DRUM_C.y, 1.01), (0.10, 0.15, 0.03), IRON)
            # fecho do guincho: pino de ferro na catraca
    if lod == 0:
        beam(winch_posts, (-0.37, DRUM_C.y + 0.19, 0.97), (-0.37, DRUM_C.y + 0.05, 0.93), 0.03, 0.045, IRON)

    # ---- corda do guincho (osso Rope)
    if lod <= 1:
        rope = part('WinchRope', 'Rope')
        d0, a0 = rope_endpoints()
        cylinder(rope, d0, a0, 0.018, 0.018, 6 if lod == 0 else 4, ROPE, wrap_v=True, caps=(True, True))

    # ---- rodas (ossos Wheel_*)
    for name, sx, sy in (('Wheel_FL', 1, -1), ('Wheel_FR', -1, -1), ('Wheel_BL', 1, 1), ('Wheel_BR', -1, 1)):
        w = part(name, name)
        c = Vector((sx * WHEEL_X, sy * WHEEL_Y, WHEEL_Z))
        # eixo local Z -> +X do mundo
        w.xf = Matrix.Translation(c) @ Matrix.Rotation(math.radians(90), 4, 'Y')
        wheel(w, lod)
    return parts


def stop_position():
    """Centro da trava acolchoada: tangente ao braco em ARM_STOP_DEG."""
    th = math.radians(ARM_REST_DEG + ARM_STOP_DEG)
    a = Vector((math.cos(th), math.sin(th)))
    n = Vector((-math.sin(th), math.cos(th)))
    s_c = 0.52
    face = Vector((PIVOT.y, PIVOT.z)) + a * s_c + n * 0.075
    c = face + n * 0.095
    return c.x, c.y


# ========================================================================================================== armadura
BONES = {}  # nome -> (head, tail, parent)


def define_bones():
    arm_dir = Vector((0, math.cos(math.radians(ARM_REST_DEG)), math.sin(math.radians(ARM_REST_DEG))))
    drum_top, hook = rope_endpoints()
    cupc = arm_frame_matrix() @ Vector((0, -0.06 - CUP_R, CUP_Z))
    BONES['Root'] = (Vector((0, 0, 0)), Vector((0, 0.30, 0)), None)
    BONES['Arm'] = (PIVOT.copy(), PIVOT + arm_dir * 1.0, 'Root')
    BONES['Ammo'] = (cupc, cupc + Vector((0, 0.10, 0)), 'Arm')
    BONES['Winch'] = (DRUM_C.copy(), DRUM_C + Vector((0, 0.15, 0)), 'Root')
    BONES['Rope'] = (drum_top, hook, 'Root')
    for name, sx, sy in (('Wheel_FL', 1, -1), ('Wheel_FR', -1, -1), ('Wheel_BL', 1, 1), ('Wheel_BR', -1, 1)):
        c = Vector((sx * WHEEL_X, sy * WHEEL_Y, WHEEL_Z))
        BONES[name] = (c, c + Vector((0, 0.10, 0)), 'Root')


def make_armature():
    arm_data = bpy.data.armatures.new('CatapultArmature')
    rig = bpy.data.objects.new('CatapultArmature', arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    define_bones()
    order = ['Root', 'Arm', 'Ammo', 'Winch', 'Rope', 'Wheel_FL', 'Wheel_FR', 'Wheel_BL', 'Wheel_BR']
    for name in order:
        head, tail, parent = BONES[name]
        eb = arm_data.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        eb.roll = 0.0
        if parent:
            eb.parent = arm_data.edit_bones[parent]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


# ========================================================================================================== montagem
def parts_to_object(parts, name, mats, rig, part_info):
    """Junta as pecas num unico objeto skinned (um grupo de vertices por osso, peso 1.0)."""
    objs = []
    for p in parts:
        part_info.append({'name': p.name, 'bone': p.bone, 'triangles': p.tris()})
        mesh = bpy.data.meshes.new(f'{name}_{p.name}')
        p.bm.to_mesh(mesh)
        for m in mats:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(f'{name}_{p.name}', mesh)
        bpy.context.scene.collection.objects.link(obj)
        vg = obj.vertex_groups.new(name=p.bone)
        vg.add(list(range(len(mesh.vertices))), 1.0, 'REPLACE')
        objs.append(obj)
        p.bm.free()
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    joined.parent = rig
    mod = joined.modifiers.new('Armature', 'ARMATURE')
    mod.object = rig
    return joined


def mesh_stats(obj):
    me = obj.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    uvs = me.uv_layers.active.data
    uv_ok = all(math.isfinite(c.uv[0]) and math.isfinite(c.uv[1]) for c in uvs)
    us = [c.uv[0] for c in uvs]
    vs = [c.uv[1] for c in uvs]
    return {'triangles': tris, 'vertices': len(me.vertices), 'uv_finite': uv_ok,
            'uv_range': [round(min(us), 4), round(max(us), 4), round(min(vs), 4), round(max(vs), 4)]}


# ========================================================================================================== animacao
def rotx_about(point, deg):
    return Matrix.Translation(point) @ Matrix.Rotation(math.radians(deg), 4, 'X') @ Matrix.Translation(-point)


def roty_about(point, deg):
    return Matrix.Translation(point) @ Matrix.Rotation(math.radians(deg), 4, 'Y') @ Matrix.Translation(-point)


def apply_basis(rig, name, W, parent_pose, frame):
    """Pose do osso `name` tal que a deformacao em espaco do mundo seja W (parent_pose = deformacao ja resolvida do pai)."""
    b = rig.data.bones[name]
    pb = rig.pose.bones[name]
    rest = b.matrix_local
    basis = rest.inverted() @ parent_pose.inverted() @ W @ rest
    loc, rot, scale = basis.decompose()
    pb.rotation_mode = 'QUATERNION'
    pb.location = loc
    pb.rotation_quaternion = rot
    pb.scale = scale
    pb.keyframe_insert('location', frame=frame)
    pb.keyframe_insert('rotation_quaternion', frame=frame)
    pb.keyframe_insert('scale', frame=frame)


def rope_deform(rig, tail):
    """Deformacao (espaco do Root em repouso) do osso Rope: cabeca presa ao tambor, cauda em `tail`."""
    d0, a0 = rope_endpoints()
    L0 = (a0 - d0).length
    d_new = tail - d0
    l = max(d_new.length, 0.03)
    q = (a0 - d0).normalized().rotation_difference(d_new.normalized())
    rb = rig.data.bones['Rope'].matrix_local.to_3x3().to_4x4()
    Sw = rb @ Matrix.Diagonal((1.0, l / L0, 1.0, 1.0)) @ rb.inverted()
    return Matrix.Translation(d0) @ q.to_matrix().to_4x4() @ Sw @ Matrix.Translation(-d0), l


def clip_state(clip, i, n):
    """Estado do quadro `i` (0-based) do clipe: dict com root, phi, wheel_spin, wheels(destroy), arm_off, ammo, rope, winch."""
    t = i / FPS
    S = {'root': Matrix.Identity(4), 'phi': 0.0, 'spin': 0.0, 'wheels': {}, 'winch': 0.0, 'arm_off': Vector((0, 0, 0)),
         'ammo': 1.0, 'rope': ('attached', 0.0, 0.0), 'reload_winch': False}
    if clip == 'Idle':
        u = i / n
        S['root'] = Matrix.Translation((0, 0, 0.004 * math.sin(2 * math.pi * u))) @ rotx_about(Vector((0, 0, 0)), 0.12 * math.sin(2 * math.pi * 2 * u + 0.4))
        S['phi'] = 0.35 * math.sin(2 * math.pi * u + 0.5) + 0.18 * math.sin(2 * math.pi * 3 * u)
        S['winch'] = 0.5 * math.sin(2 * math.pi * 2 * u + 1.0)
    elif clip == 'Move':
        u = i / n
        S['spin'] = 360.0 * u
        S['root'] = Matrix.Translation((0, 0, 0.012 * (0.5 + 0.5 * math.sin(2 * math.pi * 2 * u)))) @ rotx_about(Vector((0, 0, 0)), 0.45 * math.sin(2 * math.pi * 2 * u + 0.8))
        S['phi'] = 1.2 * math.sin(2 * math.pi * 2 * u + 1.2) + 0.5 * math.sin(2 * math.pi * 4 * u)
        S['winch'] = 2.0 * math.sin(2 * math.pi * 2 * u)
    elif clip == 'Fire':
        if t < 0.10:
            S['phi'] = -2.6 * smooth(t / 0.10)
            S['root'] = Matrix.Translation((0, 0, 0.012 * smooth(t / 0.10)))
        elif i <= RELEASE_FRAME:
            u = (t - 0.10) / (RELEASE_FRAME / FPS - 0.10)
            S['phi'] = -2.6 + (ARM_RELEASE_DEG + 2.6) * (u ** 1.8)
            S['root'] = Matrix.Translation((0, 0, 0.012))
        elif i == RELEASE_FRAME + 1:
            S['phi'] = ARM_STOP_DEG
        else:
            ts = t - (RELEASE_FRAME + 1) / FPS
            S['phi'] = ARM_STOP_DEG - 7.0 * math.exp(-ts / 0.11) * abs(math.cos(2 * math.pi * ts / 0.20))
        if i >= RELEASE_FRAME + 1:
            ts = t - (RELEASE_FRAME + 1) / FPS
            dec = math.exp(-ts / 0.16)
            S['root'] = Matrix.Translation((0, 0.012 * dec * math.sin(2 * math.pi * ts / 0.18), -0.028 * dec)) @ rotx_about(Vector((0, 0, 0)), 0.9 * dec * math.cos(2 * math.pi * ts / 0.22))
        S['ammo'] = 1.0 if i < RELEASE_FRAME else 0.001
        S['rope'] = ('release', 0.10, 0.12)
    elif clip == 'Reload':
        if t < 0.30:
            S['phi'] = ARM_STOP_DEG
        elif t < 2.05:
            u = (t - 0.30) / 1.75
            e = 0.5 - 0.5 * math.cos(math.pi * u ** 0.9)
            S['phi'] = ARM_STOP_DEG * (1 - e) + 0.35 * math.sin(2 * math.pi * 7.0 * t) * (1 - abs(2 * u - 1)) * 0.8
        elif t < 2.25:
            ts = t - 2.05
            k = math.sin(math.pi * min(ts / 0.20, 1.0))
            S['phi'] = -1.6 * k * math.exp(-ts / 0.2)
            S['root'] = Matrix.Translation((0, 0, -0.014 * k))
        S['ammo'] = 0.001 if t < 2.15 else min(1.0, 0.001 + (t - 2.15) / (4.0 / FPS))
        S['rope'] = ('reload', 0.0, 0.30)
        S['reload_winch'] = True
    elif clip == 'Hit':
        dec = math.exp(-t / 0.16)
        S['root'] = (Matrix.Translation((0.032 * dec * math.sin(2 * math.pi * 9 * t), 0.012 * dec * math.sin(2 * math.pi * 7 * t + 1),
                                         0.006 * dec * math.sin(2 * math.pi * 11 * t))) @ rotx_about(Vector((0, 0, 0)), 0.9 * dec * math.sin(2 * math.pi * 8 * t + 1.0)))
        S['phi'] = 2.4 * math.exp(-t / 0.2) * math.sin(2 * math.pi * 6 * t)
        S['winch'] = 5.0 * dec * math.sin(2 * math.pi * 8 * t)
        S['spin'] = 2.0 * dec * math.sin(2 * math.pi * 10 * t)
    elif clip == 'Destroy':
        destroy_state(S, t)
    return S


def destroy_state(S, t):
    """Colapso: tomba sobre o lado direito (-X), o braco cai para a frente, as rodas da direita se soltam e deitam."""
    k = smooth(t / 1.6)
    kf = smooth(t / 1.15)
    jolt = 0.05 * math.sin(math.pi * t / 0.18) if t < 0.18 else 0.0
    pivot = Vector((WHEEL_X, 0.0, 0.0))
    S['root'] = Matrix.Translation((0, 0, jolt - 0.05 * k)) @ roty_about(pivot, -15.0 * kf) @ Matrix.Rotation(math.radians(2.5 * k), 4, 'Z')
    kk = max(0.0, (t - 0.12) / 0.95)
    fall = min(kk * kk, 1.0)
    phi = ARM_STOP_DEG + (228.0 - ARM_STOP_DEG) * fall
    if kk > 1.0:
        phi += -4.0 * math.exp(-(kk - 1.0) * 6.0) * abs(math.cos((kk - 1.0) * 14.0))
    S['phi'] = phi
    S['arm_off'] = Vector((0.0, -0.48 * fall, -0.36 * fall))
    for name, sx, sy, delay, tgt, yaw in (('Wheel_FR', -1, -1, 0.10, Vector((-1.65, -1.25, 0.075)), 18.0),
                                          ('Wheel_BR', -1, 1, 0.30, Vector((-1.55, 1.30, 0.075)), -26.0)):
        s = smooth((t - delay) / 1.25)
        c0 = Vector((sx * WHEEL_X, sy * WHEEL_Y, WHEEL_Z))
        pos = c0.lerp(tgt, s)
        pos.z += 0.35 * math.sin(math.pi * min(s, 1.0)) * (1.0 - s)
        tilt = 90.0 * smooth((t - delay) / 1.0)
        S['wheels'][name] = (pos, tilt, 200.0 * s, yaw * s)
    S['wheel_spin_extra'] = {'Wheel_FL': 6.0 * kf, 'Wheel_BL': -5.0 * kf}
    S['winch'] = 25.0 * smooth((t - 0.2) / 0.9)
    S['rope'] = ('release', 0.12, 0.10)


def animate_clip(rig, clip):
    loop = clip in ('Idle', 'Move')
    n = CLIP_FRAMES[clip]
    count = n + 1 if loop else n
    action = bpy.data.actions.new(clip)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    drum_top, hook_rest = rope_endpoints()
    hang = drum_top + Vector((0.0, 0.05, -0.17))
    cupc = BONES['Ammo'][0]
    wheels = (('Wheel_FL', 1, -1), ('Wheel_FR', -1, -1), ('Wheel_BL', 1, 1), ('Wheel_BR', -1, 1))
    rope_len_ref = None
    for i in range(count):
        frame = i + 1
        S = clip_state(clip, i, n)
        t = i / FPS
        root = S['root']
        apply_basis(rig, 'Root', root, Matrix.Identity(4), frame)
        arm_local = Matrix.Translation(S['arm_off']) @ rotx_about(PIVOT, S['phi'])
        arm_world = root @ arm_local
        apply_basis(rig, 'Arm', arm_world, root, frame)
        sc = S['ammo']
        scale_m = Matrix.Translation(cupc) @ Matrix.Diagonal((sc, sc, sc, 1.0)) @ Matrix.Translation(-cupc)
        apply_basis(rig, 'Ammo', arm_world @ scale_m, arm_world, frame)
        for name, sx, sy in wheels:
            c = BONES[name][0]
            if name in S['wheels']:
                pos, tilt, sp, yaw = S['wheels'][name]
                W = (Matrix.Translation(pos - c) @ Matrix.Translation(c) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
                     @ Matrix.Rotation(math.radians(-tilt), 4, 'Y') @ Matrix.Rotation(math.radians(sp), 4, 'X') @ Matrix.Translation(-c))
            else:
                extra = S.get('wheel_spin_extra', {}).get(name, 0.0)
                W = root @ rotx_about(c, S['spin'] + extra)
            apply_basis(rig, name, W, root, frame)
        # corda (espaco do Root em repouso)
        mode, t0, dur = S['rope']
        hook_now = arm_local @ hook_rest
        if mode == 'attached':
            tail = hook_now
        elif mode == 'release':
            tail = hook_now.lerp(hang, smooth((t - t0) / dur))
        else:
            tail = hang.lerp(hook_now, smooth((t - t0) / dur))
        rdef, rlen = rope_deform(rig, tail)
        apply_basis(rig, 'Rope', root @ rdef, root, frame)
        winch = S['winch']
        if S['reload_winch']:
            if t >= 0.30:
                if rope_len_ref is None:
                    rope_len_ref = rlen
                winch = -math.degrees((rope_len_ref - rlen) / DRUM_R)
        apply_basis(rig, 'Winch', root @ rotx_about(DRUM_C, winch), root, frame)
    action.frame_range = (1, count)
    return action


# ========================================================================================================== exportacao
def export_fbx(path, objects, rig, action):
    scene = bpy.context.scene
    scene.render.fps = FPS
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.animation_data_create()
    rig.animation_data.action = action
    if action is not None and action.slots:
        rig.animation_data.action_slot = action.slots[0]
    if action is not None:
        scene.frame_start, scene.frame_end = (int(round(x)) for x in action.frame_range)
    else:
        scene.frame_start = scene.frame_end = 1
        for pb in rig.pose.bones:
            pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
    scene.frame_set(scene.frame_start)
    types = {'ARMATURE', 'MESH', 'EMPTY'}  # mesmos tipos do modelo: o Unity so mantem os caminhos 'CatapultArmature/Root/...' com a mesma hierarquia
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types=types,
        use_armature_deform_only=False, add_leaf_bones=False, axis_forward='-Z', axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', use_tspace=False, path_mode='STRIP',
        embed_textures=False, bake_anim=action is not None, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0)


def reset_pose(rig):
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)


def main():
    for d in (MODELS, TEXTURES, WORK, REPORT):
        d.mkdir(parents=True, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    build_textures()
    mats = make_materials()
    rig = make_armature()
    report = {'unit': 'catapult', 'lods': [], 'parts': {}, 'clips': {}, 'bones': list(BONES.keys())}
    meshes = []
    for lod in range(3):
        parts = build_parts(lod)
        info = []
        obj = parts_to_object(parts, f'Catapult_LOD{lod}', mats, rig, info)
        st = mesh_stats(obj)
        report['lods'].append(st)
        report['parts'][f'LOD{lod}'] = info
        meshes.append(obj)
        print('LOD', lod, st)
    # socket do projetil: filho do osso Arm, no centro da concha (pose armada); sai junto com o braco
    sock = bpy.data.objects.new('ProjectileSpawn', None)
    sock.empty_display_type = 'PLAIN_AXES'
    sock.empty_display_size = 0.08
    bpy.context.scene.collection.objects.link(sock)
    sock.parent = rig
    sock.parent_type = 'BONE'
    sock.parent_bone = 'Arm'
    bpy.context.view_layer.update()
    sock.matrix_world = Matrix.Translation(BONES['Ammo'][0])
    # dimensoes
    allv = [rig.matrix_world @ v.co for m in meshes[:1] for v in m.data.vertices]
    mn = Vector((min(v.x for v in allv), min(v.y for v in allv), min(v.z for v in allv)))
    mx = Vector((max(v.x for v in allv), max(v.y for v in allv), max(v.z for v in allv)))
    report['bounds_rest'] = {'min': [round(c, 3) for c in mn], 'max': [round(c, 3) for c in mx], 'size': [round(c, 3) for c in (mx - mn)]}
    report['release_frame'] = RELEASE_FRAME
    report['release_seconds'] = RELEASE_FRAME / FPS
    report['projectile_spawn_blender'] = [round(c, 4) for c in BONES['Ammo'][0]]
    report['pivot_blender'] = [round(c, 4) for c in PIVOT]
    report['wheel_circumference'] = round(2 * math.pi * WHEEL_R, 4)
    # modelo
    export_fbx(MODELS / 'Catapult.fbx', [rig] + meshes + [sock], rig, None)
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'Catapult.blend'), compress=True)
    # clipes
    for clip in CLIP_FRAMES:
        action = animate_clip(rig, clip)
        export_fbx(MODELS / f'Catapult@{clip}.fbx', [rig, meshes[2], sock], rig, action)
        report['clips'][clip] = {'frames': int(action.frame_range[1]), 'seconds': (int(action.frame_range[1]) - 1) / FPS, 'loop': clip in ('Idle', 'Move')}
        reset_pose(rig)
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'Catapult.blend'), compress=True)
    (REPORT / 'build-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('CATAPULT_BUILD_OK', json.dumps(report['lods']))


if __name__ == '__main__':
    main()
