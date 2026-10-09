"""Constroi a espada longa e o escudo do Paladino curador como dois objetos independentes, prontos para jogo.
Executar: blender --background --factory-startup --python BuildPaladinHealerAccessories.py
Escala metrica (1 unidade = 1 m), convencao Blender: +Z para cima, frente do personagem = -Y
(exportado com axis_forward='-Z', axis_up='Y' como os demais FBX de personagens).

Espada (PaladinHealer_Sword): origem no centro da empunhadura; a ponta aponta para -Y; a largura da lamina e do guarda-mao
fica em X; a face plana da lamina e Z. Escudo (PaladinHealer_Shield): origem no ponto de fixacao no antebraco (parte traseira
do escudo); largura em X, altura em Z (topo para +Z, ponta inferior para -Z), face voltada para -Y.
Materiais por parte: aco, ouro, couro, madeira (verso do escudo) e linho (face pintada do escudo).
"""
from pathlib import Path
import bmesh
import bpy
import json
import math
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/CharacterRigging/PaladinHealer/Accessories"
WORK = ROOT / "Tools/CharacterRigging/Working/PaladinHealer"
REPORT = ROOT / "Docs/CharacterMeshAudit/PaladinHealer"


def material(name, color, rough=0.6, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bs = m.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = (*color, 1)
    bs.inputs["Roughness"].default_value = rough
    bs.inputs["Metallic"].default_value = metal
    return m


MAT = {
    "steel": material("PaladinHealer_Steel", (0.62, 0.64, 0.68), 0.32, 1.0),
    "gold": material("PaladinHealer_Gold", (0.86, 0.66, 0.20), 0.35, 1.0),
    "leather": material("PaladinHealer_Leather", (0.22, 0.12, 0.06), 0.8),
    "wood": material("PaladinHealer_Wood", (0.40, 0.25, 0.12), 0.6),
    "linen": material("PaladinHealer_Linen", (0.90, 0.88, 0.82), 0.7),
}


def new_obj(name, bm, mat_slots):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    for m in mat_slots:
        mesh.materials.append(m)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def tube(bm, pts, radii, sides=8, mat=0, scale_x=1.0, cap=True):
    """Varre um anel afunilado ao longo de pts; o anel e achatado em X local por scale_x."""
    rings = []
    for i, p in enumerate(pts):
        a = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        t = a.normalized()
        ref = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0))
        u = t.cross(ref).normalized()
        v = t.cross(u).normalized()
        ring = []
        for s in range(sides):
            ang = 2 * math.pi * s / sides
            ring.append(bm.verts.new(p + u * math.cos(ang) * radii[i] * scale_x + v * math.sin(ang) * radii[i]))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for s in range(sides):
            f = bm.faces.new((rings[i][s], rings[i][(s + 1) % sides], rings[i + 1][(s + 1) % sides], rings[i + 1][s]))
            f.material_index = mat
    if cap:
        for ring, flip in ((rings[0], True), (rings[-1], False)):
            f = bm.faces.new(ring[::-1] if flip else ring)
            f.material_index = mat
    return rings


def box(bm, center, size, mat):
    cx, cy, cz = center
    sx, sy, sz = (s / 2 for s in size)
    v = [bm.verts.new((cx + dx * sx, cy + dy * sy, cz + dz * sz)) for dx in (-1, 1) for dy in (-1, 1) for dz in (-1, 1)]
    idx = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    for quad in idx:
        f = bm.faces.new([v[i] for i in quad])
        f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])


