"""Renderiza a comparação offline do House real, sem editar o FBX ou o prefab."""
from __future__ import annotations

import bpy
from mathutils import Vector
from pathlib import Path


ROOT = Path.cwd()
MODEL = ROOT / "Assets" / "Resources" / "Imports" / "Units" / "Buildings" / "house.fbx"
MAPS = ROOT / "Assets" / "Resources" / "Textures" / "Buildings" / "HousePilotPBR"
OUT = ROOT / "TestResults" / "HousePBRPilot" / "comparison-offline.png"


def clear() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def solid(name: str, color: tuple[float, float, float], roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return material


def image(path: Path, non_color: bool = False):
    texture = bpy.data.images.load(str(path), check_existing=True)
    node = bpy.data.node_groups.new(path.stem, "ShaderNodeTree").nodes.new("ShaderNodeTexImage")
    node.image = texture
    if non_color:
        node.image.colorspace_settings.name = "Non-Color"
    return node


def pbr(name: str, profile: str, tiling: float, roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    albedo = nodes.new("ShaderNodeTexImage")
    normal = nodes.new("ShaderNodeTexImage")
    normal.image = bpy.data.images.load(str(MAPS / f"HousePilot_{profile}_Normal.png"), check_existing=True)
    normal.image.colorspace_settings.name = "Non-Color"
    albedo.image = bpy.data.images.load(str(MAPS / f"HousePilot_{profile}_Albedo.png"), check_existing=True)
    mapping = nodes.new("ShaderNodeMapping")
    coord = nodes.new("ShaderNodeTexCoord")
    mapping.inputs["Scale"].default_value = (tiling, tiling, tiling)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.46
    links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    links.new(mapping.outputs["Vector"], albedo.inputs["Vector"])
    links.new(mapping.outputs["Vector"], normal.inputs["Vector"])
    links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = roughness
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def import_house(name: str):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(MODEL))
    house = next(o for o in bpy.context.scene.objects if o not in before and o.type == "MESH" and o.name.startswith("House"))
    house.name = name
    for obj in list(bpy.context.scene.objects):
        if obj not in before and obj != house and obj.type in {"CAMERA", "LIGHT", "MESH"}:
            bpy.data.objects.remove(obj, do_unlink=True)
    return house


def place_on_ground(obj, x: float) -> None:
    obj.location.x = x
    bpy.context.view_layer.update()
    lowest = min((obj.matrix_world @ Vector(corner)).z for corner in obj.bound_box)
    obj.location.z -= lowest


def add_label(body: str, x: float, y: float) -> None:
    curve = bpy.data.curves.new(body, "FONT")
    curve.body = body
    curve.align_x = "CENTER"
    curve.size = 0.55
    curve.extrude = 0.01
    material = solid(body + " label", (0.85, 0.78, 0.58), 0.7)
    curve.materials.append(material)
    obj = bpy.data.objects.new(body, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = (x, y, 0.05)
    obj.rotation_euler = (0, 0, 0)


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    clear()
    baseline = import_house("House_Baseline")
    candidate = import_house("House_PBR_Pilot")
    legacy = [solid("Legacy Wood Light", (0.80, 0.74, 0.50), 0.72), solid("Legacy Black", (0.02, 0.02, 0.02), 0.9), solid("Legacy Player", (0.12, 0.32, 0.78), 0.52), solid("Legacy Wood", (0.25, 0.16, 0.04), 0.76)]
    baseline.data.materials.clear()
    for material in legacy:
        baseline.data.materials.append(material)
    pilot = [pbr("Pilot Wood", "Wood", 1.0, 0.76), pbr("Pilot Roof", "Roof", 1.0, 0.70), legacy[2], pbr("Pilot Floor", "Wood", 1.35, 0.72)]
    candidate.data.materials.clear()
    for material in pilot:
        candidate.data.materials.append(material)
    place_on_ground(baseline, -5.2)
    place_on_ground(candidate, 5.2)

    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    floor = bpy.context.object
    floor.data.materials.append(solid("Ground", (0.055, 0.065, 0.075), 0.92))
    add_label("BASELINE", -5.2, -3.0)
    add_label("HOUSE PBR PILOT", 5.2, -3.0)

    light_data = bpy.data.lights.new("Key", "AREA")
    light_data.energy = 1500
    light_data.shape = "DISK"
    light_data.size = 6
    light = bpy.data.objects.new("Key", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (0, -4, 12)
    look_at(light, Vector((0, 0, 1.5)))
    fill_data = bpy.data.lights.new("Fill", "AREA")
    fill_data.energy = 850
    fill_data.shape = "DISK"
    fill_data.size = 8
    fill = bpy.data.objects.new("Fill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (0, -14, 7)
    look_at(fill, Vector((0, 0, 1.6)))
    world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.015, 0.02, 0.035, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.28

    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (18.0, -31.0, 17.0)
    look_at(camera, Vector((0, 0, 2.2)))
    camera_data.lens = 42
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(OUT)
    scene.render.film_transparent = False
    OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print(f"HOUSE_PBR_PILOT_OFFLINE_CAPTURED {OUT}")


if __name__ == "__main__":
    main()
