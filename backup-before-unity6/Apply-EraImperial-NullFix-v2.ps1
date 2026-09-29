$ErrorActionPreference = "Stop"

$root = Get-Location
$path = Join-Path $root "Assets\Scripts\Tools\Utils.cs"

if (!(Test-Path $path)) {
    throw "Utils.cs nao foi encontrado. Execute na pasta que contem Assets."
}

Copy-Item $path "$path.bak-nullfix-v2" -Force
$text = Get-Content $path -Raw

$pattern = '(?s)    public static \(Vector3, Vector3\) GetCameraWorldBounds\(\)\s*\{.*?\n    \}\s*\n\s*public static Sprite LoadSpriteFromFile'
$replacement = @'
    public static (Vector3, Vector3) GetCameraWorldBounds()
    {
        if (MainCamera == null ||
            GameManager.instance == null ||
            GameManager.instance.mapWrapperCollider == null)
        {
            return (Vector3.zero, Vector3.zero);
        }

        Vector3 bottomLeftCorner = new Vector3(0f, 0f);
        Vector3 topRightCorner = new Vector3(1f, 1f);
        float dist = 1000f;

        _ray = MainCamera.ViewportPointToRay(bottomLeftCorner);
        Vector3 bottomLeft =
            GameManager.instance.mapWrapperCollider.Raycast(_ray, out _hit, dist)
                ? _hit.point : Vector3.zero;

        _ray = MainCamera.ViewportPointToRay(topRightCorner);
        Vector3 topRight =
            GameManager.instance.mapWrapperCollider.Raycast(_ray, out _hit, dist)
                ? _hit.point : Vector3.zero;

        return (bottomLeft, topRight);
    }

    public static Sprite LoadSpriteFromFile
'@

$updated = [regex]::Replace($text, $pattern, $replacement, 1)
if ($updated -eq $text) {
    throw "Nao consegui localizar GetCameraWorldBounds nesta versao de Utils.cs."
}

Set-Content -Path $path -Value $updated -Encoding UTF8 -NoNewline
Write-Host "NullFix v2 aplicado em Utils.cs."
Write-Host "Backup criado em Utils.cs.bak-nullfix-v2."
Write-Host "Abra o Unity, aguarde a compilacao e pressione Play."
