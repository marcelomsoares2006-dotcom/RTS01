"""Renderiza uma comparação offline das muralhas sem alterar ativos Unity."""
from __future__ import annotations

import bpy
from mathutils import Vector
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "TestResults" / "WallPbrVisualQA-2026-10-19"
TEXTURES = ROOT / "Assets" / "CityArchitecture" / "Generated" / "Textures"
CANDIDATES = ROOT / "Assets" / "CityArchitecture" / "Materials" / "Candidates"
ERAS = ("Ancient", "Classical", "Medieval", "Imperial")
PROFILES = {"Ancient": "Stone", "Classical": "Plaster", "Medieval": "Stone", "Imperial": "Plaster"}


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


def texture_node(path: Path, non_color: bool = False):
    node = bpy.context.scene.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = bpy.data.images.load(str(path), check_existing=True)
    if non_color:
        node.image.colorspace_settings.name = "Non-Color"
    return node


def pbr(name: str, era: str):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    albedo = nodes.new("ShaderNodeTexImage")
    normal = nodes.new("ShaderNodeTexImage")
    metal = nodes.new("ShaderNodeTexImage")
    ao = nodes.new("ShaderNodeTexImage")
    albedo.image = bpy.data.images.load(str(CANDIDATES / f"Wall_{era}_Albedo.png"), check_existing=True)
    normal.image = bpy.data.images.load(str(CANDIDATES / f"Wall_{era}_Normal.png"), check_existing=True)
    metal.image = bpy.data.images.load(str(CANDIDATES / f"Wall_{era}_MetallicGloss.png"), check_existing=True)
    ao.image = bpy.data.images.load(str(CANDIDATES / f"Wall_{era}_AO.png"), check_existing=True)
    for node in (normal, metal, ao):
        node.image.colorspace_settings.name = "Non-Color"
    links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.55
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    links.new(metal.outputs["Color"], bsdf.inputs["Metallic"])
    bsdf.inputs["Roughness"].default_value = 0.72 if era != "Imperial" else 0.68
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (0.5, 0.5, 0.5, 1.0)
    return material


def baseline(era: str):
    profile = PROFILES[era]
    material = bpy.data.materials.new("Production_" + era)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    albedo = nodes.new("ShaderNodeTexImage")
    albedo.image = bpy.data.images.load(str(TEXTURES / f"PBR_{profile}.png"), check_existing=True)
    links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.50
    return material


def cube(name: str, location: tuple[float, float, float], scale: tuple[float, float, float], material):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    return obj


def build_wall(name: str, era: str, x: float, z: float, material):
    row = ERAS.index(era)
    height = 2.6 + row * 0.35
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    parts = [cube(name + "_Body", (x, height * 0.5, z), (6.0, height, 0.55), material)]
    for side in (-1, 1):
        parts.append(cube(name + "_Pillar", (x + side * 3.0, height * 0.55, z), (0.7, height * 1.1, 0.8), material))
    count = int(max(3, min(8, round(6.0))))
    for i in range(count):
        px = x - 2.75 + i * (5.5 / (count - 1))
        parts.append(cube(name + "_Merlon", (px, height + 0.28, z), (0.42, 0.55, 0.55), material))
    for part in parts:
        part.parent = root
    return root


def label(body: str, location: tuple[float, float, float], color=(0.86, 0.78, 0.60)):
    curve = bpy.data.curves.new(body, "FONT")
    curve.body = body
    curve.align_x = "CENTER"
    curve.size = 0.34
    curve.extrude = 0.008
    mat = solid(body + " label", color, 0.75)
    curve.materials.append(mat)
    obj = bpy.data.objects.new(body, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return obj


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render(scene, camera, filename: str, location: tuple[float, float, float], target: tuple[float, float, float], lens: float):
    camera.location = location
    look_at(camera, Vector(target))
    camera.data.lens = lens
    scene.camera = camera
    scene.render.filepath = str(OUT / filename)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    clear()
    OUT.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.exposure = 1.7

    for row, era in enumerate(ERAS):
        z = row * 7.0
        build_wall("Production_" + era, era, -3.6, z, baseline(era))
        build_wall("Candidate_" + era, era, 3.6, z, pbr("Candidate_" + era, era))
        label("PRODUCTION", (-3.6, -0.40, z - 0.55))
        label("PBR CANDIDATE", (3.6, -0.40, z - 0.55))
        label(era.upper(), (0.0, -0.40, z - 1.00), (0.90, 0.86, 0.72))

    floor = cube("Ground", (0.0, -0.18, 10.5), (34.0, 0.30, 31.0), solid("Ground", (0.045, 0.055, 0.065), 0.92))

    world = bpy.data.worlds.new("Wall QA World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.018, 0.026, 0.045, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.50

    key_data = bpy.data.lights.new("Wall QA Key", "AREA")
    key_data.energy = 5200
    key_data.shape = "DISK"
    key_data.size = 7.0
    key = bpy.data.objects.new("Wall QA Key", key_data)
    bpy.context.collection.objects.link(key)
    key.location = (-8.0, -13.0, 14.0)
    look_at(key, Vector((0.0, 1.8, 10.0)))

    fill_data = bpy.data.lights.new("Wall QA Fill", "AREA")
    fill_data.energy = 2800
    fill_data.shape = "DISK"
    fill_data.size = 9.0
    fill = bpy.data.objects.new("Wall QA Fill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (11.0, -10.0, 10.0)
    look_at(fill, Vector((0.0, 2.0, 9.0)))

    camera_data = bpy.data.cameras.new("Wall QA Camera")
    camera = bpy.data.objects.new("Wall QA Camera", camera_data)
    bpy.context.collection.objects.link(camera)

    render(scene, camera, "wall-pbr-offline-close-ancient.png", (0.0, 4.8, -18.0), (0.0, 1.8, 0.2), 55.0)
    render(scene, camera, "wall-pbr-offline-mid-ancient-classical.png", (7.0, 7.0, -27.0), (0.0, 2.0, 3.6), 55.0)
    render(scene, camera, "wall-pbr-offline-far-all-eras.png", (11.0, 10.0, -43.0), (0.0, 2.2, 10.5), 55.0)
    render(scene, camera, "wall-pbr-offline-grid.png", (0.0, 14.0, -35.0), (0.0, 2.0, 10.5), 60.0)
    print(f"WALL_PBR_OFFLINE_CAPTURED {OUT}")


if __name__ == "__main__":
    main()
