# Character rigging toolchain status

Updated: 2026-09-28. This file supersedes the earlier toolchain assessment in
`CHARACTER_RIGGING_PROTOTYPES.md`, which predates the Windows setup and contains
out-of-date installation claims.

## Installed and verified

- Visual Studio Build Tools 18 / MSVC 19.51, Windows SDK 10.0.26100, CMake
  4.3.1, Ninja 1.13.2, Git and Python 3.12. A C++23 smoke program compiled,
  linked and ran.
- Official Kimodo C++ source configured and built with its CPU backend. Both
  `kmd-generate.exe` and `kmd-inspect.exe` were produced; its diffusion unit
  test passed. Model weights were not downloaded, so AI motion inference has
  not been tested.
- Official SkinTokens C++ source built in Windows CPU/Release mode. API,
  binding, C API and tokenizer test executables each passed. Run them using
  `Tools/CharacterRigging/RunSkinTokensTests.ps1`, which adds the sibling DLL
  directory to `PATH`. Launching a test executable directly from Explorer can
  show “skintokens.dll was not found”.

## Still incomplete

Vulkan runtime and the Radeon driver are visible to the system, but the local
vcpkg attempt stopped while compiling SPIRV-Tools. MSVC reported C1083:
“Cannot open compiler generated file: ''”. No SkinTokens Vulkan build or GPU
inference has been validated. Kimodo CPU build/tests do not prove generation
works without model weights; downloading those weights is a separate, multi-GB
step with model-specific license terms.

Unity Editor FBX import and animation playback have not been verified in this
toolchain session. No UAC, account, or Windows security setting was changed.
