"""Cajado do Mago de Combate como objeto independente (procedural, sem terceiros).
Run: blender --background --factory-startup --python BuildCombatMageAccessories.py
Escala metrica (1 unidade = 1 m), +Z para cima (exportado com axis_forward=-Z / axis_up=Y como os demais FBX).
Origem = ponto de empunhadura (centro do punho); a ponta de baixo (ferrule) fica em z=-0.82 e o orbe em z=+1.07.
Filhos: Orb (malha emissiva separada), CastPoint (vazio no centro do orbe: origem do projetil).
"""
from pathlib import Path
import bpy, bmesh, math, json
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/CharacterRigging/CombatMage/Accessories'
WORK = ROOT / 'Tools/CharacterRigging/Working/CombatMage'
REPORT = ROOT / 'Docs/CharacterMeshAudit/CombatMage'

FOOT, GRIP, COLLAR, ORB_Z, ORB_R = -0.82, 0.0, 0.95, 1.07, 0.075


def material(name, color, rough=0.6, metal=0.0, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = metal
    if emit:
        bs.inputs['Emission Color'].default_value = (*emit, 1)
        bs.inputs['Emission Strength'].default_value = 3.0
    return m


MAT = {
    'wood': material('CombatMage_StaffWood', (0.20, 0.11, 0.06), 0.55),
    'leather': material('CombatMage_StaffLeather', (0.30, 0.17, 0.09), 0.8),
    'bronze': material('CombatMage_StaffBronze', (0.62, 0.42, 0.18), 0.35, 1.0),
    'orb': material('CombatMage_Orb', (0.45, 0.75, 1.0), 0.15, 0.0, emit=(0.35, 0.65, 1.0)),
}


def new_obj(name, bm, mats):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    for m in mats:
        mesh.materials.append(m)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def tube(bm, pts, radii, sides=10, mat=0, caps=True):
    rings = []
    for i, p in enumerate(pts):
        a = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        t = a.normalized()
        ref = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0))
        u = t.cross(ref).normalized()
        v = t.cross(u).normalized()
        rings.append([bm.verts.new(p + u * math.cos(2 * math.pi * s / sides) * radii[i] + v * math.sin(2 * math.pi * s / sides) * radii[i]) for s in range(sides)])
    for i in range(len(rings) - 1):
        for s in range(sides):
            f = bm.faces.new((rings[i][s], rings[i][(s + 1) % sides], rings[i + 1][(s + 1) % sides], rings[i + 1][s]))
            f.material_index = mat
    if caps:
        for ring, flip in ((rings[0], True), (rings[-1], False)):
            f = bm.faces.new(ring[::-1] if flip else ring)
            f.material_index = mat


def build_staff():
    bm = bmesh.new()
    # shaft: slightly gnarled, thicker at the grip and at the head
    n = 28
    pts, rad = [], []
    for i in range(n + 1):
        t = i / n
        z = FOOT + 0.02 + t * (COLLAR - FOOT - 0.02)
        pts.append(Vector((0.006 * math.sin(t * 9.0), 0.005 * math.cos(t * 7.0), z)))
        rad.append(0.016 + 0.004 * math.sin(t * 6.0) + 0.006 * t ** 4)
    tube(bm, pts, rad, sides=10, mat=0)
    # leather wrap around the grip (z -0.10..0.12)
    wrap = [Vector((0, 0, z)) for z in (-0.10, -0.06, 0.0, 0.06, 0.12)]
    tube(bm, wrap, [0.0215] * 5, sides=12, mat=1)
    # bronze ferrule (foot)
    tube(bm, [Vector((0, 0, FOOT)), Vector((0, 0, FOOT + 0.02)), Vector((0, 0, FOOT + 0.10))], [0.011, 0.019, 0.018], sides=10, mat=2)
    # bronze collar below the head + decorative ring
    tube(bm, [Vector((0, 0, COLLAR - 0.03)), Vector((0, 0, COLLAR + 0.025))], [0.030, 0.030], sides=14, mat=2)
    tube(bm, [Vector((0, 0, COLLAR - 0.20)), Vector((0, 0, COLLAR - 0.185))], [0.024, 0.024], sides=12, mat=2)
    # four bronze prongs cradling the orb, twisting slightly and meeting above it
    for k in range(4):
        a0 = math.pi / 2 * k + math.pi / 4
        pts_p, rad_p = [], []
        m = 12
        for i in range(m + 1):
            t = i / m
            z = COLLAR + 0.02 + t * (ORB_Z + ORB_R + 0.03 - COLLAR - 0.02)
            r = 0.026 + 0.062 * math.sin(min(t, 1.0) * math.pi * 0.62) ** 1.2 * (1.0 if t < 0.85 else 0.55 + 0.45 * (1 - t) / 0.15)
            a = a0 + 0.35 * t
            pts_p.append(Vector((math.cos(a) * r, math.sin(a) * r, z)))
            rad_p.append(0.0105 - 0.004 * t)
        tube(bm, pts_p, rad_p, sides=7, mat=2)
    return new_obj('CombatMage_Staff', bm, [MAT['wood'], MAT['leather'], MAT['bronze']])


def build_orb():
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=ORB_R)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, ORB_Z))
    return new_obj('Orb', bm, [MAT['orb']])


def add_empty(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'PLAIN_AXES'
    e.empty_display_size = 0.04
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = loc
    return e


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    for d in (OUT, WORK, REPORT):
        d.mkdir(parents=True, exist_ok=True)
    for default in list(bpy.data.objects):
        bpy.data.objects.remove(default, do_unlink=True)
    staff = build_staff()
    orb = build_orb()
    orb.parent = staff
    add_empty('CastPoint', staff, (0, 0, ORB_Z))
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    staff.select_set(True)
    for c in staff.children:
        c.select_set(True)
    bpy.context.view_layer.objects.active = staff
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'CombatMage_Staff.fbx'), use_selection=True, object_types={'MESH', 'EMPTY'},
                             add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, path_mode='COPY', embed_textures=False)
    report = {'objects': [{'name': o.name, 'triangles': tris(o), 'materials': [m.name for m in o.data.materials]} for o in (staff, orb)],
              'staffDimensionsM': [round(x, 3) for x in staff.dimensions], 'gripToFootM': -FOOT, 'orbCenterAboveGripM': ORB_Z, 'orbRadiusM': ORB_R}
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'CombatMage_Accessories.blend'), compress=True)
    (REPORT / 'accessories.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('ACCESSORIES_OK', json.dumps(report))


main()
