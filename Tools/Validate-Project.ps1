param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe',
    [switch]$Migrate,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
$reviewProject = Split-Path $PSScriptRoot -Parent
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
$reviewOpen = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Where-Object { $_.CommandLine -like "*$reviewProject*" }
if ($reviewOpen) { throw 'Close this project in Unity before running batch validation. No process was terminated.' }
New-Item -ItemType Directory -Force -Path (Join-Path $reviewProject 'TestResults') | Out-Null

function Invoke-ReviewUnity([string]$Name, [string]$Arguments, [int]$TimeoutSeconds = 300) {
    $reviewLog = Join-Path $reviewProject "TestResults\$Name.log"
    $reviewArguments = '-batchmode -acceptSoftwareTermsForThisRunOnly -projectPath "' + $reviewProject +
        '" -logFile "' + $reviewLog + '" ' + $Arguments
    $reviewProcess = Start-Process -FilePath $UnityPath -ArgumentList $reviewArguments -WindowStyle Hidden -PassThru
    if (!$reviewProcess.WaitForExit($TimeoutSeconds * 1000)) {
        # Only the process created by this invocation is terminated.
        Stop-Process -Id $reviewProcess.Id
        throw "$Name exceeded $TimeoutSeconds seconds. See $reviewLog"
    }
    if ($reviewProcess.ExitCode -ne 0) { throw "$Name failed ($($reviewProcess.ExitCode)). See $reviewLog" }
    Write-Host "$Name completed successfully."
}

if ($Migrate) {
    Invoke-ReviewUnity 'migration' '-quit -executeMethod EraImperialValidation.MigrateAndAudit'
}
$reviewResults = Join-Path $reviewProject 'TestResults\unit-tests.xml'
Invoke-ReviewUnity 'unit-tests' ('-runTests -testPlatform EditMode -testResults "' + $reviewResults + '"')
[xml]$reviewXml = Get-Content -LiteralPath $reviewResults
$reviewRun = $reviewXml.'test-run'
$passed = [int]$reviewRun.passed
$failed = [int]$reviewRun.failed
$skipped = [int]$reviewRun.skipped
$inconclusive = [int]$reviewRun.inconclusive
if ($reviewRun.result -ne 'Passed' -or $passed -le 0 -or $failed -ne 0 -or $skipped -ne 0 -or $inconclusive -ne 0) {
    throw "EditMode tests did not pass cleanly: result=$($reviewRun.result), passed=$passed, failed=$failed, skipped=$skipped, inconclusive=$inconclusive. Inspect unit-tests.xml."
}
Invoke-ReviewUnity 'playmode' '-executeMethod EraImperialSmokeTests.Run'
if ($Build) {
    Invoke-ReviewUnity 'windows-build' '-quit -executeMethod EraImperialValidation.BuildWindows' 1200
}
Write-Host 'Validation complete. Results and rendered screenshots: TestResults.'
