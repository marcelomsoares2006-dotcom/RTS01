# Release validation

This project targets Unity 6000.6.3f1. The repository keeps package versions
in both Packages/manifest.json and Packages/packages-lock.json; the preflight
below checks that they agree and that every enabled build scene is present. It
does not modify assets or gameplay.

## Fast, editor-free preflight

From the project root in PowerShell:

    Set-Location D:\RTS01
    & .\Tools\Validate-Release.ps1

This validates the Unity version declaration, package manifest/lock agreement,
required validation files, all enabled build scene paths and GUIDs, Unity
`.meta` coverage for source and shader files, and the building content catalog.
Catalog validation checks stable entry codes, `BuildingData` GUID references,
and paths for playable prefabs and models. Planned catalog paths may remain
absent until their status is promoted to Playable.

## Licensed Unity validation

Use a Unity Hub editor installation that is already activated for the account
or machine. Unity batch mode still needs a valid editor license; this project
does not store credentials and the validation routine never accepts or writes
license secrets.

    $unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe'
    & .\Tools\Validate-Release.ps1 -WithUnity -UnityPath $unity

The Unity pass runs the existing project checks in Tools/Validate-Project.ps1:
Edit Mode tests, the three-scenario Play Mode smoke test, and the migration/audit
pass only when -Migrate is explicitly requested. The project must
be closed in the editor before batch validation. Results are written under
TestResults/, which is ignored by Git.

To include the migration and scene audit in the same run:

    & .\Tools\Validate-Release.ps1 -WithUnity -Migrate -UnityPath $unity

For a local Windows development build after the tests pass:

    & .\Tools\Validate-Release.ps1 -WithUnity -Build -UnityPath $unity

-Build invokes the existing EraImperialValidation.BuildWindows method and
produces Builds/Windows/EraImperial.exe; builds are ignored by Git.

## CI policy

Run the editor-free preflight on every change. Run the licensed Unity pass on a
machine with Unity 6000.6.3f1 activated, because Unity editor binaries and
licenses are intentionally not committed to this repository. Preserve the
TestResults logs as CI artifacts when a run fails. Do not add Library/, Temp/,
editor credentials, or generated builds to Git.