# ----------------------------------------------------------------------------- espada
def build_sword():
    """Espada longa de 1,0 m: lamina 0,80 m com cana central, guarda-mao reto, empunhadura de couro e pomo de ouro.
    Slots: 0 aco, 1 ouro, 2 couro."""
    bm = bmesh.new()
    # lamina em losango (largura em X, espessura em Z), da base (y=-0.105) ate a ponta (y=-0.905)
    sections = [(-0.105, 0.052, 0.0105), (-0.20, 0.050, 0.0100), (-0.40, 0.045, 0.0090), (-0.60, 0.038, 0.0080),
                (-0.78, 0.028, 0.0065), (-0.865, 0.017, 0.0045)]
    rings = []
    for y, w, t in sections:
        rings.append([bm.verts.new((-w, y, 0.0)), bm.verts.new((0.0, y, t)), bm.verts.new((w, y, 0.0)), bm.verts.new((0.0, y, -t))])
    for i in range(len(rings) - 1):
        for k in range(4):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % 4], rings[i + 1][(k + 1) % 4], rings[i + 1][k]))
            f.material_index = 0
    tip = bm.verts.new((0.0, -0.905, 0.0))
    for k in range(4):
        f = bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 4], tip))
        f.material_index = 0
    f = bm.faces.new(rings[0][::-1])
    f.material_index = 0
    # cana: filete central de cada face
    for sign in (1, -1):
        pts = [Vector((0, -0.13, sign * 0.0105)), Vector((0, -0.55, sign * 0.0085))]
        tube(bm, pts, [0.0035, 0.0030], sides=6, mat=0)
    # guarda-mao reto com pontas levemente curvas (ouro)
    cross_pts = [Vector((x, -0.095 - 0.004 * (x / 0.11) ** 2, 0.0)) for x in (-0.11, -0.07, -0.035, 0.0, 0.035, 0.07, 0.11)]
    tube(bm, cross_pts, [0.0075, 0.0105, 0.0125, 0.0135, 0.0125, 0.0105, 0.0075], sides=8, mat=1)
    for x in (-0.115, 0.115):
        tube(bm, [Vector((x, -0.097, 0.0)), Vector((x + math.copysign(0.012, x), -0.099, 0.0))], [0.0085, 0.0055], sides=8, mat=1)
    # colar da lamina
    tube(bm, [Vector((0, -0.088, 0)), Vector((0, -0.107, 0))], [0.017, 0.017], sides=8, mat=1, scale_x=1.5)
    # empunhadura de couro (y +0.08 .. -0.085) e pomo de ouro
    grip = [Vector((0, y, 0)) for y in (-0.085, -0.04, 0.0, 0.04, 0.085)]
    tube(bm, grip, [0.0165, 0.0175, 0.0180, 0.0175, 0.0165], sides=10, mat=2)
    for y in (-0.05, -0.017, 0.017, 0.05):  # fios de couro (aneis)
        tube(bm, [Vector((0, y - 0.003, 0)), Vector((0, y + 0.003, 0))], [0.0195, 0.0195], sides=10, mat=2)
    tube(bm, [Vector((0, 0.085, 0)), Vector((0, 0.108, 0)), Vector((0, 0.125, 0)), Vector((0, 0.137, 0))],
         [0.020, 0.030, 0.027, 0.012], sides=12, mat=1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return new_obj("PaladinHealer_Sword", bm, [MAT["steel"], MAT["gold"], MAT["leather"]])


# ----------------------------------------------------------------------------- escudo
W2, TOP, BOTTOM = 0.29, 0.28, -0.60        # meia largura, topo e ponta inferior (em Z; origem = ponto de fixacao no antebraco)
CENTER = Vector((0.0, -0.10))              # centro de homotetia dos aneis (x, z)


def contour(n_side=10, n_curve=14):
    """Silhueta de escudo normando (heater) em (x, z), no sentido anti-horario visto pela frente (-Y)."""
    pts = [(-W2, TOP), (0.0, TOP + 0.012), (W2, TOP)]
    for i in range(1, n_side + 1):  # lado direito descendo
        pts.append((W2, TOP + (-0.08 - TOP) * i / n_side))
    start = Vector((W2, -0.08))
    end = Vector((0.0, BOTTOM))
    ctrl = Vector((W2 * 0.95, BOTTOM * 0.62))
    for i in range(1, n_curve):
        t = i / n_curve
        p = (1 - t) ** 2 * start + 2 * (1 - t) * t * ctrl + t ** 2 * end
        pts.append((p.x, p.y))
    pts.append((0.0, BOTTOM))
    for i in range(n_curve - 1, 0, -1):  # lado esquerdo subindo (espelho)
        t = i / n_curve
        p = (1 - t) ** 2 * start + 2 * (1 - t) * t * ctrl + t ** 2 * end
        pts.append((-p.x, p.y))
    for i in range(n_side, 0, -1):
        pts.append((-W2, TOP + (-0.08 - TOP) * i / n_side))
    return pts[:-0 or None]


def face_y(x, z):
    """Altura (Y, negativo = frente) da superficie da face em (x, z): cupula suave."""
    u = x / W2
    v = (z - CENTER.y) / 0.50
    k = max(0.0, 1.0 - 0.55 * u * u - 0.45 * v * v)
    return -(0.014 + 0.040 * k)


def back_y(x, z):
    u = x / W2
    v = (z - CENTER.y) / 0.50
    k = max(0.0, 1.0 - 0.55 * u * u - 0.45 * v * v)
    return 0.020 - 0.020 * k


def build_shield():
    """Escudo normando de 0,88 m. Slots: 0 aco (borda), 1 ouro (cruz), 2 couro (tiras), 3 madeira (verso), 4 linho (face)."""
    bm = bmesh.new()
    outline = contour()
    n = len(outline)
    scales = [1.0, 0.915, 0.72, 0.48, 0.24]

    def ring_vertices(s, yfun):
        out = []
        for x, z in outline:
            px = CENTER.x + (x - CENTER.x) * s
            pz = CENTER.y + (z - CENTER.y) * s
            out.append(bm.verts.new((px, yfun(px, pz), pz)))
        return out

    front = [ring_vertices(s, face_y) for s in scales]
    centre_front = bm.verts.new((CENTER.x, face_y(CENTER.x, CENTER.y), CENTER.y))
    for k in range(len(front) - 1):
        mat = 0 if k == 0 else 4
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((front[k][i], front[k + 1][i], front[k + 1][j], front[k][j]))
            f.material_index = mat
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((front[-1][i], centre_front, front[-1][j]))
        f.material_index = 4
    # verso (madeira): aneis concavos, normais para +Y
    back = [ring_vertices(s, back_y) for s in scales[::1]]
    centre_back = bm.verts.new((CENTER.x, back_y(CENTER.x, CENTER.y), CENTER.y))
    for k in range(len(back) - 1):
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((back[k][i], back[k][j], back[k + 1][j], back[k + 1][i]))
            f.material_index = 3
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((back[-1][i], back[-1][j], centre_back))
        f.material_index = 3
    # borda lateral de aco: liga a face externa ao verso, levemente projetada para fora
    rim_out = []
    for i, (x, z) in enumerate(outline):
        d = Vector((x - CENTER.x, z - CENTER.y))
        d = d.normalized() * 0.009 if d.length > 1e-6 else Vector((0, 0))
        rim_out.append((bm.verts.new((x + d.x, face_y(x, z) - 0.003, z + d.y)), bm.verts.new((x + d.x, back_y(x, z) + 0.002, z + d.y))))
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((rim_out[i][0], rim_out[j][0], rim_out[j][1], rim_out[i][1]))
        f.material_index = 0
        f = bm.faces.new((front[0][i], front[0][j], rim_out[j][0], rim_out[i][0]))
        f.material_index = 0
        f = bm.faces.new((back[0][j], back[0][i], rim_out[i][1], rim_out[j][1]))
        f.material_index = 0
    open_faces = []

    # cruz de ouro em relevo, acompanhando a cupula
    def surface_patch(x0, x1, z0, z1, nx, nz, lift):
        grid = []
        for a in range(nx + 1):
            col = []
            for b in range(nz + 1):
                x = x0 + (x1 - x0) * a / nx
                z = z0 + (z1 - z0) * b / nz
                col.append(bm.verts.new((x, face_y(x, z) - lift, z)))
            grid.append(col)
        for a in range(nx):
            for b in range(nz):
                f = bm.faces.new((grid[a][b], grid[a][b + 1], grid[a + 1][b + 1], grid[a + 1][b]))
                f.material_index = 1
                open_faces.append(f)
    # cruz potente: haste vertical, braco horizontal e pontas alargadas
    surface_patch(-0.040, 0.040, -0.44, 0.19, 3, 14, 0.006)
    surface_patch(-0.20, 0.20, -0.075, 0.015, 10, 2, 0.0065)
    for sx in (-1, 1):
        surface_patch(sx * 0.20 if sx > 0 else -0.235, sx * 0.235 if sx > 0 else -0.20, -0.095, 0.035, 1, 3, 0.006)
    surface_patch(-0.075, 0.075, 0.19, 0.225, 4, 1, 0.006)
    surface_patch(-0.075, 0.075, -0.475, -0.44, 4, 1, 0.006)
    # cubo central (umbo) de aco
    umbo = []
    for r, h in ((0.050, 0.0), (0.046, 0.012), (0.032, 0.022), (0.012, 0.027)):
        ring = []
        for s in range(14):
            ang = 2 * math.pi * s / 14
            x, z = r * math.cos(ang), -0.034 + r * math.sin(ang)
            ring.append(bm.verts.new((x, face_y(x, z) - 0.006 - h, z)))
        umbo.append(ring)
    for k in range(len(umbo) - 1):
        for s in range(14):
            f = bm.faces.new((umbo[k][s], umbo[k][(s + 1) % 14], umbo[k + 1][(s + 1) % 14], umbo[k + 1][s]))
            f.material_index = 0
            open_faces.append(f)
    f = bm.faces.new(umbo[-1][::-1])
    f.material_index = 0
    open_faces.append(f)
    # tiras de couro no verso: duas bandas em torno do antebraco (eixo vertical Z, centro em y=+0.085) e uma guiga
    def band(zc, height, rx=0.058, ry=0.050, cy=0.082, thick=0.010):
        segs = 16
        outer0, outer1, inner0, inner1 = [], [], [], []
        for s in range(segs):
            ang = 2 * math.pi * s / segs
            ox, oy = (rx + thick) * math.cos(ang), cy + (ry + thick) * math.sin(ang)
            ix, iy = rx * math.cos(ang), cy + ry * math.sin(ang)
            outer0.append(bm.verts.new((ox, oy, zc - height / 2)))
            outer1.append(bm.verts.new((ox, oy, zc + height / 2)))
            inner0.append(bm.verts.new((ix, iy, zc - height / 2)))
            inner1.append(bm.verts.new((ix, iy, zc + height / 2)))
        for s in range(segs):
            t = (s + 1) % segs
            for quad in ((outer0[s], outer0[t], outer1[t], outer1[s]), (inner0[t], inner0[s], inner1[s], inner1[t]),
                         (outer0[s], inner0[s], inner0[t], outer0[t]), (outer1[t], inner1[t], inner1[s], outer1[s])):
                f = bm.faces.new(quad)
                f.material_index = 2
    band(0.150, 0.045)
    band(-0.150, 0.050)
    # pegador (guiga/manete) de couro entre as tiras, no verso
    box(bm, (0.0, 0.060, 0.0), (0.10, 0.012, 0.050), 2)
    skip = set(open_faces)
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in skip])
    for f in open_faces:   # relevos abertos: normal voltada para a frente (-Y)
        f.normal_update()
        if f.normal.y > 0:
            f.normal_flip()
    return new_obj("PaladinHealer_Shield", bm, [MAT["steel"], MAT["gold"], MAT["leather"], MAT["wood"], MAT["linen"]])


