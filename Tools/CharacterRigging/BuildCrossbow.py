"""Balestra (balista de carreta) do Era Imperial, modelada 100% proceduralmente: obra do projeto, sem terceiros.

Executar:  blender -b --factory-startup --python-exit-code 1 --python BuildCrossbow.py
Gera (tudo em metros, 1 unidade = 1 m; Blender +Z cima, frente da unidade = -Y; exportado com axis_forward=-Z, axis_up=Y,
ou seja, no Unity a frente e +Z):
  Assets/CharacterRigging/Crossbow/Models/Crossbow.fbx         malha skinned (3 LODs) + esqueleto generico
  Assets/CharacterRigging/Crossbow/Models/Crossbow@<Clip>.fbx  Idle, Move, Reload, Fire, Hit, Destroy (armature + malha LOD2)
  Assets/CharacterRigging/Crossbow/Accessories/Crossbow_Bolt.fbx  virote separado (projetil), pivo na PONTA
  Assets/CharacterRigging/Crossbow/Textures/*.png              texturas procedurais leves
  Tools/CharacterRigging/Working/Crossbow/Crossbow.blend e crossbow-build.json (sockets, medidas)
  Docs/CharacterMeshAudit/Crossbow/build-report.json
Esqueleto: Root > Wheel_L, Wheel_R, LimbL > LimbL_Tip, LimbR > LimbR_Tip, String, Bolt, Winch, Trigger.
Lado "L" = +X do Blender (esquerda da unidade no Unity). Estado base (bind) = ARMADA e CARREGADA (corda puxada, virote no trilho).
"""
from pathlib import Path
import bpy, bmesh, math, json, sys
import numpy as np
from mathutils import Vector, Matrix, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT_MODELS = ROOT / 'Assets/CharacterRigging/Crossbow/Models'
OUT_ACC = ROOT / 'Assets/CharacterRigging/Crossbow/Accessories'
OUT_TEX = ROOT / 'Assets/CharacterRigging/Crossbow/Textures'
WORK = ROOT / 'Tools/CharacterRigging/Working/Crossbow'
REPORT = ROOT / 'Docs/CharacterMeshAudit/Crossbow'
FPS = 30

# ------------------------------------------------------------------------------------------------ dimensoes (m)
WX, WY, WR = 0.66, -0.20, 0.55          # roda: x do centro, y do centro, raio (centro em z = WR)
HEAD_Y = -0.93                          # centro do cabecote / arco
LIMB_Z = 1.075                           # altura do eixo dos bracos
LIMB_X0, LIMB_X1 = 0.12, 0.88           # braco: raiz (dentro da bochecha) e ponta
LIMB_BACK = 0.30                        # quanto a ponta do braco fica atras do centro do arco (armado)
Y_C = -0.46                             # centro da corda armada
Z_STR = 1.0575
BOLT_TIP_Y, BOLT_LEN, Z_BOLT = -1.05, 0.60, 1.0575
Y_BOLT_TAIL = BOLT_TIP_Y + BOLT_LEN     # -0.52
STOCK_Y0, STOCK_Y1 = -1.13, 0.80
STOCK_Z0, STOCK_Z1, LIP_Z1 = 0.87, 1.03, 1.062
WINCH_Y, WINCH_Z = 0.58, 1.17
TRAIL_Y = 1.22
BONES = ['Root', 'Wheel_L', 'Wheel_R', 'LimbL', 'LimbL_Tip', 'LimbR', 'LimbR_Tip', 'String', 'Bolt', 'Winch', 'Trigger']
BONE_INDEX = {n: i for i, n in enumerate(BONES)}
MAT_NAMES = ['Crossbow_Wood', 'Crossbow_DarkWood', 'Crossbow_Iron', 'Crossbow_Rope', 'Crossbow_Feather']
WOOD, DARK, IRON, ROPE, FEATHER = range(5)
LOD_LIMITS = (8000, 3000, 1000)


def smooth(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def limb_point(s, t):
    """Linha de centro do braco do lado s (+1 = L) em t em [0,1], estado armado."""
    return Vector((s * (LIMB_X0 + (LIMB_X1 - LIMB_X0) * t), HEAD_Y + LIMB_BACK * t ** 1.8, LIMB_Z))


# ------------------------------------------------------------------------------------------------ texturas procedurais
def blur_wrap(a, k):
    """Suavizacao gaussiana circular (tileavel) por FFT."""
    h, w = a.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    g = np.exp(-2 * (np.pi ** 2) * (k ** 2) * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g))


def noise(size, k, seed):
    n = np.random.default_rng(seed).standard_normal((size, size))
    n = blur_wrap(n, k)
    return (n - n.mean()) / (n.std() + 1e-9)


def save_png(name, rgba):
    OUT_TEX.mkdir(parents=True, exist_ok=True)
    h, w, _ = rgba.shape
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = str(OUT_TEX / (name + '.png'))
    img.file_format = 'PNG'
    img.save()
    return img


def make_textures():
    s = 512
    y, x = np.mgrid[0:s, 0:s] / s
    # madeira: aneis com fibras ao longo de U (x)
    warp = 0.9 * np.sin(2 * np.pi * (2 * x + 0.3)) + 0.6 * noise(s, 5, 1)
    rings = np.sin(2 * np.pi * (14 * y + warp))
    fibre = noise(s, 1.2, 2) * 0.5 + np.sin(2 * np.pi * 85 * y + 3 * noise(s, 8, 3)) * 0.25
    t = 0.55 + 0.13 * rings + 0.05 * fibre + 0.025 * noise(s, 14, 4)
    t = np.clip(t, 0, 1)[..., None]
    dark, light = np.array([0.40, 0.26, 0.13]), np.array([0.74, 0.55, 0.32])
    rgb = dark + (light - dark) * t
    save_png('Crossbow_Wood', np.dstack([rgb, np.ones((s, s))]))
    # ferro: cinza escuro manchado, leve ferrugem
    s2 = 256
    n1, n2 = noise(s2, 3, 5), noise(s2, 0.8, 6)
    base = 0.34 + 0.025 * n1 + 0.015 * n2
    rust = np.clip(noise(s2, 6, 7) - 1.3, 0, 1)[..., None] * np.array([0.10, 0.04, 0.01])
    rgb = np.clip(base[..., None] * np.array([1.0, 1.0, 1.05]) + rust, 0, 1)
    save_png('Crossbow_Iron', np.dstack([rgb, np.ones((s2, s2))]))
    # corda: fios torcidos na diagonal
    s3 = 128
    y3, x3 = np.mgrid[0:s3, 0:s3] / s3
    twist = 0.5 + 0.5 * np.sin(2 * np.pi * (10 * y3 + 4 * x3))
    rgb = np.array([0.55, 0.45, 0.30]) + (np.array([0.82, 0.72, 0.52]) - np.array([0.55, 0.45, 0.30])) * twist[..., None]
    rgb += 0.04 * noise(s3, 0.8, 8)[..., None]
    save_png('Crossbow_Rope', np.dstack([rgb, np.ones((s3, s3))]))
    # efeitos: fumaca/poeira suave e fagulha
    s4 = 128
    yy, xx = np.mgrid[0:s4, 0:s4] / (s4 - 1) * 2 - 1
    r = np.sqrt(xx ** 2 + yy ** 2)
    puff = np.clip(1 - r, 0, 1) ** 1.6 * (0.78 + 0.22 * noise(s4, 2.5, 9))
    puff = np.clip(puff, 0, 1)
    save_png('Crossbow_Smoke', np.dstack([np.ones((s4, s4, 3)) * 0.92, puff]))
    s5 = 64
    yy, xx = np.mgrid[0:s5, 0:s5] / (s5 - 1) * 2 - 1
    r = np.sqrt(xx ** 2 + yy ** 2)
    spark = np.clip(1 - r, 0, 1) ** 2.2
    save_png('Crossbow_Spark', np.dstack([np.ones((s5, s5)), 0.85 * np.ones((s5, s5)), 0.55 * np.ones((s5, s5)), spark]))


