"""Audita a densidade de texels de cada malha de um ou mais modelos, em batch.

Usa o add-on Texel Density Checker (extensao `texel_density_checker`, Blender 5.2.1).
Somente leitura: nao altera UVs nem os arquivos de origem.

Uso:
  blender.exe -b -P Tools\\CharacterRigging\\AuditTexelDensity.py -- a.fbx b.fbx ... [--size 1024] [--target 5.0] [--csv saida.csv]

--size    resolucao quadrada de textura usada quando o modelo nao traz textura (padrao 1024)
--target  densidade alvo; malhas fora de +-20% sao marcadas
--csv     grava a tabela em CSV

Colunas: densidade medida na resolucao usada, altura da malha (m), triangulos e
densidade normalizada por altura (densidade / altura), que permite comparar
unidades de tamanhos diferentes.

Ajustar a densidade (texel_density_set) exige a interface do Blender (editor UV).
"""
import argparse
import csv
import os
import sys

import bpy

ADDON = "bl_ext.user_default.texel_density_checker"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("models", nargs="+")
    p.add_argument("--size", type=int, default=1024)
    p.add_argument("--target", type=float, default=None)
    p.add_argument("--csv", default=None)
    return p.parse_args(argv)


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.armatures):
        for item in list(coll):
            coll.remove(item)


def import_model(path):
    ext = path.lower().rsplit(".", 1)[-1]
    if ext == "fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext in ("glb", "gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == "obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise SystemExit(f"Formato nao suportado: {ext}")


def texture_size(obj):
    """Maior textura (px) ligada aos materiais da malha, ou None."""
    best = None
    for slot in obj.material_slots:
        mat = slot.material
        if not mat or not mat.use_nodes:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image and node.image.size[0] > 0:
                size = max(node.image.size)
                best = size if best is None else max(best, size)
    return best


def measure(obj, td, default_size):
    size = texture_size(obj) or default_size
    td.texture_size = "CUSTOM"
    td.custom_width = td.custom_height = str(size)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.texel_density_check()
    return size, float(td.density)


def main():
    args = parse_args()
    bpy.ops.preferences.addon_enable(module=ADDON)
    td = bpy.context.scene.td
    rows = []
    for path in args.models:
        clear_scene()
        import_model(path)
        name = os.path.splitext(os.path.basename(path))[0]
        for obj in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            height = obj.dimensions.z
            tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
            if not obj.data.uv_layers:
                rows.append([name, obj.name, "", "", round(height, 3), tris, "", "SEM UV"])
                continue
            size, value = measure(obj, td, args.size)
            norm = value / height if height > 1e-6 else 0.0
            flag = ""
            if args.target:
                flag = "OK" if abs(value - args.target) <= args.target * 0.2 else "FORA"
            rows.append([name, obj.name, size, round(value, 3), round(height, 3), tris,
                         round(norm, 3), flag])

    header = ["modelo", "malha", "tex_px", "densidade", "altura_m", "tris", "dens/altura", "alvo"]
    print("\n" + "".join(f"{h:<24}" if i < 2 else f"{h:>12}" for i, h in enumerate(header)))
    for r in rows:
        print("".join(f"{str(c):<24}" if i < 2 else f"{str(c):>12}" for i, c in enumerate(r)))
    if args.csv:
        with open(args.csv, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(header)
            w.writerows(rows)
        print(f"\nCSV: {args.csv}")


if __name__ == "__main__":
    main()
