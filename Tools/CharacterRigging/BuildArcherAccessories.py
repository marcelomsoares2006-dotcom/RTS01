"""Build the Archer accessories as three independent, game-ready objects: Bow, Quiver, Arrow.
Run: blender --background --factory-startup --python BuildArcherAccessories.py
Metric scale (1 unit = 1 m), Blender convention: +Z up, character/projectile forward = -Y
(exported with axis_forward='-Z', axis_up='Y' like the other character FBX files).
Pivots are the attachment points: Bow = grip centre, Quiver = strap anchor on the back, Arrow = nock (tail).
"""
from pathlib import Path
import bpy, bmesh, math, json
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/CharacterRigging/Archer/Accessories'
WORK = ROOT / 'Tools/CharacterRigging/Working/Archer'
REPORT = ROOT / 'Docs/CharacterMeshAudit/Archer'


def material(name, color, rough=0.6, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = metal
    return m


BOW_TIPS = None
MAT = {
    'wood': material('Archer_Wood', (0.36, 0.20, 0.09), 0.55),
    'leather': material('Archer_Leather', (0.20, 0.11, 0.06), 0.8),
    'string': material('Archer_String', (0.80, 0.74, 0.60), 0.9),
    'steel': material('Archer_Steel', (0.55, 0.56, 0.58), 0.35, 1.0),
    'feather': material('Archer_Feather', (0.88, 0.85, 0.78), 0.9),
    'ash': material('Archer_AshShaft', (0.62, 0.45, 0.25), 0.6),
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


def tube(bm, pts, radii, sides=8, mat=0, scale_x=1.0):
    """Sweep a tapered ring along pts (Vector list); ring is flattened along local X by scale_x."""
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
    for ring, flip in ((rings[0], True), (rings[-1], False)):
        f = bm.faces.new(ring[::-1] if flip else ring)
        f.material_index = mat


def build_bow():
    """Medieval self-bow, 1.45 m. Limb tips are toward +Y (the archer); the string (drawn in Unity) spans the tips at +Y. Grip at origin."""
    bm = bmesh.new()
    half, n = 0.72, 24
    limb_pts, limb_r = [], []
    for sign in (1, -1):
        pts, rad = [], []
        for i in range(n + 1):
            t = i / n
            z = sign * (0.07 + t * (half - 0.07))
            y = 0.13 * math.sin(t * math.pi * 0.62) - 0.03 * t ** 3  # grip is the point farthest from the archer; tips toward the archer, slight recurve
            pts.append(Vector((0, y, z)))
            rad.append(0.017 - 0.010 * t)
        if sign == 1:
            up_pts, up_rad = pts, rad
        else:
            lo_pts, lo_rad = pts[::-1], rad[::-1]
    path = lo_pts[:-1] + [Vector((0, -0.0, 0))] + up_pts[1:]
    radii = lo_rad[:-1] + [0.021] + up_rad[1:]
    path[len(lo_pts) - 1] = Vector((0, 0, 0))
    tube(bm, path, radii, sides=8, mat=0, scale_x=1.35)
    # leather grip wrap
    grip = [Vector((0, 0, z)) for z in (-0.07, -0.03, 0.0, 0.03, 0.07)]
    tube(bm, grip, [0.0235] * 5, sides=10, mat=1)
    # The string is NOT modelled: Unity draws it with a LineRenderer between Bow_TipTop/Bow_TipBottom
    # and the drawing hand, so it can be pulled back during the draw animation.
    global BOW_TIPS
    BOW_TIPS = (path[-1].copy(), path[0].copy())
    return new_obj('Archer_Bow', bm, [MAT['wood'], MAT['leather']])


TORSO = None
QUIVER_ORIGIN = Vector((0, 0, 1.27))  # character-space (Blender axes) position of the quiver object's pivot
QUIVER_MOUTH = Vector((0, 0, 0.6))


def torso_profile(z, theta):
    """(cx, cy, radius) of the torso surface at height z in direction theta (rad from +X, CCW seen from above)."""
    levels = sorted((float(k), v) for k, v in TORSO["levels"].items())
    z = min(max(z, levels[0][0]), levels[-1][0])
    for (z0, l0), (z1, l1) in zip(levels, levels[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0) if z1 > z0 else 0.0
            break
    def rad(l):
        n = len(l["radii"])
        f = (theta % (2 * math.pi)) / (2 * math.pi) * n - 0.5
        i0 = int(math.floor(f)) % n
        i1 = (i0 + 1) % n
        w = f - math.floor(f)
        return l["radii"][i0] * (1 - w) + l["radii"][i1] * w
    return (l0["cx"] * (1 - t) + l1["cx"] * t, l0["cy"] * (1 - t) + l1["cy"] * t, rad(l0) * (1 - t) + rad(l1) * t)


def ribbon(bm, pts, normals, width, thick, mat):
    rings = []
    for i, p in enumerate(pts):
        t = (pts[(i + 1) % len(pts)] - pts[i - 1]).normalized()
        n = normals[i]
        b = n.cross(t).normalized()
        c = [p + b * width / 2 + n * thick, p - b * width / 2 + n * thick, p - b * width / 2, p + b * width / 2]
        rings.append([bm.verts.new(v) for v in c])
    for i in range(len(rings)):
        j = (i + 1) % len(rings)
        for k in range(4):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % 4], rings[j][(k + 1) % 4], rings[j][k]))
            f.material_index = mat