def make_materials():
    def tex_material(name, tex, tint=(1, 1, 1), rough=0.6, metal=0.0):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bs = m.node_tree.nodes['Principled BSDF']
        bs.inputs['Roughness'].default_value = rough
        bs.inputs['Metallic'].default_value = metal
        if tex is not None:
            img = bpy.data.images[tex]
            node = m.node_tree.nodes.new('ShaderNodeTexImage')
            node.image = img
            if tuple(tint) != (1, 1, 1):
                mix = m.node_tree.nodes.new('ShaderNodeMix')
                mix.data_type = 'RGBA'
                mix.blend_type = 'MULTIPLY'
                mix.inputs[0].default_value = 1.0
                mix.inputs[7].default_value = (*tint, 1)
                m.node_tree.links.new(node.outputs['Color'], mix.inputs[6])
                m.node_tree.links.new(mix.outputs[2], bs.inputs['Base Color'])
            else:
                m.node_tree.links.new(node.outputs['Color'], bs.inputs['Base Color'])
        return m
    return [
        tex_material('Crossbow_Wood', 'Crossbow_Wood', rough=0.65),
        tex_material('Crossbow_DarkWood', 'Crossbow_Wood', tint=(0.62, 0.56, 0.52), rough=0.7),
        tex_material('Crossbow_Iron', 'Crossbow_Iron', rough=0.45, metal=0.6),
        tex_material('Crossbow_Rope', 'Crossbow_Rope', rough=0.9),
        tex_material('Crossbow_Feather', None, rough=0.9),
    ]


# ------------------------------------------------------------------------------------------------ construtor de malha
class MeshBuilder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.dl = self.bm.verts.layers.deform.verify()

    def vert(self, co, w):
        v = self.bm.verts.new(Vector(co))
        weights = w(v.co) if callable(w) else ({w: 1.0} if isinstance(w, str) else w)
        for name, val in weights.items():
            if val > 1e-4:
                v[self.dl][BONE_INDEX[name]] = val
        return v

    def face(self, verts, mat, uvs, smooth_=False):
        f = self.bm.faces.new(verts)
        f.material_index = mat
        f.smooth = smooth_
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def fix(self, faces):
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    def tris(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)


def obox(b, c, half, mat, w, axes=None, grain=1, tile=0.5, off=0.0):
    c = Vector(c)
    ax, ay, az = axes if axes else (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    ex, h = [ax, ay, az], list(half)
    verts, faces = {}, []

    def V(sg):
        key = tuple(sg)
        if key not in verts:
            verts[key] = b.vert(c + ex[0] * h[0] * sg[0] + ex[1] * h[1] * sg[1] + ex[2] * h[2] * sg[2], w)
        return verts[key]
    order = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for i in range(3):
        a, bb = (i + 1) % 3, (i + 2) % 3
        for s in (-1, 1):
            def corner(o):
                sg = [0, 0, 0]
                sg[i], sg[a], sg[bb] = s, o[0], o[1]
                return sg
            od = order if s > 0 else order[::-1]
            if grain in (a, bb):
                ua, va = grain, (a if grain == bb else bb)
            else:
                ua, va = a, bb
            uvs = []
            for o in od:
                sg = corner(o)
                uvs.append((sg[ua] * h[ua] / tile + off, sg[va] * h[va] / tile))
            faces.append(b.face([V(corner(o)) for o in od], mat, uvs))
    b.fix(faces)


def frame_from(t, up):
    t = Vector(t).normalized()
    s = t.cross(Vector(up))
    if s.length < 1e-4:
        s = t.cross(Vector((1, 0, 0)))
    s.normalize()
    v = s.cross(t).normalized()
    return t, s, v


def beam(b, p0, p1, wd, ht, mat, w, up=(0, 0, 1), grain=0, tile=0.5, off=0.0):
    p0, p1 = Vector(p0), Vector(p1)
    L = (p1 - p0).length
    t, s, v = frame_from(p1 - p0, up)
    obox(b, (p0 + p1) / 2, (L / 2, wd / 2, ht / 2), mat, w, axes=(t, s, v), grain=grain, tile=tile, off=off)


def tube(b, pts, radii, sides, mat, w, up=(0, 0, 1), su=1.0, sv=1.0, caps=(True, True), tile=0.5, smooth_=True):
    pts = [Vector(p) for p in pts]
    rings, lens, acc = [], [], 0.0
    for i, p in enumerate(pts):
        tg = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        t, u, v = frame_from(tg, up)
        if i > 0:
            acc += (pts[i] - pts[i - 1]).length
        lens.append(acc)
        rings.append([b.vert(p + u * math.cos(2 * math.pi * k / sides) * radii[i] * su + v * math.sin(2 * math.pi * k / sides) * radii[i] * sv, w)
                      for k in range(sides)])
    faces = []
    for i in range(len(rings) - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            uvs = [(lens[i] / tile, k / sides), (lens[i] / tile, (k + 1) / sides), (lens[i + 1] / tile, (k + 1) / sides), (lens[i + 1] / tile, k / sides)]
            faces.append(b.face([rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]], mat, uvs, smooth_))
    for ring, flip, enabled in ((rings[0], True, caps[0]), (rings[-1], False, caps[1])):
        if not enabled:
            continue
        vs = ring[::-1] if flip else ring
        uvs = [(0.5 + 0.5 * math.cos(2 * math.pi * k / sides), 0.5 + 0.5 * math.sin(2 * math.pi * k / sides)) for k in range(sides)]
        faces.append(b.face(vs, mat, uvs, False))
    b.fix(faces)


def cyl(b, p0, p1, r0, r1, sides, mat, w, up=(0, 0, 1), caps=(True, True), tile=0.5):
    tube(b, [p0, p1], [r0, r1], sides, mat, w, up=up, caps=caps, tile=tile, smooth_=True)


def ring_loop(b, center, axis, radius, half_axial, half_radial, segs, mat, w, tile=0.5):
    """Anel fechado de secao retangular (aro de roda). axis = eixo da roda."""
    axis = Vector(axis).normalized()
    e1 = axis.cross(Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))).normalized()
    e2 = axis.cross(e1).normalized()
    center = Vector(center)
    rings = []
    for i in range(segs):
        th = 2 * math.pi * i / segs
        d = e1 * math.cos(th) + e2 * math.sin(th)
        base = center + d * radius
        rings.append([b.vert(base + axis * half_axial + d * half_radial, w), b.vert(base + axis * half_axial - d * half_radial, w),
                      b.vert(base - axis * half_axial - d * half_radial, w), b.vert(base - axis * half_axial + d * half_radial, w)])
    faces = []
    arc = 2 * math.pi * radius / segs
    for i in range(segs):
        j = (i + 1) % segs
        for k in range(4):
            k2 = (k + 1) % 4
            uvs = [(i * arc / tile, k / 4), (i * arc / tile, (k + 1) / 4), ((i + 1) * arc / tile, (k + 1) / 4), ((i + 1) * arc / tile, k / 4)]
            faces.append(b.face([rings[i][k], rings[i][k2], rings[j][k2], rings[j][k]], mat, uvs))
    b.fix(faces)


