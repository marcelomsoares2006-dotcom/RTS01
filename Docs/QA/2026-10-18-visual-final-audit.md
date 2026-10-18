# Auditoria visual final — construções

**Data:** 2026-10-18  
**Projeto:** `RTS01-Corrigido`  
**Modo:** somente leitura; nenhum prefab, cena, FBX, material ou configuração de produção foi alterado/promovido.

## Resultado executivo

**Status: BLOCKED / não aprovado para paridade visual com Age of Empires IV.**

Há evidência suficiente de que os FBX arquitetônicos LOD0/LOD1/LOD2 importam e que a comparação geométrica offline não encontra deslocamento de fragmentos nos LODs de produção. Porém os LODs não estão ligados aos prefabs de runtime, a cobertura PBR é desigual, há UVs fora do tile no Market e não foi possível obter captura visual dentro do Unity nesta sessão. Isso impede afirmar ausência de magenta, popping, seams ou leitura material em runtime.

## Evidência executada/inspecionada

- `git status --short`: preservadas todas as alterações pré-existentes; apenas este relatório foi criado.
- Processo Unity encontrado (`Unity 6000.6.3f1`), mas sem janela acessível pelo inventário de UI; não houve captura nem interação no editor.
- Evidência offline já versionada em `Docs/AAA/VisualAudit/visual-assets-audit.json`: 44 FBX importados, status `REVIEW`, sem palavras-chave de material rosa/magenta/error e sem dimensões nulas.
- Evidência UV já versionada em `Docs/AAA/VisualAudit/uv-review-2026-10-14.md` e `.json`: 20 ocorrências fora de 0–1; nenhuma correção automática.
- Evidência LOD já versionada em `Docs/AAA/VisualAudit/2026-10-17-uv-lod-audit.json`: comparação dos LODs de produção com `displaced_fragments.production = 0`; candidato isolado também passou, mas não foi promovido.
- Fonte dos critérios: `Tools/CityArchitecture/AuditAAAVisualAssets.py`, `Tools/CityArchitecture/AuditBuildingLODs.py` e `Assets/Editor/CityArchitecture/BuildingMeshAudit.cs`. Os scripts que sobrescrevem relatórios existentes não foram executados nesta rodada.

## PBR e materiais

### Estado observado

- `Showcase_Stone`, `Showcase_Plaster`, `Showcase_Wood`, `Showcase_Roof` e `Showcase_Trim` usam shader Built-in/Standard (`fileID 46`) e referenciam albedo, normal, metallic/gloss e occlusion; são a melhor evidência de um conjunto PBR funcional.
- `Wall_Ancient`, `Wall_Classical`, `Wall_Imperial` e `Wall_Medieval` referenciam apenas `_MainTex`; `_BumpMap`, `_MetallicGlossMap` e `_OcclusionMap` estão vazios. Para uma comparação com AoE IV, isso é material de albedo com resposta PBR incompleta, não material final de produção.
- Não foi observada evidência textual de material nomeado `pink`, `magenta` ou `error`; isso **não substitui** inspeção em viewport/runtime.

### Bloqueios objetivos

1. Cobertura PBR inconsistente entre Showcase e materiais `Wall_*`; falta normal/roughness/metallic/AO verificável nos materiais de parede.
2. Captura Unity indisponível nesta sessão; magenta, shader fallback, escala de roughness e resposta à iluminação continuam sem confirmação visual.
3. Não há critério de aceitação documentado para consistência de texel density, roughness e variação de materiais por era comparável ao acabamento de AoE IV.

## Magenta / shaders

- **Sinal estático:** nenhum nome suspeito nos 44 FBX auditados e nenhuma ocorrência relevante de `pink`, `magenta` ou `shader error` nos logs consultados.
- **Estado de aprovação:** **não aprovado**; a ausência de nome suspeito não prova que um shader incompatível não fique magenta após importação/renderização no Unity 6.
- **Bloqueio:** falta captura da cena/preview de construção no Unity com iluminação e materiais realmente carregados.

## UVs

- O inventário offline registra **20 ocorrências** de UV0 fora de 0–1.
- O caso arquitetônico diretamente relevante é `Market_Classical`: 4 objetos no LOD0 e 4 correspondentes no LOD1 (`MarketPost*`), classificados como borda de precisão com overrun máximo aproximado de `7e-08`. O relatório anterior recomenda manter sem normalização automática, mas exige validação no material Unity.
- As demais ocorrências são principalmente Crossbow e HeraldicKnight; não foram tratadas como defeitos de construção nesta auditoria.
- **Bloqueio:** o Market ainda precisa de validação visual de repetição/seam no material e de correspondência de amostragem entre LOD0 e LOD1; não corrigir automaticamente sem prova de erro de exportação.

## LODs e construções

- Existem 4 FBX LOD0 em `Assets/CityArchitecture/Generated/Buildings` e 8 FBX LOD1/LOD2 em `Assets/CityArchitecture/Generated/LOD` para House, Barracks, Market e Temple.
- A auditoria offline reporta `displaced_fragments.production = 0`; os centros/pivôs dos fragmentos de produção estão alinhados dentro dos limiares do script (`1 cm` para centro e `0,01 mm` para pivô).
- **Falha de integração:** os 9 prefabs em `Assets/CityArchitecture/Prefabs` não serializam `LODGroup`, incluindo `EraBuildings/*`, `Gatehouse` e peças de muralha. Os LODs exportados não demonstram seleção automática por distância no runtime.
- Os quatro prefabs de edifício (`House_Medieval`, `Barracks_Medieval`, `Market_Classical`, `Temple_Imperial`) também não têm collider serializado no inventário offline. Isso bloqueia a equivalência de interação/placement e a validação de leitura em escala de jogo.
- **Bloqueios comparáveis a AoE IV:** ausência de LODGroup runtime, ausência de collider de construção e falta de captura de transição LOD com câmera afastando/aproximando. Sem isso não é possível excluir popping, overdraw e custo excessivo em cidades densas.

## Comparação de referência

O padrão comparativo usado foi: leitura material consistente em distância próxima, fallback sem magenta, UVs sem seams visíveis, transição LOD imperceptível e construção interagível/selecionável. O estado atual falha objetivamente nos gates de integração de LOD/collider e no gate PBR dos materiais `Wall_*`; os gates visuais de magenta/seams permanecem inconclusivos por falta de captura Unity.

## Próximos gates, sem executar nesta auditoria

1. Abrir o projeto em uma instância Unity com janela acessível e capturar as quatro construções em perto/médio/longe, incluindo Market.
2. Verificar shader/material efetivo, magenta, roughness, normal, AO, seams UV e transição LOD na mesma câmera.
3. Integrar e validar `LODGroup` e colliders somente após decisão explícita de implementação; não promover FBX isolado como solução.
4. Repetir a auditoria e registrar capturas antes de qualquer aprovação visual final.
