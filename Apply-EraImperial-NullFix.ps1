$ErrorActionPreference = "Stop"

$root = Get-Location
$path = Join-Path $root "Assets\Scripts\Tools\Utils.cs"

if (!(Test-Path $path)) {
    throw "Não encontrei Assets\Scripts\Tools\Utils.cs. Execute este arquivo na pasta que contém Assets."
}

Copy-Item $path "$path.bak-nullfix" -Force
$text = Get-Content $path -Raw

$old = @'
    public static (Vector3, Vector3) GetCameraWorldBounds()
    {
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
'@

$new = @'
    public static (Vector3, Vector3) GetCameraWorldBounds()
    {
        // A câmera e o GameManager podem ainda não existir durante a inicialização.
        // Retornar limites vazios evita NullReferenceException no Minimap/CameraManager.
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
'@

if (!$text.Contains($old)) {
    throw "O método GetCameraWorldBounds não foi encontrado exatamente nesta versão do projeto."
}

$text = $text.Replace($old, $new)
Set-Content -Path $path -Value $text -Encoding UTF8 -NoNewline

Write-Host "Correção aplicada em Assets\Scripts\Tools\Utils.cs"
Write-Host "Backup criado em Assets\Scripts\Tools\Utils.cs.bak-nullfix"
Write-Host "Abra o Unity, aguarde a compilação e pressione Play novamente."
