# Auditoria LOD de produção — causa dos 55 fragmentos

## Escopo

Esta rodada investigou exclusivamente o deslocamento de fragmentos dos LODs
arquitetônicos. Não houve promoção, cópia ou alteração em
`Assets/CityArchitecture/Generated/Buildings`,
`Assets/CityArchitecture/Generated/LOD`, prefabs ou cenas.

## Contagem antes/depois

| Conjunto auditado | Antes | Depois da correção do gerador/auditor | Situação |
|---|---:|---:|---|
| FBX LODs de produção | 23 | 23 | bloqueado; não promovido |
| Round4 isolado | 0 | 0 | aprovado tecnicamente isolado |
| Cópia isolada legada `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedTest` | 32 | 32 | falso positivo de espaço-raiz; não comparável |
| Total que aparecia como “55” | 55 | 23 + 0 + 32 separados | corrigido no relatório |
| Round6 gerado pelo gerador corrigido | — | 0 | candidato isolado aprovado |

O valor 55 misturava os 23 deslocamentos reais de produção com 32 diferenças
de enquadramento da cópia legada. O relatório agora separa `production`,
`candidate` e `legacy_isolated` e marca a cópia legada como não comparável.

## Causa encontrada

`GenerateBuildingLODKit.py` calculava um centro global do kit, que contém
quatro edifícios afastados entre si, e subtraía esse centro das vértices de
cada malha. Isso confundia a origem do edifício com a origem da cena. Além
disso, o Decimate pode alterar o centro dos bounds de forma assimétrica.

A correção mantém `location`, rotação e escala do LOD0 e, depois do Decimate,
aplica apenas a diferença do centro dos bounds nas vértices em espaço local.
Assim o pivô não se move e a geometria permanece alinhada ao LOD0.

## Evidência executada

- Round4 reauditado: `production=23`, `round4=0`, `legacy_isolated=32`.
- Round6 gerado sem escrever em `Assets`: `production=23`, `round6=0`,
  `legacy_isolated=32`.
- Auditoria: `Tools/CityArchitecture/AuditBuildingLODs.py`.
- Gerador corrigido: `Tools/CityArchitecture/GenerateBuildingLODKit.py`.
- Candidato externo: `TestResults/LOD-Generation-Round6/`.
- Relatório detalhado: `Docs/AAA/VisualAudit/2026-10-17-uv-lod-audit.json`.

## Decisão

Os 23 deslocamentos nos FBX de produção continuam bloqueando a promoção.
O Round6 é apenas candidato externo e ainda não deve ser copiado para
produção; a promoção exige revisão visual Unity posterior e permanece fora do
escopo desta correção.
