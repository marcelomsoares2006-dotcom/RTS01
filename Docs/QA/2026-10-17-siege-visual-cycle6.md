# Ciclo 6 — Cerco físico/visual isolado

Data: 2026-10-17  
Escopo: protótipo auditável de projéteis, colliders, alvo estrutural, dano por impacto/área, destruição e feedback visual.

## Resultado desta execução

- Protótipo criado em `Assets/Editor/FinalIntegration/SiegeVisualCycle6Harness.cs`.
- Alterações em prefabs, cenas e assets de produção: **nenhuma**.
- Commit/push: **não realizados**, conforme solicitado.
- Auditoria estrutural do arquivo, tags/layers e contratos por reflexão: **PASS** (`63/63` chaves balanceadas; fixtures e contratos presentes).
- Gate global `Tools/Validate-EditorFree.ps1 -SkipReleasePreflight`: **PASS** (Unity 6000.6.3f1, 5 cenas, 43 contratos de construção, 11 entradas jogáveis, 1.669 assets indexados).
- Execução Unity/PlayMode real: **BLOCKED** pelo canal `LicenseClient-User`; tentativa feita com Unity 6000.6.3f1 em `-batchmode -nographics -executeMethod SiegeVisualCycle6Harness.Run`, saída 1 antes da compilação do método. Log: `C:\Users\User\AppData\Local\Temp\EraImperial-SiegeVisualCycle6.log`.
- Promoção para produção: **BLOCKED** até revisão visual dentro do Unity.

## O protótipo cobre

- trajetória balística determinística com gravidade, avanço por passos e impacto único;
- fixture de collider, layer 12 (`UNIT_MASK`) e tag `Unit`;
- alvo estrutural com HP, dano parcial, destruição e desativação do collider;
- dano direto e dano radial com falloff linear;
- proteção de aliados sem `friendlyFire`;
- eventos mínimos de spawn, impacto direto, dano estrutural e destruição;
- reflexão dos contratos `SiegeAttackProfile`, `CatapultCombatController`, `BuildingManager`, `Building`, `ProjectileContract` e dos campos de `Globals`;
- fixtures de `Unit`/`Terrain` para execução headless quando o TagManager não estiver disponível.

## Limitações honestas

Este ciclo não afirma que a física foi executada com sucesso em uma sessão Unity nesta máquina. O PlayMode, quando iniciado pelo menu, cria apenas `GameObject`s temporários em uma cena vazia e os destrói ao terminar; não instancia prefab de produção, não altera `BuildingManager` ou `Building` e não salva cena. A validação visual de partículas, material, animação, câmera, crateras, fragmentos e LOD continua pendente. A validação de produção só pode ser feita após o Unity abrir sem o bloqueio do `LicenseClient-User`.

## Próximo gate

Executar o método `SiegeVisualCycle6Harness.RunStaticAudit` no Unity recuperado; depois criar uma cena temporária de teste, instanciar os objetos físicos e revisar a captura visual. Somente após essa auditoria promover qualquer implementação para os assets de produção.