def gear(b, center, n, r_root, r_tip, half_thick, mat, w, axis=(1, 0, 0)):
    axis = Vector(axis).normalized()
    e1 = axis.cross(Vector((0, 0, 1))).normalized()
    e2 = axis.cross(e1).normalized()
    c = Vector(center)
    top, bot = [], []
    for i in range(2 * n):
        th = math.pi * i / n
        r = r_tip if i % 2 == 0 else r_root
        d = e1 * math.cos(th) + e2 * math.sin(th)
        top.append(b.vert(c + axis * half_thick + d * r, w))
        bot.append(b.vert(c - axis * half_thick + d * r, w))
    faces = []
    m = 2 * n
    for i in range(m):
        j = (i + 1) % m
        faces.append(b.face([top[i], top[j], bot[j], bot[i]], mat, [(0, 0), (0.1, 0), (0.1, 0.1), (0, 0.1)]))
    uvc = [(0.5 + 0.5 * math.cos(math.pi * i / n), 0.5 + 0.5 * math.sin(math.pi * i / n)) for i in range(m)]
    faces.append(b.face(top, mat, uvc))
    faces.append(b.face(bot[::-1], mat, uvc[::-1]))
    b.fix(faces)


def quad_double(b, pts, mat, w, uv=((0, 0), (1, 0), (1, 1), (0, 1))):
    """Quadro de uma face dupla (penas): duas faces com vertices proprios."""
    f1 = b.face([b.vert(p, w) for p in pts], mat, list(uv))
    f2 = b.face([b.vert(p, w) for p in pts[::-1]], mat, list(uv[::-1]))
    return f1, f2


# ------------------------------------------------------------------------------------------------ pecas
def build_wheel(b, s, d):
    wn = 'Wheel_L' if s > 0 else 'Wheel_R'
    cx = s * WX
    C = Vector((cx, WY, WR))
    sd = (10, 8, 5)[d]
    cyl(b, C + Vector((-0.085, 0, 0)), C + Vector((0.085, 0, 0)), 0.072, 0.072, sd, WOOD, wn)
    if d < 2:
        for dx in (-0.07, 0.07):
            cyl(b, C + Vector((dx - 0.012, 0, 0)), C + Vector((dx + 0.012, 0, 0)), 0.085, 0.085, sd, IRON, wn)
    if d == 0:
        cyl(b, C + Vector((s * 0.085, 0, 0)), C + Vector((s * 0.135, 0, 0)), 0.042, 0.038, 8, IRON, wn)   # ponta do eixo + contrapino
    n = (8, 6, 4)[d]
    for i in range(n):
        a = 2 * math.pi * i / n + math.pi / n
        dv = Vector((0, math.sin(a), math.cos(a)))
        cyl(b, C + dv * 0.06, C + dv * 0.50, 0.024, 0.017, (6, 4, 4)[d], WOOD, wn, caps=(False, False) if d == 0 else (True, True))
    segs = (24, 16, 8)[d]
    ring_loop(b, C, (1, 0, 0), 0.503, 0.044, 0.035, segs, DARK, wn)
    ring_loop(b, C, (1, 0, 0), 0.5435, 0.050, 0.0065, segs, IRON, wn)
    if d == 0:
        for i in range(12):
            a = 2 * math.pi * i / 12
            obox(b, C + Vector((s * 0.052, math.sin(a) * 0.5435, math.cos(a) * 0.5435)), (0.005, 0.009, 0.006), IRON, wn)


