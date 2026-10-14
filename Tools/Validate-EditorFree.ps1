<#
    Editor-free smoke validation for RTS01.

    Validates repository and serialized-project invariants without starting Unity.
    C# compilation, scene deserialization, runtime smoke, and player builds still
    require the licensed Unity editor and remain in Validate-Project.ps1.
#>
param(
    [switch]$SkipReleasePreflight
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot

function Require-File([string]$RelativePath) {
    $path = Join-Path $projectRoot $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file is missing: $RelativePath" }
    return $path
}

function Require-Text([string]$RelativePath, [string]$Pattern, [string]$Description) {
    $path = Require-File $RelativePath
    $text = Get-Content -Raw -LiteralPath $path
    if ($text -notmatch $Pattern) { throw "Required $Description was not found in $RelativePath" }
    return $text
}

function Relative-Path([string]$Path) {
    return $Path.Substring($projectRoot.Length + 1).Replace('\', '/')
}

function Get-GuidIndex {
    $index = @{}
    foreach ($meta in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets') -Recurse -File -Filter '*.meta') {
        $text = Get-Content -Raw -LiteralPath $meta.FullName
        $match = [regex]::Match($text, '(?m)^guid:\s*([0-9a-f]{32})\s*$')
        if (!$match.Success) { throw "Unity metadata has no valid GUID: $(Relative-Path $meta.FullName)" }
        $metaPath = Relative-Path $meta.FullName
        $assetPath = $metaPath.Substring(0, $metaPath.Length - 5)
        $guid = $match.Groups[1].Value
        if ($index.ContainsKey($guid) -and $index[$guid] -ne $assetPath) { throw "Duplicate Unity asset GUID ${guid}: $($index[$guid]) and $assetPath" }
        $index[$guid] = $assetPath
    }
    return $index
}

function Test-BuildScenes([hashtable]$GuidIndex) {
    $settings = Get-Content -Raw -LiteralPath (Require-File 'ProjectSettings/EditorBuildSettings.asset')
    $records = [regex]::Matches($settings, '(?ms)^\s*- enabled:\s*(\d+)\s*\r?\n\s+path:\s*(Assets/[^\r\n]+)\s*\r?\n\s+guid:\s*([0-9a-f]{32})')
    if ($records.Count -lt 1) { throw 'EditorBuildSettings.asset contains no scenes.' }
    $seen = @{}
    foreach ($record in $records) {
        $enabled = [int]$record.Groups[1].Value
        $path = $record.Groups[2].Value.Trim()
        $guid = $record.Groups[3].Value
        if ($enabled -ne 1) { throw "Build scene is disabled: $path" }
        if ($seen.ContainsKey($guid)) { throw "Duplicate build scene GUID: $guid" }
        $seen[$guid] = $true
        Require-File $path | Out-Null
        if (!$GuidIndex.ContainsKey($guid) -or $GuidIndex[$guid] -ne $path) { throw "Build scene GUID/path mismatch: $path ($guid)" }
    }
    return $records.Count
}

function Test-ConstructionContracts {
    $required = @(
        @{ Path = 'Assets/Scripts/Units/Buildings/Building.cs'; Pattern = 'class\s+Building\b'; Name = 'Building runtime' },
        @{ Path = 'Assets/Scripts/Units/Buildings/BuildingManager.cs'; Pattern = 'class\s+BuildingManager\b'; Name = 'Building manager' },
        @{ Path = 'Assets/Scripts/Units/Buildings/BuildingPlacer.cs'; Pattern = 'class\s+BuildingPlacer\b'; Name = 'Building placement' },
        @{ Path = 'Assets/Scripts/ScriptableObjects/Units/BuildingData.cs'; Pattern = 'class\s+BuildingData\s*:\s*UnitData'; Name = 'BuildingData asset' },
        @{ Path = 'Assets/Scripts/ScriptableObjects/Units/BuildingContentCatalog.cs'; Pattern = 'class\s+BuildingContentCatalog\s*:\s*ScriptableObject'; Name = 'building content catalog' },
        @{ Path = 'Assets/Scripts/Progression/BuildingContentProgressionRules.cs'; Pattern = 'class\s+BuildingContentProgressionRules\b'; Name = 'building progression bridge' },
        @{ Path = 'Assets/Scripts/StrategicAI/ConstructionPlanningRules.cs'; Pattern = 'class\s+ConstructionPlanningRules\b'; Name = 'construction planning rules' },
        @{ Path = 'Assets/Scripts/ScriptableObjects/Units/BuildingConstructionGate.cs'; Pattern = 'class\s+BuildingConstructionGate\b'; Name = 'construction gate' },
        @{ Path = 'Assets/Scripts/Units/Buildings/BuildingProductionQueue.cs'; Pattern = 'class\s+BuildingProductionQueue\b'; Name = 'production queue' },
        @{ Path = 'Assets/Scripts/Units/Buildings/BuildingFeedback.cs'; Pattern = 'class\s+BuildingFeedbackEmitter\b'; Name = 'construction feedback' },
        @{ Path = 'Assets/Scripts/Units/Buildings/BuildingMaintenanceRuntimeBinding.cs'; Pattern = 'class\s+BuildingMaintenanceRuntimeBinding\b'; Name = 'maintenance runtime binding' }
    )
    foreach ($item in $required) { Require-Text $item.Path $item.Pattern $item.Name | Out-Null }
    $requiredTests = @(
        'Assets/Tests/Editor/BuildingConstructionGateTests.cs',
        'Assets/Tests/Editor/BuildingConstructionRulesTests.cs',
        'Assets/Tests/Editor/BuildingContentCatalogTests.cs',
        'Assets/Tests/Editor/BuildingProductionQueueTests.cs',
        'Assets/Tests/Editor/BuildingFeedbackRulesTests.cs',
        'Assets/Tests/Editor/BuildingMaintenanceRuntimeBindingTests.cs',
        'Assets/Tests/Editor/BuildingContentProgressionRulesTests.cs',
        'Assets/Tests/Editor/ConstructionPlanningRulesTests.cs'
    )
    foreach ($test in $requiredTests) { Require-File $test | Out-Null }
    Require-Text 'Assets/Scripts/Progression/BuildingContentProgressionRules.cs' 'ValidateEraContracts' 'building era progression contract' | Out-Null
    $editorTestsAsmdef = Join-Path $projectRoot 'Assets/Tests/Editor/EraImperial.EditorTests.asmdef'
    if (Test-Path $editorTestsAsmdef) {
        throw 'Editor tests must remain in Unity''s predefined Assembly-CSharp-Editor assembly so they can access runtime types.'
    }
    return $required.Count
}

function Test-CatalogContracts([hashtable]$GuidIndex) {
    $catalogPath = 'Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset'
    $catalog = Require-Text $catalogPath '(?m)^\s*entries:' 'building catalog entries'
    $blocks = [regex]::Matches($catalog, '(?ms)^\s*- code:\s*(\S+)(.*?)(?=^\s*- code:|\z)')
    if ($blocks.Count -lt 2) { throw 'Building catalog must contain at least Town Center and House entries.' }
    $codes = @{}
    $bodies = @{}
    $playable = 0
    foreach ($block in $blocks) {
        $code = $block.Groups[1].Value
        $body = $block.Groups[2].Value
        if ($codes.ContainsKey($code.ToLowerInvariant())) { throw "Duplicate building code: $code" }
        $codes[$code.ToLowerInvariant()] = $true
        $bodies[$code.ToLowerInvariant()] = $body
        foreach ($field in @('displayName', 'prefabPath', 'modelPath', 'populationCapacity', 'acceptanceCriteria')) {
            if ($body -notmatch "(?m)^\s*${field}:\s*[^\r\n]+") { throw "Catalog entry '$code' has no $field." }
        }
        $statusMatch = [regex]::Match($body, '(?m)^\s*status:\s*(\d+)\s*$')
        $prefabMatch = [regex]::Match($body, '(?m)^\s*prefabPath:\s*([^\r\n]+)')
        $modelMatch = [regex]::Match($body, '(?m)^\s*modelPath:\s*([^\r\n]+)')
        if (!$statusMatch.Success -or !$prefabMatch.Success -or !$modelMatch.Success) { throw "Catalog entry '$code' is missing lifecycle or asset fields." }
        if ([int]$statusMatch.Groups[1].Value -ge 2) {
            $playable++
            $prefab = $prefabMatch.Groups[1].Value.Trim()
            $model = $modelMatch.Groups[1].Value.Trim().TrimEnd('/')
            Require-File $prefab | Out-Null
            if (!(Test-Path -LiteralPath (Join-Path $projectRoot $model))) { throw "Playable entry '$code' model path is missing: $model" }
            $data = [regex]::Match($body, 'buildingData:\s*\{[^}]*guid:\s*([0-9a-f]{32})')
            if (!$data.Success -or !$GuidIndex.ContainsKey($data.Groups[1].Value)) { throw "Playable entry '$code' has no resolvable BuildingData GUID." }
        }
    }
    $requiredCodes = @('town_center', 'house', 'resource_dropoff', 'barracks', 'archery_range', 'stable', 'mining_camp', 'lumber_camp', 'mill', 'tower')
    foreach ($requiredCode in $requiredCodes) {
        if (!$codes.ContainsKey($requiredCode)) { throw "Catalog is missing required building contract: $requiredCode" }
    }
    foreach ($productionCode in @('town_center', 'barracks', 'archery_range', 'stable')) {
        if ($bodies[$productionCode] -notmatch '(?ms)^\s*production:\s*\r?\n\s*- productCode:\s*\S+') {
            throw "Catalog entry '$productionCode' has no production contract."
        }
    }
    foreach ($dropoffCode in @('town_center', 'resource_dropoff', 'mining_camp', 'lumber_camp', 'mill')) {
        if ($bodies[$dropoffCode] -notmatch '(?ms)^\s*constructionCost:\s*\r?\n\s*- resource:\s*\d+\s*\r?\n\s*amount:\s*[1-9]\d*') {
            throw "Catalog entry '$dropoffCode' has no positive construction cost."
        }
    }
    if ($playable -lt 1) { throw 'Building catalog has no playable entries.' }
    return [pscustomobject]@{ Entries = $blocks.Count; Playable = $playable }
}

if (!$SkipReleasePreflight) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot 'Tools/Validate-Release.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Validate-Release.ps1 failed with exit code $LASTEXITCODE." }
}
Require-File 'ProjectSettings/ProjectVersion.txt' | Out-Null
$version = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')
if ($version -notmatch 'm_EditorVersion:\s*6000\.6\.3f1\b') { throw 'Unsupported Unity editor version.' }
$guidIndex = Get-GuidIndex
$sceneCount = Test-BuildScenes $guidIndex
$contractCount = Test-ConstructionContracts
$catalog = Test-CatalogContracts $guidIndex
Write-Host ("Editor-free smoke passed: Unity 6000.6.3f1; {0} build scenes; {1} construction contracts; {2} catalog entries ({3} playable); {4} assets indexed." -f $sceneCount, $contractCount, $catalog.Entries, $catalog.Playable, $guidIndex.Count)
Write-Host 'Unity-dependent evidence still required: C# compilation, EditMode/PlayMode Test Runner, scene deserialization/runtime smoke, and player build.'