def build_quiver():
    """Leather quiver (~0.6 m) on the archer's back with a diagonal baldric, in character space around QUIVER_ORIGIN.
    The strap hugs the measured torso (torso.json); six arrow tails stick out of the mouth."""
    global QUIVER_MOUTH
    bm = bmesh.new()
    def back_y(z):
        cx, cy, r = torso_profile(z, math.pi / 2)
        return cy + r
    o = QUIVER_ORIGIN
    base = Vector((0.10, back_y(1.05) + 0.030, 1.05)) - o
    top = Vector((-0.10, back_y(1.58) + 0.045, 1.62)) - o
    n = 9
    path = [base.lerp(top, i / (n - 1)) for i in range(n)]
    radii = [0.046 + (0.088 - 0.046) * (i / (n - 1)) ** 0.8 for i in range(n)]
    tube(bm, path, radii, sides=16, mat=0)
    axis = (top - base).normalized()
    rim = [top - axis * 0.01, top + axis * 0.012]
    tube(bm, rim, [0.097, 0.097], sides=16, mat=0)
    QUIVER_MOUTH = top.copy()
    # arrow tails (shaft + feather block) rising from inside the quiver
    side = axis.cross(Vector((1, 0, 0))).normalized()
    up2 = axis.cross(side).normalized()
    for k, (a, rr) in enumerate([(0.2, 0.040), (1.2, 0.050), (2.3, 0.030), (3.4, 0.052), (4.4, 0.038), (5.5, 0.046)]):
        off = side * math.cos(a) * rr + up2 * math.sin(a) * rr
        start = base.lerp(top, 0.45) + off * 0.8
        end = top + axis * (0.10 + 0.012 * k) + off
        tube(bm, [start, end - axis * 0.07], [0.0035, 0.0035], sides=6, mat=1)
        tube(bm, [end - axis * 0.07, end - axis * 0.012, end], [0.0035, 0.0105, 0.0065], sides=6, mat=2)
    # baldric: a diagonal ribbon around the torso (right shoulder -> left hip), passing behind the quiver
    pts, nrm = [], []
    for i in range(48):
        psi = 2 * math.pi * i / 48
        theta = math.atan2(-math.sin(psi), -math.cos(psi))
        z = 1.27 + 0.19 * math.cos(psi)
        cx, cy, r = torso_profile(z, theta)
        r += 0.014
        d = Vector((math.cos(theta), math.sin(theta), 0))
        pts.append(Vector((cx, cy, z)) + d * r - o)
        nrm.append(d)
    ribbon(bm, pts, nrm, 0.05, 0.008, 0)
    q = new_obj('Archer_Quiver', bm, [MAT['leather'], MAT['ash'], MAT['feather']])
    return q