def build_body(b, d):
    R = 'Root'
    # eixo
    cyl(b, (-0.80, WY, WR), (0.80, WY, WR), 0.035, 0.035, (10, 8, 6)[d], IRON, R, up=(0, 1, 0), caps=(d == 0, d == 0))
    # chassi: longarinas, travessas e leito do eixo
    for sx in (-1, 1):
        beam(b, (sx * 0.19, -0.92, 0.77), (sx * 0.19, 1.00, 0.77), 0.10, 0.14, DARK, R, up=(0, 0, 1), grain=0)
        obox(b, (sx * 0.19, WY, 0.645), (0.05, 0.10, 0.055), DARK, R)                     # leito do eixo
    for y, hz in ((-0.86, 0.045), (0.40, 0.045), (0.94, 0.045)) if d < 2 else ((0.40, 0.045),):
        obox(b, (0, y, 0.76), (0.245, 0.05, hz), DARK, R, grain=0)
    if d < 2:
        obox(b, (0, WY, 0.76), (0.245, 0.06, 0.05), WOOD, R, grain=0)
    # tabuas do convés
    nplank = (4, 2, 1)[d]
    wid = 0.50 / nplank
    for i in range(nplank):
        x = -0.25 + wid * (i + 0.5)
        obox(b, (x, 0.06, 0.855), (wid / 2 - 0.004, 0.86, 0.018), WOOD, R, grain=1, off=i * 0.23)
    # coronha / estoque com trilho (canal do virote)
    obox(b, (0, (STOCK_Y0 + STOCK_Y1) / 2, (STOCK_Z0 + STOCK_Z1) / 2), (0.10, (STOCK_Y1 - STOCK_Y0) / 2, (STOCK_Z1 - STOCK_Z0) / 2), DARK, R, grain=1, off=0.1)
    for sx in (-1, 1):
        obox(b, (sx * 0.044, (BOLT_TIP_Y - 0.01 + 0.30) / 2, (STOCK_Z1 + LIP_Z1) / 2),
             (0.016, (0.30 - (BOLT_TIP_Y - 0.01)) / 2, (LIP_Z1 - STOCK_Z1) / 2), DARK, R, grain=1)  # labios do canal
    obox(b, (0, (BOLT_TIP_Y - 0.01 + 0.30) / 2, STOCK_Z1 + 0.006), (0.012, (0.30 - (BOLT_TIP_Y - 0.01)) / 2, 0.006), IRON, R, grain=1)  # trilho de apoio do virote
    obox(b, (0, STOCK_Y0 - 0.01, 0.95), (0.105, 0.015, 0.088), IRON, R)                      # capa de ferro do nariz
    if d < 2:
        for y in (-1.02, -0.30, 0.45):
            obox(b, (0, y, (STOCK_Z0 + STOCK_Z1) / 2 + 0.002), (0.108, 0.018, 0.086), IRON, R)  # cintas
        obox(b, (0, STOCK_Y1 + 0.006, 0.95), (0.105, 0.008, 0.08), IRON, R)                   # chapa da coronha
    # cabecote: bochechas (deixam o canal livre), travessa de topo e montantes
    for sx in (-1, 1):
        obox(b, (sx * 0.105, HEAD_Y, 1.06), (0.065, 0.11, 0.10), DARK, R, grain=1)
        beam(b, (sx * 0.105, HEAD_Y - 0.01, 1.16), (sx * 0.088, HEAD_Y - 0.01, 1.40), 0.07, 0.07, WOOD, R, up=(0, 1, 0), grain=0)
        if d < 2:
            beam(b, (sx * 0.15, HEAD_Y + 0.11, 0.99), (sx * 0.105, HEAD_Y - 0.10, 1.34), 0.035, 0.035, DARK, R, up=(1, 0, 0), grain=0)  # escora
            obox(b, (sx * 0.105, HEAD_Y, 1.19), (0.078, 0.065, 0.016), IRON, R)               # cinta de ferro
    obox(b, (0, HEAD_Y - 0.01, 1.425), (0.15, 0.055, 0.03), DARK, R, grain=0)
    if d < 2:
        obox(b, (0, HEAD_Y - 0.01, 1.462), (0.13, 0.04, 0.008), IRON, R)
    # cabrestante: bochechas laterais e mancais
    for sx in (-1, 1) if d < 2 else ():
        obox(b, (sx * 0.145, WINCH_Y, 1.095), (0.027, 0.085, 0.085), DARK, R, grain=2)
    if d < 2:
        obox(b, (0, WINCH_Y, 1.0), (0.10, 0.07, 0.03), DARK, R)
        # gatilho: caixa da noz, guarda-mato
        obox(b, (0, 0.12, 0.915), (0.115, 0.14, 0.05), IRON, R)
        beam(b, (0, 0.04, 0.865), (0, 0.04, 0.80), 0.04, 0.03, IRON, R, up=(0, 1, 0))
        beam(b, (0, 0.04, 0.80), (0, 0.30, 0.80), 0.04, 0.03, IRON, R, up=(1, 0, 0))
        beam(b, (0, 0.30, 0.80), (0, 0.26, 0.87), 0.04, 0.03, IRON, R, up=(1, 0, 0))
        obox(b, (0.108, WINCH_Y + 0.09, 1.12), (0.014, 0.012, 0.03), IRON, R, grain=2)         # tranca/catraca fixa
    # apoio traseiro: duas pernas abertas com sapata de ferro e travessa
    for sx in (-1, 1):
        beam(b, (sx * 0.17, 0.95, 0.74), (sx * 0.36, TRAIL_Y - 0.02, 0.07), 0.075, 0.075, DARK, R, up=(0, 1, 0), grain=0)
        if d < 2:
            obox(b, (sx * 0.365, TRAIL_Y - 0.005, 0.03), (0.055, 0.075, 0.03), IRON, R)
    if d < 2:
        beam(b, (-0.275, 1.12, 0.38), (0.275, 1.12, 0.38), 0.05, 0.045, DARK, R, up=(0, 1, 0), grain=0)
    # suporte de virotes sobressalentes no convés (lado -X)
    if d < 2:
        obox(b, (-0.18, 0.12, 0.885), (0.07, 0.30, 0.012), DARK, R, grain=1)
        for y in (-0.17, 0.41):
            obox(b, (-0.18, y, 0.915), (0.07, 0.012, 0.03), DARK, R)
        spare = [(-0.145, 0.9125), (-0.215, 0.9125)] + ([(-0.18, 0.94)] if d == 0 else [])
        for sx, sz in spare:
            build_bolt(b, d, tip=(sx, -0.17, sz), w=R)


def build_limb(b, s, d):
    n = 'L' if s > 0 else 'R'
    nseg = (14, 8, 4)[d]

    def w(p):
        t = (abs(p.x) - LIMB_X0) / (LIMB_X1 - LIMB_X0)
        wt = smooth((t - 0.28) / 0.5)
        return {f'Limb{n}': 1 - wt, f'Limb{n}_Tip': wt}
    ts = [i / nseg for i in range(nseg + 1)]
    pts = [limb_point(s, t) for t in ts]
    tube(b, pts, [0.034 - 0.015 * t for t in ts], (8, 6, 4)[d], WOOD, w, up=(0, 0, 1), su=0.85, sv=1.40, tile=0.6)
    bands = [(0.09, 0.16), (0.44, 0.50), (0.92, 0.985)] if d < 2 else [(0.92, 0.985)]
    for t0, t1 in bands:
        k = max(2, int(round((t1 - t0) * nseg)) + 1)
        tt = [t0 + (t1 - t0) * i / (k - 1) for i in range(k)]
        tube(b, [limb_point(s, t) for t in tt], [0.034 - 0.015 * t + 0.005 for t in tt], (8, 6, 4)[d], IRON, w, up=(0, 0, 1), su=0.85, sv=1.40)
    if d < 2:
        tip = limb_point(s, 1.0)
        cyl(b, tip + Vector((0, 0.004, 0)), tip + Vector((0, 0.030, 0)), 0.013, 0.010, 6, IRON, w, up=(0, 0, 1))   # noz da corda


