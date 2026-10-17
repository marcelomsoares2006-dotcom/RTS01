# Auditoria PBR de produção — piloto House — 17/10/2026

## Veredito

**PILOTO PREPARADO, PROMOÇÃO BLOQUEADA.** O escopo ficou restrito ao `House` de produção. Foram criados mapas e materiais candidatos PBR dedicados, com backup do estado anterior e comparação visual offline. O `House.prefab` **não foi alterado** porque o gate de importação/captura Unity não pôde ser executado enquanto outra instância do projeto estava aberta e o Licensing Client recusou a conexão.

Não foi feita nenhuma afirmação de superioridade visual sobre Age of Empires IV. A imagem abaixo é uma comparação do próprio asset, não uma comparação AoE4:

- [comparação offline baseline × candidato](../../../TestResults/HousePBRPilot/comparison-offline.png)

## Escopo e proteção

- Piloto: `Assets/Resources/Prefabs/Units/Buildings/House.prefab`.
- Fora do escopo: Town Center, demais prefabs, FBX de produção, LODs, UVs da malha, gameplay, colliders e NavMesh.
- Backup: `TestResults/Backups/HousePBRPilot-2026-10-17-before/`.
- Hash SHA-256 do backup do prefab: `1142952C21519702206BD29B04C1A142C052555A88B8CE1122F5191DE04263DF`.
- Hash atual do `House.prefab` confere com o backup: `HOUSE_PREFAB_HASH_EQUAL=True`.
- Não há diff em `Assets/**/*.prefab`; nenhum outro prefab foi tocado.

O worktree já possuía alterações de outras frentes antes deste piloto. Elas foram preservadas. O `git diff --check` reproduziu uma linha de whitespace pré-existente em `Docs/Runtime/2026-10-14-runtime-standalone-audit.md`; ela não pertence a esta frente e não foi corrigida.

## Auditoria do asset e UV

Leitura somente da malha real `Assets/Resources/Imports/Units/Buildings/house.fbx` no Blender 5.2.1:

| Item | Resultado |
|---|---|
| Vértices / triângulos | 232 / 380 |
| Dimensões no FBX | 9,3092 × 6,7724 × 5,1181 |
| Escala de importação Unity observada | `globalScale: 0.4` no `.fbx.meta`; aproximadamente 3,7237 × 2,7089 × 2,0472 |
| UV0 | 232/232 coordenadas, uma camada `UVMap` |
| Faixa UV0 global | U `0,0974..0,6999`; V `-0,4529..1,3485` |
| Interpretação | repetição/ilhas fora de 0–1 existentes; não normalizadas neste piloto |

Densidade relativa estimada a 512 px, antes da escala de material: slot 0 `WoodPlanks` 79,65 px/unidade; slot 1 `Black` 93,63; slot 2 `PlayerColor` 65,10; slot 3 `WoodFloor` 65,38. O candidato usa tiling `1,0` para madeira e telhado e `1,35` para piso, sem editar a UV0. A diferença ainda precisa ser confirmada no importador Unity antes de qualquer promoção.

## Mapas e shader

Foram gerados oito mapas próprios, determinísticos, em `Assets/Resources/Textures/Buildings/HousePilotPBR/`, todos verificados como 512×512:

- `HousePilot_Wood_Albedo.png`, `Normal.png`, `MetallicGloss.png`, `AO.png`.
- `HousePilot_Roof_Albedo.png`, `Normal.png`, `MetallicGloss.png`, `AO.png`.

Configuração pretendida:

- `_MainTex`: albedo sRGB.
- `_BumpMap`: normal linear, `textureType: NormalMap`, escala de relevo moderada.
- `_MetallicGlossMap`: R para metallic e A para smoothness.
- `_OcclusionMap`: dado linear, força abaixo de 1.
- Shader: Built-in `Standard`, compatível com o pipeline atual; sem shader novo e sem alteração global de render pipeline.
- Wrap: Repeat, coerente com a UV0 repetida do modelo.

Materiais candidatos em `Assets/Resources/Materials/Building/HousePilotPBR/`:

| Slot original | Material candidato | Tiling | Decisão |
|---|---|---:|---|
| `WoodPlanks` | `HousePilot_Wood` | 1,00 | promover somente após gate Unity |
| `Black` | `HousePilot_Roof` | 1,00 | promover somente após gate Unity |
| `PlayerColor` | material original | — | preservado para não quebrar ownership |
| `WoodFloor` | `HousePilot_WoodFloor` | 1,35 | promover somente após gate Unity |

## Evidência visual

`Tools/CityArchitecture/RenderHousePilotPBRComparison.py` renderizou a malha real em Blender com luz/câmera compartilhadas: baseline à esquerda e candidato PBR à direita. O candidato mantém a silhueta e melhora a leitura de madeira/telhado com variação de albedo, normal e resposta de roughness; não foram observadas áreas magenta ou alterações geométricas na captura offline.

Essa evidência **não substitui** a captura Unity de produção. Os arquivos esperados para o gate pendente são:

- `TestResults/HousePBRPilot/baseline.png`
- `TestResults/HousePBRPilot/candidate.png`
- `TestResults/HousePBRPilot/capture.log`

## Gates

| Gate | Resultado | Evidência / bloqueio |
|---|---|---|
| Backup e reversibilidade | PASS | cópia explícita; hash do prefab atual igual ao backup |
| Escopo/diff | PASS | nenhum prefab alterado; somente piloto e ferramentas novas |
| Cobertura de mapas | PASS offline | 8 mapas dedicados, 512×512, metadados de importação preparados |
| Shader/material slots | PASS por revisão estática | Standard + mapas separados; `PlayerColor` preservado |
| UV/escala | REVIEW | UV0 original repetida; tiling candidato definido, sem correção destrutiva |
| Screenshot Unity | **BLOCKED** | batch abortado: outra instância abriu o projeto; Licensing Client recusou canal `LicenseClient-User` |
| Promoção para produção | **NÃO PROMOVER** | gate Unity não passou; `House.prefab` continua intacto |

## Reexecução segura

Depois de fechar manualmente o Unity Editor que mantém o projeto aberto, executar a preparação/captura sem `-executeMethod ...Promote`:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido' `
  -executeMethod HousePbrPilotAuthoring.PrepareAndCapture `
  -logFile 'TestResults/house-pbr-pilot-unity.log'
```

Revisar as duas PNGs Unity, checar shader/mapas/escala no Inspector e confirmar ausência de diff em qualquer prefab fora do piloto. Só se todos os gates passarem, executar `HousePbrPilotAuthoring.Promote` e repetir a auditoria do prefab. Nenhum LOD ou outro edifício deve ser promovido automaticamente.

## Arquivos desta frente

- `Assets/Editor/CityArchitecture/HousePbrPilotAuthoring.cs` — preparação, captura isolada e promoção explicitamente separadas.
- `Tools/CityArchitecture/GenerateHousePilotPBRMaps.py` — geração dos mapas do piloto.
- `Tools/CityArchitecture/RenderHousePilotPBRComparison.py` — comparação offline.
- `Assets/Resources/Textures/Buildings/HousePilotPBR/` — mapas candidatos.
- `Assets/Resources/Materials/Building/HousePilotPBR/` — materiais candidatos não promovidos.
- `TestResults/HousePBRPilot/comparison-offline.png` — evidência offline.
