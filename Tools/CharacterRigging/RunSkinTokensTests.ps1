param(
    [string]$Configuration = 'windows-release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$buildRoot = Join-Path $repoRoot "Tools\CharacterRigging\ThirdParty\skin-tokens.cpp\build\$Configuration"
$testsRoot = Join-Path $buildRoot 'tests'
$dllPath = Join-Path $buildRoot 'skintokens.dll'

if (-not (Test-Path -LiteralPath $dllPath)) {
    throw "SkinTokens DLL not found at '$dllPath'. Build the SkinTokens project first."
}

$env:PATH = "$buildRoot;$buildRoot\bin;$env:PATH"
$testNames = @(
    'skintokens-api-test.exe',
    'skintokens-binding-test.exe',
    'skintokens-c-api-test.exe',
    'skintokens-tokenizer-test.exe'
)
$failed = [System.Collections.Generic.List[string]]::new()

Push-Location $testsRoot
try {
    foreach ($testName in $testNames) {
        $testPath = Join-Path $testsRoot $testName
        if (-not (Test-Path -LiteralPath $testPath)) {
            Write-Host "SKIP $testName (not built)" -ForegroundColor Yellow
            continue
        }

        & $testPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "PASS $testName" -ForegroundColor Green
        }
        else {
            Write-Host "FAIL $testName (exit $LASTEXITCODE)" -ForegroundColor Red
            $failed.Add($testName)
        }
    }
}
finally {
    Pop-Location
}

if ($failed.Count -gt 0) {
    throw "SkinTokens tests failed: $($failed -join ', ')"
}