def build_string(b, d):
    # corda (3 trechos por lado) + carro/gatilho preso a ela + cordas do cabrestante
    T = {1: limb_point(1, 1.0) + Vector((0, 0.020, 0)), -1: limb_point(-1, 1.0) + Vector((0, 0.020, 0))}
    Cc = Vector((0, Y_C, Z_STR))
    nh = (4, 3, 2)[d]

    def w(p):
        s_ = min(1.0, abs(p.x) / abs(T[1].x))
        tipname = 'LimbL_Tip' if p.x >= 0 else 'LimbR_Tip'
        return {'String': 1 - s_, tipname: s_} if s_ > 0.001 else {'String': 1.0}
    pts = [T[1].lerp(Cc, i / nh) for i in range(nh)] + [Cc] + [Cc.lerp(T[-1], i / nh) for i in range(1, nh + 1)]
    tube(b, pts, [0.0075] * len(pts), (5, 4, 3)[d], ROPE, w, up=(0, 0, 1), tile=0.25)
    # carro (trava da corda): corpo no canal + placa que corre sobre os labios
    obox(b, (0, Y_C + 0.06, 1.052), (0.016, 0.075, 0.018), IRON, 'String', grain=1)
    obox(b, (0, Y_C + 0.065, 1.086), (0.056, 0.06, 0.012), IRON, 'String', grain=1)
    if d < 2:
        for sx in (-1, 1):
            obox(b, (sx * 0.049, Y_C + 0.012, 1.068), (0.007, 0.012, 0.02), IRON, 'String')    # alças da corda
    # cordas do cabrestante: do carro ao tambor (esticam/afrouxam conforme a corda e puxada)
    y0, y1 = Y_C + 0.13, WINCH_Y - 0.03
    for sx in (-1, 1):
        def wr(p, y0=y0, y1=y1):
            f = smooth((p.y - y0) / (y1 - y0))
            return {'String': 1 - f, 'Root': f} if f > 0.001 else {'String': 1.0}
        k = (6, 3, 2)[d]
        ps = [Vector((sx * 0.03 * (1 - 0.0), y0 + (y1 - y0) * i / (k - 1), 1.092 + 0.022 * (i / (k - 1)))) for i in range(k)]
        tube(b, ps, [0.0065] * k, (5, 4, 3)[d], ROPE, wr, up=(1, 0, 0), tile=0.25)


def build_winch(b, d):
    W = 'Winch'
    C = Vector((0, WINCH_Y, WINCH_Z))
    cyl(b, C + Vector((-0.17, 0, 0)), C + Vector((0.17, 0, 0)), 0.060, 0.060, (14, 10, 6)[d], DARK, W)
    cyl(b, C + Vector((-0.20, 0, 0)), C + Vector((0.20, 0, 0)), 0.020, 0.020, 6, IRON, W, caps=(d == 0, d == 0)) if d < 2 else None
    for sx in (-1, 1) if d < 2 else ():
        cyl(b, C + Vector((sx * 0.118, 0, 0)), C + Vector((sx * 0.130, 0, 0)), 0.078, 0.078, (14, 10, 6)[d], IRON, W)
    if d < 2:
        gear(b, C + Vector((-0.172, 0, 0)), 8 if d == 0 else 6, 0.075, 0.098, 0.010, IRON, W)             # roda de catraca
    # manivelas: braço radial + punho (um em cada ponta, defasados)
    for sx, ang in ((1, math.radians(40)), (-1, math.radians(220))):
        dv = Vector((0, math.sin(ang), math.cos(ang)))  # (y, z)
        a0 = C + Vector((sx * 0.20, 0, 0))
        a1 = a0 + dv * 0.20
        beam(b, a0, a1, 0.032, 0.022, DARK if d else DARK, W, up=(1, 0, 0), grain=0)
        cyl(b, a1 + Vector((-sx * 0.01, 0, 0)), a1 + Vector((sx * 0.09, 0, 0)), 0.017, 0.017, (8, 6, 4)[d], WOOD, W)


def build_trigger(b, d):
    # alavanca do gatilho sob a coronha (gira em torno de (0, 0.18, 0.86))
    beam(b, (0, 0.16, 0.89), (0, 0.235, 0.745), 0.028, 0.034, IRON, 'Trigger', up=(1, 0, 0), grain=0)
    if d < 2:
        obox(b, (0, 0.14, 0.905), (0.02, 0.035, 0.02), IRON, 'Trigger')


def build_bolt(b, d, tip=(0, BOLT_TIP_Y, Z_BOLT), w='Bolt', up_len=None):
    """Virote de 0,60 m apontando para -Y: ponta de ferro, haste de madeira, 3 penas baixas."""
    ty = Vector(tip)
    hr = 0.0155
    body_end = BOLT_LEN - 0.085       # tail..shaft/head junction measured from the tip
    # haste (da cauda ate a base da ponta)
    cyl(b, ty + Vector((0, BOLT_LEN, 0)), ty + Vector((0, 0.085, 0)), hr, hr, (6, 5, 4)[d], WOOD, w, up=(0, 0, 1), caps=(True, False))
    # ponta tipo "quarrel": piramide de 4 lados
    tube(b, [ty + Vector((0, 0.085, 0)), ty + Vector((0, 0.060, 0)), ty], [0.0115, 0.0260, 0.0014], 4, IRON, w, up=(0, 0, 1), caps=(False, True))
    if d < 2:
        # nó (encaixe da corda) e penas
        cyl(b, ty + Vector((0, BOLT_LEN + 0.006, 0)), ty + Vector((0, BOLT_LEN, 0)), 0.0155, 0.0135, 6, WOOD, w, up=(0, 0, 1))
        for k in range(3):
            a = 2 * math.pi * k / 3 + math.pi / 2
            dv = Vector((math.cos(a), 0, math.sin(a)))
            y_a, y_b = BOLT_LEN - 0.03, BOLT_LEN - 0.12
            pts = [ty + Vector((0, y_a, 0)) + dv * hr, ty + Vector((0, y_b, 0)) + dv * hr, ty + Vector((0, y_b + 0.012, 0)) + dv * 0.036,
                   ty + Vector((0, y_a, 0)) + dv * 0.036]
            quad_double(b, pts, FEATHER, w)


