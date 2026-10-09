"""Projetil (pedra de ~0,4 m), fragmentos e texturas de particulas da catapulta, tudo procedural (obra do projeto).
Uso: blender -b --factory-startup --python-exit-code 1 --python Tools/CharacterRigging/BuildCatapultProps.py
Saidas: Assets/CharacterRigging/Catapult/Accessories/{CatapultBoulder,CatapultDebris}.fbx e Textures/{Fx_Smoke,Fx_Flash,Fx_Spark}.png"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import bmesh
import json
import math
import numpy as np
from mathutils import Vector, Matrix
import BuildCatapult as bc

ACC = bc.OUT / 'Accessories'


def save_rgba(path, rgba):
    h, w, _ = rgba.shape
    img = bpy.data.images.new(path.stem, w, h, alpha=True)
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def fx_textures():
    n = 128
    yy, xx = np.mgrid[0:n, 0:n]
    r = np.sqrt(((xx - n / 2 + 0.5) / (n / 2)) ** 2 + ((yy - n / 2 + 0.5) / (n / 2)) ** 2)
    # fumaca/poeira: nuvem macia (alpha cai suavemente a zero antes da borda, sem recorte quadrado)
    noise = bc.fbm(n, n, 4, 4, 4, 91)
    a = np.clip(1.0 - r, 0, 1)
    a = (a * a * (3 - 2 * a)) * (0.55 + 0.9 * (noise - 0.3))
    a = np.clip(a, 0, 1)
    smoke = np.zeros((n, n, 4))
    smoke[:, :, :3] = (0.92 - 0.25 * noise)[:, :, None]
    smoke[:, :, 3] = a
    save_rgba(bc.TEXTURES / 'Fx_Smoke.png', smoke)
    # flash: nucleo brilhante com halo
    flash = np.zeros((n, n, 4))
    core = np.exp(-(r * 2.6) ** 2)
    halo = np.clip(1 - r, 0, 1) ** 2
    flash[:, :, 0] = 1.0
    flash[:, :, 1] = 0.85 + 0.15 * core
    flash[:, :, 2] = 0.55 + 0.45 * core
    flash[:, :, 3] = np.clip(core * 0.95 + halo * 0.45, 0, 1)
    save_rgba(bc.TEXTURES / 'Fx_Flash.png', flash)
    # fagulha/fragmento: ponto pequeno, bordas macias
    spark = np.zeros((n, n, 4))
    s = np.exp(-(r * 3.2) ** 2)
    spark[:, :, :3] = np.array([1.0, 0.8, 0.5])[None, None, :]
    spark[:, :, 3] = np.clip(s, 0, 1)
    save_rgba(bc.TEXTURES / 'Fx_Spark.png', spark)


def make_obj(P, name, mats):
    mesh = bpy.data.meshes.new(name)
    P.bm.to_mesh(mesh)
    for m in mats:
        mesh.materials.append(m)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    P.bm.free()
    return obj


def export_prop(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'}, add_leaf_bones=False,
                             bake_anim=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, mesh_smooth_type='OFF', path_mode='STRIP', embed_textures=False)


def main():
    ACC.mkdir(parents=True, exist_ok=True)
    bc.TEXTURES.mkdir(parents=True, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    fx_textures()
    if not (bc.TEXTURES / bc.TEX_FILES[bc.STONE]).exists():
        bc.build_textures()
    mats = bc.make_materials()
    report = {}

    # pedra: ~0,4 m de diametro, 320 triangulos, centro na origem
    P = bc.Part('CatapultBoulder', 'None')
    bc.icosphere(P, (0, 0, 0), 0.20, 3, bc.STONE, jitter=0.07, squash=(1.0, 0.92, 1.0))
    boulder = make_obj(P, 'CatapultBoulder', mats)
    d = boulder.dimensions
    report['boulder'] = {'triangles': sum(len(p.vertices) - 2 for p in boulder.data.polygons), 'dimensions': [round(x, 3) for x in d]}
    export_prop(boulder, ACC / 'CatapultBoulder.fbx')

    # fragmento irregular (madeira/pedra) para as particulas de impacto: 24 triangulos
    Q = bc.Part('CatapultDebris', 'None')
    bc.icosphere(Q, (0, 0, 0), 0.5, 1, bc.STONE, jitter=0.25, squash=(1.0, 0.55, 0.8))
    debris = make_obj(Q, 'CatapultDebris', mats)
    report['debris'] = {'triangles': sum(len(p.vertices) - 2 for p in debris.data.polygons), 'dimensions': [round(x, 3) for x in debris.dimensions]}
    export_prop(debris, ACC / 'CatapultDebris.fbx')
    (bc.REPORT / 'props-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('CATAPULT_PROPS_OK', json.dumps(report))


if __name__ == '__main__':
    main()
