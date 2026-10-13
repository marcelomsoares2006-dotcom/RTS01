# Auditoria de montaria, IK e LOD

Data da auditoria: 2026-10-12  
Escopo: leitura somente de `Assets/CharacterRigging`, `Docs/CharacterMeshAudit` e cenas relacionadas. Nenhum runtime compartilhado, prefab de produção ou cena de jogo foi alterado.

## Estado encontrado

| Frente | Evidência | Estado |
|---|---|---|
| Cavalo/rig | `Assets/CharacterRigging/Stage3/Horse/Models/Horse.fbx`, `Prototypes/Horse_SkinnedPrototype.fbx` e relatório Stage3 | Base de malha/rig existe; falta validar como unidade jogável de produção. |
| Montaria | `Stage5/MountedFitPrototype.prefab` e `mounted-prototype.json` | Protótipo visual com rider, cavalo e sockets. O próprio relatório diz que não há socket de sela compartilhado, locomoção ou ataque montado. |
| Sockets | `MountedSockets` referencia sela, rider, pés, mãos e arma | Estrutura segura para continuar; ainda não é contrato de produção nem está ligada ao catálogo de unidades. |
| IK | `MountedIKController.cs` existe e o prefab contém o componente | Implementação apenas de `OnAnimatorIK`; depende de Avatar Humanoid, IK Pass e targets válidos. Não existe teste automatizado específico de alcance/erro dos quatro alvos. |
| Locomoção | `MountedMovementDriver.cs` e `MountedAnimatorSync.cs` existem | O prefab mantém `NavMeshAgent` e driver desativados no protótipo. O relatório Stage5 demonstra deslocamento do protótipo, mas não uma unidade integrada em Map1/Map2. |
| Animação | `MountedIdle.controller` contém somente estado `MountedIdle`, sem parâmetros | Idle sentado validado; não há clips/estados verificáveis de walk, run, ataque montado, hit ou desmontagem. |
| LOD | Há imagens Stage4 do Horse LOD1/LOD2, mas não há LOD de cavalo dentro do prefab montado nem teste de seleção por distância | Bloqueado para aceite. É necessário comprovar LOD0/1/2 decrescentes e seleção em zoom/distância. |
| NavMesh | `MountedReview.unity` tem `m_NavMeshData: {fileID: 0}` | A cena de revisão não é uma prova reproduzível de NavMesh; o cenário NavMesh separado precisa ser refeito/validado no Unity. |

## Bloqueios concretos

1. Não existe uma unidade de cavalaria de produção em `Assets/Resources/Prefabs/Units/Characters`; só o protótipo Stage5.
2. O controlador de montaria não tem estados de locomoção/ataque e o `MountedAnimatorSync` procura parâmetros que o controlador atual não declara.
3. O `MountedIKController` não está presente no resultado textual do prefab como componente no objeto raiz; o script existe, mas a ligação precisa ser confirmada no Inspector/AssetDatabase.
4. O LOD do cavalo foi documentado visualmente, mas não está demonstrado no prefab montado nem por teste de distância.
5. Não há contrato para desmontar/morrer, colisão/agente único cavalo+rider ou integração com `CharacterData`/`UnitManager`.
6. Os testes Stage5 disponíveis são evidências de protótipo: não cobrem ataque montado, IK quantitativo, seleção de LOD ou partida real.

## Próximos passos verificáveis

1. Criar um prefab experimental isolado `MountedCavalry` fora de `Resources`, com `MountedIKController` ligado e validação dos quatro targets; critério: erro de pé/mão menor que 0,05 m em Idle/Walk/Run.
2. Preparar controlador com parâmetros `Speed`, `IsMoving`, `IsAttacking` e `AttackIndex`, clips MountedIdle/MountedWalk/MountedRun/MountedAttack; critério: cada transição observada no PlayMode.
3. Adicionar LODGroup ao cavalo/montaria e medir triângulos LOD0 > LOD1 > LOD2; critério: LOD esperado em três distâncias e zero material magenta.
4. Criar teste PlayMode isolado para NavMesh, IK, sincronização rider/cavalo e ataque; não promover ao catálogo antes de passar.
5. Só depois integrar a unidade em uma cena de mapa e criar `CharacterData`, mantendo fallback para unidade desmontada.
6. Medir FPS com 1/10/50 montarias e registrar resultado antes do aceite.

## Ferramenta criada

`Tools/CharacterRigging/AuditMountedReadOnly.ps1` apenas enumera arquivos, hashes e sinais textuais do protótipo. Ela não abre o Unity, não importa assets, não escreve em `Assets` e grava somente `scan.json` dentro desta pasta de auditoria quando executada.

Execução:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CharacterRigging\AuditMountedReadOnly.ps1
```