# ------------------------------------------------------------------------------------------------ montagem de objetos
def finalize(b, name, rig, mats, collection):
    """Converte o MeshBuilder em objeto skinned: compacta materiais, normaliza pesos, cria grupos e modificador de armature."""
    bm = b.bm
    for v in bm.verts:
        dv = v[b.dl]
        total = sum(dv.values()) if len(dv) else 0.0
        if total <= 1e-6:
            dv.clear()
            dv[BONE_INDEX['Root']] = 1.0
        else:
            for k in list(dv.keys()):
                dv[k] = dv[k] / total
    used = sorted({f.material_index for f in bm.faces})
    remap = {old: new for new, old in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    tri = sum(len(p.vertices) - 2 for p in mesh.polygons)
    bm.free()
    for i in used:
        mesh.materials.append(mats[i])
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for bone in BONES:
        obj.vertex_groups.new(name=bone)
    if rig is not None:
        obj.parent = rig
        mod = obj.modifiers.new('Armature', 'ARMATURE')
        mod.object = rig
    return obj, tri


def build_rig():
    arm = bpy.data.armatures.new('CrossbowRig')
    rig = bpy.data.objects.new('CrossbowRig', arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.edit_bones

    def bone(name, head, tail, parent=None):
        e = eb.new(name)
        e.head, e.tail = Vector(head), Vector(tail)
        e.use_connect = False
        if parent:
            e.parent = eb[parent]
        e.align_roll(Vector((0, 0, 1)))
        return e
    bone('Root', (0, 0, 0), (0, 0.25, 0))
    bone('Wheel_L', (WX - 0.07, WY, WR), (WX + 0.07, WY, WR), 'Root')
    bone('Wheel_R', (-WX - 0.07, WY, WR), (-WX + 0.07, WY, WR), 'Root')
    for s, n in ((1, 'L'), (-1, 'R')):
        h = limb_point(s, 0.0)
        h.x = s * 0.14
        bone(f'Limb{n}', h, limb_point(s, 0.5), 'Root')
        bone(f'Limb{n}_Tip', limb_point(s, 0.5), limb_point(s, 1.0), f'Limb{n}')
    bone('String', (0, Y_C, Z_STR), (0, Y_C + 0.12, Z_STR), 'Root')
    bone('Bolt', (0, Y_BOLT_TAIL, Z_BOLT), (0, Y_BOLT_TAIL - 0.25, Z_BOLT), 'Root')
    bone('Winch', (-0.18, WINCH_Y, WINCH_Z), (0.18, WINCH_Y, WINCH_Z), 'Root')
    bone('Trigger', (0, 0.18, 0.86), (0, 0.235, 0.745), 'Root')
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return rig


# ------------------------------------------------------------------------------------------------ animacao
A1, A2 = math.radians(6.5), math.radians(8.0)      # rotacao total dos bracos entre armado e relaxado
TIP_ATTACH = {s: limb_point(s, 1.0) + Vector((0, 0.020, 0)) for s in (1, -1)}
V_DEPTH = Y_C - (TIP_ATTACH[1].y)                  # quanto o centro da corda fica atras das pontas, armado


def put(rig, name, loc=None, rot=None, scl=None, pivot=None):
    """Define a pose de um osso em espaco do mundo (pai em repouso): loc = deslocamento; rot = (eixo, angulo) em torno de pivot (padrao: cabeca)."""
    pb, bone = rig.pose.bones[name], rig.data.bones[name]
    R = bone.matrix_local.to_3x3()
    head = bone.head_local.copy()
    off = Vector(loc) if loc is not None else Vector((0, 0, 0))
    if rot is not None:
        axis, ang = rot
        q = Quaternion(Vector(axis), ang)
        pv = head if pivot is None else Vector(pivot)
        off = off + (pv + q @ (head - pv)) - head
        pb.rotation_quaternion = (R.inverted() @ q.to_matrix() @ R).to_quaternion()
    else:
        pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = R.inverted() @ off
    pb.scale = scl if scl is not None else (1, 1, 1)


def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)


def tip_world_y(rig):
    bpy.context.view_layer.update()
    ys = []
    for s, n in ((1, 'L'), (-1, 'R')):
        pb, bone = rig.pose.bones[f'Limb{n}_Tip'], rig.data.bones[f'Limb{n}_Tip']
        ys.append((pb.matrix @ (bone.matrix_local.inverted() @ TIP_ATTACH[s])).y)
    return sum(ys) / 2


def set_draw(rig, p, sway=0.0):
    """p = 1 corda armada (pose base), 0 = relaxada (bracos soltos), <0 = oscilacao alem do repouso. sway = giro comum dos bracos (rad)."""
    for s, n in ((1, 'L'), (-1, 'R')):
        put(rig, f'Limb{n}', rot=((0, 0, 1), -s * ((1 - p) * A1 + sway)))
        put(rig, f'Limb{n}_Tip', rot=((0, 0, 1), -s * ((1 - p) * A2 + sway)))
    ty = tip_world_y(rig)
    put(rig, 'String', loc=(0, ty + p * V_DEPTH - Y_C, 0))


def ease(x):
    return smooth(x)


def key_all(rig, frame):
    for pb in rig.pose.bones:
        pb.keyframe_insert('location', frame=frame)
        pb.keyframe_insert('rotation_quaternion', frame=frame)
        pb.keyframe_insert('scale', frame=frame)


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)


class Clip:
    def __init__(self, name, frames, loop, fn):
        self.name, self.frames, self.loop, self.fn = name, frames, loop, fn


def pose_idle(rig, f, n=60):
    ph = 2 * math.pi * f / n
    set_draw(rig, 1.0, sway=math.radians(0.18) * math.sin(ph))
    put(rig, 'Bolt', rot=(Y, math.radians(0.25) * math.sin(ph + 1)))
    put(rig, 'Root', loc=(0, 0, 0.0015 * (0.5 - 0.5 * math.cos(ph))))


def pose_move(rig, f, n=120):
    a = 2 * math.pi * f / n
    for name in ('Wheel_L', 'Wheel_R'):
        put(rig, name, rot=(X, a))
    put(rig, 'Root', loc=(0, 0, 0.006 * math.sin(8 * a)), rot=(X, math.radians(0.35) * math.sin(4 * a + 0.6)))
    set_draw(rig, 1.0, sway=math.radians(0.35) * math.sin(2 * a))
    put(rig, 'Bolt', rot=(Y, math.radians(0.6) * math.sin(4 * a)))
    put(rig, 'Trigger', rot=(X, math.radians(1.0) * math.sin(4 * a + 1)))


RELEASE_FRAME = 6


