"""Create non-production Rigify rigs and low-poly skinning test meshes.

Run with Blender 5.2:
  blender --background --factory-startup --python Tools/CharacterRigging/BuildCharacterRigPrototypes.py

Outputs are staged in Assets/CharacterRigging; the GLBs are kept under Tools as
local inference fixtures and are not imported into the game.
"""

from pathlib import Path
import bpy


PROJECT = Path(__file__).resolve().parents[2]
OUTPUT = PROJECT / "Assets" / "CharacterRigging" / "Prototypes"
TEST_INPUTS = PROJECT / "Tools" / "CharacterRigging" / "Inputs"
BLEND_PATH = OUTPUT / "EraImperial_Rigify_Prototypes.blend"

SPECIES = ("human", "horse", "wolf")


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)


def create_material(name, colour):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*colour, 1.0)
    return material


def add_ellipsoid(name, location, radii, material, segments=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, radius=1.0, location=location
    )
    part = bpy.context.object
    part.name = name
    part.scale = radii
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    part.data.materials.append(material)
    return part


def make_proxy(species):
    """A neutral low-poly blockout for testing bones, export and skin weights."""
    skin = create_material(f"{species.title()}_Blockout", (0.56, 0.34, 0.22))
    parts = []
    if species == "human":
        parts.extend([
            add_ellipsoid("Pelvis", (0, 0, 1.00), (0.22, 0.15, 0.18), skin),
            add_ellipsoid("Torso", (0, 0, 1.35), (0.27, 0.16, 0.34), skin),
            add_ellipsoid("Neck", (0, 0, 1.68), (0.09, 0.09, 0.12), skin),
            add_ellipsoid("Head", (0, -0.01, 1.84), (0.14, 0.13, 0.18), skin),
        ])
        for side in (-1, 1):
            parts.extend([
                add_ellipsoid("UpperArm", (side * 0.34, 0, 1.43), (0.10, 0.10, 0.24), skin),
                add_ellipsoid("Forearm", (side * 0.43, 0, 1.12), (0.085, 0.085, 0.21), skin),
                add_ellipsoid("Hand", (side * 0.46, -0.01, 0.91), (0.09, 0.07, 0.10), skin),
                add_ellipsoid("Thigh", (side * 0.13, 0, 0.70), (0.13, 0.14, 0.29), skin),
                add_ellipsoid("Shin", (side * 0.13, 0, 0.32), (0.095, 0.105, 0.25), skin),
                add_ellipsoid("Foot", (side * 0.13, -0.055, 0.08), (0.10, 0.19, 0.07), skin),
            ])
    elif species == "horse":
        parts.extend([
            add_ellipsoid("Barrel", (0, 0.38, 1.38), (0.32, 0.56, 0.34), skin),
            add_ellipsoid("Chest", (0, -0.28, 1.40), (0.28, 0.27, 0.34), skin),
            add_ellipsoid("Neck", (0, -0.62, 1.67), (0.19, 0.36, 0.39), skin),
            add_ellipsoid("Head", (0, -0.91, 1.96), (0.14, 0.21, 0.24), skin),
            add_ellipsoid("Muzzle", (0, -1.08, 1.85), (0.12, 0.15, 0.12), skin),
            add_ellipsoid("Tail", (0, 0.91, 1.42), (0.08, 0.28, 0.11), skin),
        ])
        for side in (-1, 1):
            for fore, y in ((True, -0.34), (False, 0.78)):
                x = side * 0.18
                parts.append(add_ellipsoid("UpperLeg", (x, y, 0.92), (0.10, 0.12, 0.27), skin))
                parts.append(add_ellipsoid("LowerLeg", (x, y - 0.02, 0.48), (0.065, 0.075, 0.30), skin))
                parts.append(add_ellipsoid("Hoof", (x, y - 0.055, 0.10), (0.095, 0.12, 0.09), skin))
    else:
        parts.extend([
            add_ellipsoid("Body", (0, 0.28, 0.77), (0.22, 0.49, 0.23), skin),
            add_ellipsoid("Chest", (0, -0.18, 0.78), (0.21, 0.27, 0.24), skin),
            add_ellipsoid("Neck", (0, -0.39, 0.90), (0.13, 0.25, 0.17), skin),
            add_ellipsoid("Head", (0, -0.62, 0.99), (0.15, 0.20, 0.15), skin),
            add_ellipsoid("Muzzle", (0, -0.79, 0.94), (0.09, 0.13, 0.08), skin),
            add_ellipsoid("Tail", (0, 0.82, 0.79), (0.07, 0.26, 0.07), skin),
        ])
        for side in (-1, 1):
            for y in (-0.38, 0.65):
                x = side * 0.14
                parts.append(add_ellipsoid("UpperLeg", (x, y, 0.51), (0.075, 0.10, 0.23), skin))
                parts.append(add_ellipsoid("LowerLeg", (x, y - 0.015, 0.22), (0.055, 0.07, 0.18), skin))

    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    mesh = bpy.context.object
    mesh.name = f"{species.title()}_SkinningTestMesh"
    return mesh


def add_rigify_metarig(species):
    operator = getattr(bpy.ops.object, f"armature_{species}_metarig_add")
    operator()
    meta = bpy.context.object
    meta.name = f"{species.title()}_METARIG"
    bpy.ops.pose.rigify_generate()
    rig = bpy.context.object
    rig.name = f"{species.title()}_RIG"
    for obj in bpy.context.selected_objects:
        if obj.type == "ARMATURE" and obj is not rig:
            obj.name = f"{species.title()}_METARIG"
            meta = obj
    return meta, rig


def bind_proxy(mesh, rig):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    if mesh.find_armature() is not rig:
        raise RuntimeError(f"{mesh.name} failed to bind to {rig.name}")
    if not mesh.vertex_groups:
        raise RuntimeError(f"{mesh.name} has no generated skin weights")
    # Unity and glTF skinning are most portable with at most four influences.
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)


def export_asset(species, mesh, rig):
    name = species.title()
    TEST_INPUTS.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    glb = TEST_INPUTS / f"{name}_SkinningFixture.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(glb), export_format="GLB", use_selection=True,
        export_skins=True, export_animations=False,
    )
    fbx = OUTPUT / f"{name}_SkinnedPrototype.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(fbx), use_selection=True, object_types={"ARMATURE", "MESH"},
        use_armature_deform_only=True, add_leaf_bones=False,
        bake_anim=False, axis_forward="-Z", axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
    )
    return glb, fbx


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    TEST_INPUTS.mkdir(parents=True, exist_ok=True)
    clear_scene()
    bpy.ops.preferences.addon_enable(module="rigify")
    records = []
    for species in SPECIES:
        meta, rig = add_rigify_metarig(species)
        mesh = make_proxy(species)
        bind_proxy(mesh, rig)
        glb, fbx = export_asset(species, mesh, rig)
        records.append((species, len(meta.data.bones), len(rig.data.bones), len(mesh.data.vertices), len(mesh.vertex_groups), str(glb), str(fbx)))
        for obj in (meta, rig, mesh):
            obj.select_set(False)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    report = OUTPUT / "generation-report.txt"
    report.write_text(
        "Rigify 5.2 prototype generation\n"
        "These are blockout skinning fixtures, not finished character art.\n"
        + "\n".join(
            f"{species}: metaBones={meta}, controlRigBones={rig}, vertices={verts}, "
            f"weightGroups={groups}, glb={glb}, fbx={fbx}"
            for species, meta, rig, verts, groups, glb, fbx in records
        ) + "\n",
        encoding="utf-8",
    )
    print(report.read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
