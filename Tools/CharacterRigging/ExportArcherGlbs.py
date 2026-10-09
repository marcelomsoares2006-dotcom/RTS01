"""Export bow, quiver and arrow (with their reference empties) as separate GLB files.
Run: blender -b Tools/CharacterRigging/Working/Archer/Archer_Accessories.blend --python Tools/CharacterRigging/ExportArcherGlbs.py -- <output dir>"""
import sys
from pathlib import Path
import bpy
out = Path(sys.argv[sys.argv.index("--") + 1])
out.mkdir(parents=True, exist_ok=True)
for name in ("Archer_Bow", "Archer_Quiver", "Archer_Arrow"):
    obj = bpy.data.objects[name]
    obj.location = (0, 0, 0)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    for child in obj.children:
        child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=str(out / f"{name}.glb"), export_format="GLB", use_selection=True, export_yup=True)
    print("GLB_OK", name)