def pose_fire(rig, f):
    squeeze = ease(f / 5.0) if f < RELEASE_FRAME else max(0.0, 1 - (f - RELEASE_FRAME) / 6.0)
    put(rig, 'Trigger', rot=(X, math.radians(16) * squeeze))
    pk = {5: 1.0, 6: 0.40, 7: -0.18, 8: 0.14, 9: -0.07, 10: 0.035, 11: -0.015}
    p = 1.0 if f < RELEASE_FRAME - 1 else pk.get(f, 0.0)
    if f < RELEASE_FRAME - 1:
        p = 1.0
    set_draw(rig, p)
    bolt_vis = 1.0 if f < RELEASE_FRAME else 0.0
    if bolt_vis:
        put(rig, 'Bolt')
    else:
        put(rig, 'Bolt', loc=(0, 0, 0.20), scl=(0.001, 0.001, 0.001))   # oculto, na posicao em que o Reload o faz surgir
    # tambor solta a corda: gira ao contrario ate parar
    wf = ease((f - RELEASE_FRAME) / 10.0) if f > RELEASE_FRAME else 0.0
    put(rig, 'Winch', rot=(X, math.radians(720) * wf))
    kick = math.exp(-max(0, f - RELEASE_FRAME) / 3.5) * (f >= RELEASE_FRAME)
    put(rig, 'Root', loc=(0, 0.012 * kick, 0.0), rot=(X, math.radians(-0.7) * kick))


def pose_reload(rig, f, n=42):
    draw_end, bolt_f0 = 28, 25
    u = min(1.0, f / draw_end)
    p = 0.5 - 0.5 * math.cos(math.pi * u) if u < 1 else 1.0          # puxa a corda
    set_draw(rig, p)
    put(rig, 'Winch', rot=(X, -math.radians(720) * u))
    # gatilho trava
    tf = 0.0
    if f >= draw_end - 3:
        tf = math.sin(min(1.0, (f - (draw_end - 3)) / 8.0) * math.pi)
    put(rig, 'Trigger', rot=(X, math.radians(10) * tf))
    # virote entra no trilho: surge ligeiramente acima e assenta
    bu = min(1.0, max(0.0, (f - bolt_f0) / 9.0))
    vis = 0.001 if f < bolt_f0 else min(1.0, 0.35 + 0.65 * min(1.0, (f - bolt_f0) / 3.0))
    drop = 0.20 * (1 - ease(bu)) ** 2
    bounce = 0.0
    if f > bolt_f0 + 9:
        bounce = 0.006 * math.exp(-(f - bolt_f0 - 9) / 2.5) * math.cos((f - bolt_f0 - 9) * 1.9)
    put(rig, 'Bolt', loc=(0, 0, drop + bounce), scl=(vis, vis, vis))
    if f > draw_end:
        settle = math.exp(-(f - draw_end) / 3.0) * math.cos((f - draw_end) * 1.8)
        put(rig, 'Root', loc=(0, 0, 0.004 * settle))
        set_draw(rig, 1.0, sway=math.radians(0.5) * settle)


def pose_hit(rig, f, n=18):
    k = math.exp(-f / 5.5)
    put(rig, 'Root', loc=(0.018 * k * math.sin(f * 1.9), 0, 0.01 * k * math.sin(f * 2.3 + 1)),
        rot=((0.6, 0.0, 0.8), math.radians(2.2) * k * math.sin(f * 1.6)))
    set_draw(rig, 1.0, sway=math.radians(0.9) * k * math.sin(f * 2.4))


def build_destroy_state(f, rig):
    """Carreta desaba: tremida, roda direita (-X) se solta, braco esquerdo rompe, a frente tomba."""
    # tempos (30 fps): 0-6 tremor; 6-24 colapso; 24-48 assenta/quica; 48-60 repouso
    u = ease((f - 4) / 22.0)                                  # colapso principal
    shake = math.exp(-f / 7.0) * math.sin(f * 2.2) * (1 if f < 14 else 0)
    # raiz: inclina para a direita (-X) e a frente, pivotando perto da roda esquerda
    tilt_roll = math.radians(-13.0) * u
    tilt_pitch = math.radians(9.0) * u
    bounce = 0.0
    if f > 22:
        bounce = 0.02 * math.exp(-(f - 22) / 4.5) * abs(math.sin((f - 22) * 0.9))
    pv = Vector((WX, WY, 0.0))
    put(rig, 'Root', loc=(0.0, 0.0, bounce + 0.012 * shake), rot=(Y, tilt_roll), pivot=pv)
    # compoe pitch no Root via segunda rotacao: aplicada manualmente (quaternion combinado)
    pb, bone = rig.pose.bones['Root'], rig.data.bones['Root']
    q = Quaternion(Vector(Y), tilt_roll) @ Quaternion(Vector(X), 0.0)
    q2 = Quaternion(Vector(X), tilt_pitch) @ q
    R = bone.matrix_local.to_3x3()
    pb.rotation_quaternion = (R.inverted() @ q2.to_matrix() @ R).to_quaternion()
    # translacao para que o pivot (roda esquerda no chao) fique fixo
    pv2 = Vector((WX, WY, 0.0))
    newp = q2 @ pv2
    off = pv2 - newp + Vector((0, 0, bounce))
    pb.location = R.inverted() @ off
    # roda direita solta
    w_t = ease((f - 5) / 20.0)
    put(rig, 'Wheel_R', loc=(-0.55 * w_t, 0.25 * w_t, -0.40 * w_t * 0.0), rot=(Y, math.radians(-78) * w_t))
    # roda esquerda gira ate parar
    put(rig, 'Wheel_L', rot=(X, math.radians(160) * ease(f / 30.0)))
    # braco esquerdo (+X) rompe, direito pende
    lb = ease((f - 5) / 16.0)
    put(rig, 'LimbL', rot=(Z, math.radians(32) * lb))
    put(rig, 'LimbL_Tip', rot=(Z, math.radians(46) * lb))
    put(rig, 'LimbR', rot=(Z, math.radians(-6) * lb))
    put(rig, 'LimbR_Tip', rot=(Z, math.radians(-14) * lb))
    # corda arrebenta e cai para frente
    put(rig, 'String', loc=(0, -0.34 * ease((f - 5) / 10.0), -0.07 * lb))
    # virote cai do trilho
    bf = ease((f - 6) / 12.0)
    put(rig, 'Bolt', loc=(0.25 * bf, -0.1 * bf, -0.50 * bf), rot=((0.2, 0.9, 0.4), math.radians(70) * bf))
    put(rig, 'Winch', rot=(X, math.radians(-200) * ease((f - 6) / 14.0)))
    put(rig, 'Trigger', rot=(X, math.radians(22) * ease((f - 4) / 6.0)))


