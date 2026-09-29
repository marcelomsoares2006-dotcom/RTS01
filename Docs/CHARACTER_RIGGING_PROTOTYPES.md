# Character-rigging prototype pipeline

Date: 2026-09-28. Blender 5.2.1 LTS; target project Unity 6000.6.3f1.

## Deliverables

`Assets/CharacterRigging/Prototypes/` contains the editable Rigify `.blend` and
three skinned FBX blockouts. `Tools/CharacterRigging/Inputs/` contains matching
GLB fixtures intended for SkinTokens experiments, not for the shipped game.
The meshes are simple joined low-poly primitive forms: they validate skeleton
structure, skin weights, export and deformation, but are not finished character
art or production-ready topology.

The humanoid and wolf use Rigify's humanoid and wolf metarigs; the horse uses
Rigify's horse metarig. The build script automatically binds each blockout,
limits each vertex to four influences, then normalizes the weights before GLB
and FBX export.

## Tests run

Commands (run from the project root):

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python Tools/CharacterRigging/BuildCharacterRigPrototypes.py
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background Assets/CharacterRigging/Prototypes/EraImperial_Rigify_Prototypes.blend --python Tools/CharacterRigging/ValidateCharacterRigPrototypes.py
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python Tools/CharacterRigging/VerifyFbxRoundTrip.py
```

All six per-species checks passed:

| Fixture | Vertices | Groups/FBX bones | Max influences | Weight sum | Pose delta |
|---|---:|---:|---:|---:|---:|
| Human | 2,336 | 160 / 278 | 4 | 1.0000 | 0.101109 (FBX round-trip: 0.403601) |
| Horse | 2,628 | 80 / 122 | 4 | 1.0000 | 0.197184 (FBX round-trip: 0.799966) |
| Wolf | 2,044 | 197 / 333 | 4 | 1.0000 | 0.134437 (FBX round-trip: 0.418076) |

Pose delta is the maximum vertex movement in Blender units after rotating a
weighted bone; it proves that skinning still works after export and reimport.
It does not measure animation quality.

## Unity import status

An Editor audit script is staged at `Assets/Editor/CharacterRiggingImportAudit.cs`.
It sets the human model to Humanoid, animals to Generic, reimports them, and
checks Avatar validity plus `SkinnedMeshRenderer` data. It has **not passed yet**:
the Unity Editor launched by this machine presents “Administrator Privileges
Detected”, while headless Unity stalls during Licensing Client IPC
initialization. The Hub itself reports the Unity Student license active through
2027-09-30; license validity is not the observed blocker. No UAC, account, or
Windows security setting was changed, and the FBXs have not been attached to
game prefabs/scenes.

## SkinTokens and Kimodo assessment

- **SkinTokens C++:** the upstream C++23 port documents a Linux-first build
  (CMake 3.25+, Ninja, Git, compiler and optional Vulkan), and supports both
  generation from a static mesh and predicting weights for an existing GLB
  skeleton. No CMake or C++ compiler is present in this Windows environment, so
  its model inference has not run. The original Python project is GPU-focused;
  prefer the GGML C++ port and try Vulkan on the Radeon only after the native
  build succeeds.
- **Kimodo C++:** upstream provides the motion-generation implementation; the
  Windows-ready path found is a small community fork, not an official Windows
  release. Its documented setup requires Visual Studio C++ Build Tools, CMake,
  Python and about 16.3 GB of checkpoints/text-encoder data for a minimal model.
  Those dependencies have not been installed or downloaded. The SOMA motion
  skeleton has 30 joints, so its output will need explicit retargeting before
  it can animate the Rigify human skeleton; it is not a drop-in Rigify action.
- **Recommended next validation:** resolve a standard-user Unity Editor launch,
  run the staged importer audit, then build SkinTokens and Kimodo in a user
  workspace once a supported C++ toolchain is available. Keep AI outputs as
  separate test assets until retargeting and Unity animation playback pass.

Rerun instructions are encoded in `Tools/CharacterRigging/BuildCharacterRigPrototypes.py`,
`Tools/CharacterRigging/ValidateCharacterRigPrototypes.py`, and
`Tools/CharacterRigging/VerifyFbxRoundTrip.py`.