def build_arrow():
    """Arrow 0.72 m pointing -Y; nock (tail) at origin; steel bodkin head; three feather vanes."""
    bm = bmesh.new()
    L = 0.72
    shaft = [Vector((0, -y, 0)) for y in (0.0, 0.2, 0.4, 0.6, L - 0.045)]
    tube(bm, shaft, [0.0042] * 5, sides=8, mat=0)
    # nock
    tube(bm, [Vector((0, 0.008, 0)), Vector((0, 0.0, 0))], [0.0048, 0.0042], sides=8, mat=0)
    # head: tapered cone
    head = [Vector((0, -(L - 0.045), 0)), Vector((0, -(L - 0.020), 0)), Vector((0, -L, 0))]
    tube(bm, head, [0.0075, 0.0050, 0.0004], sides=8, mat=1)
    # fletching: 3 vanes at 120 deg
    for k in range(3):
        a = 2 * math.pi * k / 3
        d = Vector((math.cos(a), 0, math.sin(a)))
        y0, y1 = 0.025, 0.115
        quad = [bm.verts.new(d * 0.0042 + Vector((0, -y0, 0))),
                bm.verts.new(d * 0.0042 + Vector((0, -y1, 0))),
                bm.verts.new(d * 0.020 + Vector((0, -y1 + 0.012, 0))),
                bm.verts.new(d * 0.020 + Vector((0, -y0, 0)))]
        f = bm.faces.new(quad)
        f.material_index = 2
        back = [bm.verts.new(v.co) for v in quad[::-1]]  # separate verts: double-sided without a duplicate face
        f2 = bm.faces.new(back)
        f2.material_index = 2
    return new_obj('Archer_Arrow', bm, [MAT['ash'], MAT['steel'], MAT['feather']])


def add_empty(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'PLAIN_AXES'
    e.empty_display_size = 0.03
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = loc
    return e


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def export(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    for c in obj.children:
        c.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH', 'EMPTY'},
                             add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, path_mode='COPY', embed_textures=False)


def main():
    for d in (OUT, WORK, REPORT):
        d.mkdir(parents=True, exist_ok=True)
    for default in list(bpy.data.objects):
        bpy.data.objects.remove(default, do_unlink=True)
    global TORSO
    TORSO = json.loads((WORK / 'torso.json').read_text())
    objs = [build_bow(), build_quiver(), build_arrow()]
    add_empty('Bow_TipTop', objs[0], BOW_TIPS[0])
    add_empty('Bow_TipBottom', objs[0], BOW_TIPS[1])
    add_empty('Bow_NockPoint', objs[0], (0, 0.0, 0.0))
    add_empty('Quiver_Mouth', objs[1], QUIVER_MOUTH)
    # lay them out for the blend preview, then export each at its own origin
    report = {'objects': [], 'quiverOriginCharacterSpaceBlender': [round(c, 4) for c in QUIVER_ORIGIN]}
    for o in objs:
        o.location = (0, 0, 0)
        bpy.context.view_layer.update()
        dim = o.dimensions
        export(o, OUT / (o.name + '.fbx'))
        report['objects'].append({'name': o.name, 'triangles': tris(o), 'dimensions_m': [round(x, 3) for x in dim],
                                  'materials': [m.name for m in o.data.materials]})
    objs[0].location = (-0.5, 0, 0.8)
    objs[1].location = (0.5, 0, 0.0)
    objs[2].location = (0.0, 0, 0.0)
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'Archer_Accessories.blend'), compress=True)
    (REPORT / 'accessories.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('ACCESSORIES_OK', json.dumps(report))


main()