CLIPS = [
    Clip('Idle', 60, True, pose_idle),
    Clip('Move', 120, True, pose_move),
    Clip('Reload', 42, False, pose_reload),
    Clip('Fire', 24, False, pose_fire),
    Clip('Hit', 18, False, pose_hit),
    Clip('Destroy', 60, False, build_destroy_state),
]


def bake_clip(rig, clip):
    action = bpy.data.actions.new(clip.name)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    for f in range(0, clip.frames + 1):
        reset_pose(rig)
        if clip.fn is build_destroy_state:
            clip.fn(f, rig)
        else:
            clip.fn(rig, f)
        bpy.context.view_layer.update()
        key_all(rig, f)
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    reset_pose(rig)
    return action


# ------------------------------------------------------------------------------------------------ exportacao
def export_fbx(path, objects, action, frames):
    scene = bpy.context.scene
    scene.render.fps = FPS
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    rig = next(o for o in objects if o.type == 'ARMATURE')
    bpy.context.view_layer.objects.active = rig
    rig.animation_data_create()
    rig.animation_data.action = action
    if action is not None and action.slots:
        rig.animation_data.action_slot = action.slots[0]
    scene.frame_start, scene.frame_end = (0, frames) if action is not None else (0, 0)
    scene.frame_set(0)
    if action is None:
        reset_pose(rig)
    types = {'ARMATURE', 'MESH'} if any(o.type == 'MESH' for o in objects) else {'ARMATURE'}
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types=types, use_armature_deform_only=False, add_leaf_bones=False,
        axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='FACE', use_tspace=False,
        path_mode='STRIP', embed_textures=False, bake_anim=action is not None, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)


def main():
    for d in (OUT_MODELS, OUT_ACC, OUT_TEX, WORK, REPORT):
        d.mkdir(parents=True, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.unit_settings.system = 'METRIC'
    make_textures()
    mats = make_materials()
    rig = build_rig()
    coll = scene.collection
    report = {'parts': {}, 'lods': {}, 'bones': BONES}
    meshes = []

    # LOD0: pecas separadas e nomeadas; LOD1/LOD2: malha unica
    parts = {'Body': build_body, 'WheelL': lambda b, d: build_wheel(b, 1, d), 'WheelR': lambda b, d: build_wheel(b, -1, d),
             'LimbL': lambda b, d: build_limb(b, 1, d), 'LimbR': lambda b, d: build_limb(b, -1, d), 'String': build_string,
             'Bolt': build_bolt, 'Winch': build_winch, 'Trigger': build_trigger}
    lod_tris = [0, 0, 0]
    for lod in range(3):
        if lod == 0:
            for part, fn in parts.items():
                b = MeshBuilder(f'{part}_LOD0')
                fn(b, 0)
                obj, tri = finalize(b, f'{part}_LOD0', rig, mats, coll)
                meshes.append(obj)
                report['parts'][f'{part}_LOD0'] = tri
                lod_tris[0] += tri
        else:
            b = MeshBuilder(f'Crossbow_LOD{lod}')
            for part, fn in parts.items():
                fn(b, lod)
            obj, tri = finalize(b, f'Crossbow_LOD{lod}', rig, mats, coll)
            meshes.append(obj)
            lod_tris[lod] = tri
            report['parts'][f'Crossbow_LOD{lod}'] = tri
    report['lods'] = {f'LOD{i}': t for i, t in enumerate(lod_tris)}
    print('LOD_TRIANGLES', lod_tris)
    for t, lim in zip(lod_tris, LOD_LIMITS):
        if t > lim:
            print('AVISO: LOD acima do orcamento', t, lim)

    # dimensoes (todas as pecas do LOD0)
    dg = bpy.context.evaluated_depsgraph_get()
    lo, hi = Vector((9, 9, 9)), Vector((-9, -9, -9))
    for o in meshes:
        if not o.name.endswith('_LOD0'):
            continue
        for v in o.data.vertices:
            for i in range(3):
                lo[i], hi[i] = min(lo[i], v.co[i]), max(hi[i], v.co[i])
    report['bounds_blender'] = {'min': [round(c, 3) for c in lo], 'max': [round(c, 3) for c in hi],
                                'size_m': [round(hi[i] - lo[i], 3) for i in range(3)]}
    report['sockets_blender'] = {'BoltSpawn': [0.0, BOLT_TIP_Y, Z_BOLT], 'WeaponMount': [0.0, HEAD_Y, 1.10]}
    report['release_frame'] = RELEASE_FRAME
    report['fps'] = FPS

    # animacoes
    actions = {}
    for clip in CLIPS:
        actions[clip.name] = bake_clip(rig, clip)
        report.setdefault('clips', {})[clip.name] = {'frames': clip.frames, 'seconds': round(clip.frames / FPS, 4), 'loop': clip.loop}
    reset_pose(rig)
    rig.animation_data.action = None

    # exportacao: modelo base (bind = armada/carregada), clipes (so armature), virote separado
    export_fbx(OUT_MODELS / 'Crossbow.fbx', [rig] + meshes, None, 0)
    for clip in CLIPS:
        # a malha mais leve acompanha o clipe para que o caminho dos ossos (CrossbowRig/Root/...) seja igual ao do modelo base
        export_fbx(OUT_MODELS / f'Crossbow@{clip.name}.fbx', [rig, meshes[-1]], actions[clip.name], clip.frames)
    # virote separado (pivo na ponta; apontando para -Y no Blender = +Z no Unity)
    bb = MeshBuilder('Crossbow_Bolt')
    build_bolt(bb, 0, tip=(0, 0, 0), w='Root')
    bolt_obj, bolt_tris = finalize(bb, 'Crossbow_Bolt', None, mats, coll)
    report['parts']['Crossbow_Bolt'] = bolt_tris
    report['bolt_dimensions_m'] = [round(c, 4) for c in bolt_obj.dimensions]
    for vg in list(bolt_obj.vertex_groups):
        bolt_obj.vertex_groups.remove(vg)
    bpy.ops.object.select_all(action='DESELECT')
    bolt_obj.select_set(True)
    bpy.context.view_layer.objects.active = bolt_obj
    bpy.ops.export_scene.fbx(filepath=str(OUT_ACC / 'Crossbow_Bolt.fbx'), use_selection=True, object_types={'MESH'}, add_leaf_bones=False,
                             bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
    bolt_obj.hide_set(True)
    bolt_obj.location = (3.0, 0, 0)

    (WORK / 'crossbow-build.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    (REPORT / 'build-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    rig.animation_data.action = actions['Idle']
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'Crossbow.blend'), compress=True)
    print('CROSSBOW_BUILD_OK', json.dumps(report['lods']), json.dumps(report['bounds_blender']))


main()
