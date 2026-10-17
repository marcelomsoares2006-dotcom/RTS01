# Retry da revisão PBR no Unity — 2026-10-17

## Resultado

**BLOCKED por infraestrutura; nenhum asset de produção foi alterado.**

O Unity 6000.6.3f1 foi iniciado sem outra instância do projeto e com
Direct3D 11. O processo parou antes do método
`HousePbrPilotAuthoring.PrepareAndCapture` porque o canal
`LicenseClient-User` foi recusado durante a inicialização do Licensing Client.

## Segurança

- `House.prefab` permanece intacto.
- Materiais e mapas `HousePilotPBR` continuam candidatos isolados.
- `HousePbrPilotAuthoring.Promote` não foi executado.
- Log: `TestResults/house-pbr-pilot-unity.log`.

## Próximo gate

Reexecutar somente com o Unity Licensing Client respondendo; revisar
`baseline.png` e `candidate.png` renderizados pelo Unity, além de shader,
mapas, escala, UV e ausência de rosa no Inspector/Game View. A captura offline
não substitui essa revisão.