def add_empty(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.03
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = loc
    return e


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def export(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    for c in obj.children:
        c.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"MESH", "EMPTY"},
                             add_leaf_bones=False, bake_anim=False, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, path_mode="COPY", embed_textures=False)


def main():
    for d in (OUT, WORK, REPORT):
        d.mkdir(parents=True, exist_ok=True)
    for default in list(bpy.data.objects):
        bpy.data.objects.remove(default, do_unlink=True)
    sword, shield = build_sword(), build_shield()
    SHIELD_SCALE = 0.85   # escudo proporcional ao personagem (1,85 m)
    shield.data.transform(Matrix.Scale(SHIELD_SCALE, 4))
    add_empty("Sword_Tip", sword, (0, -0.905, 0))
    add_empty("Sword_Pommel", sword, (0, 0.137, 0))
    add_empty("Shield_Top", shield, (0, 0, TOP * SHIELD_SCALE))
    add_empty("Shield_Bottom", shield, (0, 0, BOTTOM * SHIELD_SCALE))
    report = {"objects": []}
    for o in (sword, shield):
        o.location = (0, 0, 0)
        bpy.context.view_layer.update()
        dim = o.dimensions
        export(o, OUT / (o.name + ".fbx"))
        report["objects"].append({"name": o.name, "triangles": tris(o), "dimensions_m": [round(x, 3) for x in dim],
                                  "materials": [m.name for m in o.data.materials]})
        assert tris(o) <= 8000, o.name
    sword.location = (-0.5, 0, 0.8)
    shield.location = (0.5, 0, 0.0)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "PaladinHealer_Accessories.blend"), compress=True)
    (REPORT / "accessories.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("ACCESSORIES_OK", json.dumps(report))


main()
