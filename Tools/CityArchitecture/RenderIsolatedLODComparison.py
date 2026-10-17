"""Renderiza comparação visual do defeito isolado, sem salvar assets de origem."""
from __future__ import annotations

from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
def rooted_env(name: str, default: Path) -> Path:
    value = Path(__import__("os").environ.get(name, str(default)))
    return value if value.is_absolute() else ROOT / value


OUT = rooted_env("ERA_IMPERIAL_LOD_RENDER_OUT", ROOT / "TestResults/UVLOD-Isolated/House_Medieval_LOD-comparison.png")
LOD_CANDIDATE = rooted_env("ERA_IMPERIAL_LOD_RENDER_CANDIDATE", ROOT / "TestResults/UVLOD-Isolated/House_Medieval_LOD1_HouseBody-corrected.fbx")
FILES = [
    (ROOT / "Assets/CityArchitecture/Generated/Buildings/House_Medieval.fbx", -8.0, (0.22, 0.48, 0.86, 1.0)),
    (ROOT / "Assets/CityArchitecture/Generated/LOD/House_Medieval_LOD1.fbx", 0.0, (0.86, 0.58, 0.18, 1.0)),
    (LOD_CANDIDATE, 8.0, (0.24, 0.78, 0.42, 1.0)),
]


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.72
    return m


def import_group(path, target_x, color):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=False)
    objs = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in objs if obj.type == "MESH"]
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    center = (lo + hi) * 0.5
    offset = Vector((target_x - center.x, -lo.y, -center.z))
    roots = [obj for obj in objs if obj.parent not in objs]
    for obj in roots:
        obj.matrix_world.translation += offset
    mat = material(path.stem, color)
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    return meshes


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def main():
    clear()
    for path, x, color in FILES:
        import_group(path, x, color)
    bpy.ops.mesh.primitive_plane_add(size=32, location=(0, -0.02, 0))
    ground = bpy.context.object
    ground.data.materials.append(material("Ground", (0.055, 0.07, 0.09, 1.0)))
    light_data = bpy.data.lights.new("Key", type="AREA")
    light_data.energy = 1300
    light_data.shape = "RECTANGLE"
    light_data.size = 12
    light = bpy.data.objects.new("Key", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (0, 9, 10)
    look_at(light, (0, 2, 0))
    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 22
    camera.location = (0, 5.5, 18)
    look_at(camera, (0, 2.2, 0))
    bpy.context.scene.camera = camera
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 650
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(OUT)
    scene.world.color = (0.025, 0.035, 0.05)
    scene.render.film_transparent = False
    OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print(f"ISOLATED_LOD_RENDER output={OUT}")


if __name__ == "__main__":
    main()
