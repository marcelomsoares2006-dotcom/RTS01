# Auditoria segura da arquitetura visual — 15/10/2026

## Escopo

Auditoria somente leitura de `Assets/CityArchitecture` e da documentação visual. Nenhum prefab jogável, contrato de gameplay, cena, malha ou material foi substituído.

## Resultado reproduzível

- `Tools/CityArchitecture/Validate-AAAVisualGate.ps1`: **PASS**.
- 44 FBX importados pelo auditor Blender sem alterar os arquivos.
- 0 escalas não unitárias.
- 0 dimensões nulas.
- 0 normais inválidas.
- 0 materiais com nomes de erro/rosa/magenta.
- 20 ocorrências de UV classificadas para revisão, sem normalização automática.
- `git diff --check`: **PASS**.

## Achados

### 1. UVs

As ocorrências já classificadas são coerentes com três casos que não devem ser corrigidos por lote:

- bordas numéricas mínimas no mercado;
- repetição provável de textura nas peças da balestra;
- revisão manual de `Bolt`, `Trigger` e `HeraldicKnight_LOD2`.

Mover ou reescalar essas ilhas sem conferir o material no Unity pode alterar tiling, texel density e aparência. Nenhuma edição foi aplicada.

### 2. Materiais e texturas

Os quatro materiais `Wall_*` são referenciados por prefabs de muralha, mas seus slots de textura permanecem com `fileID: 0`. Existem texturas `PBR_*`, porém não há correspondência verificável entre textura, canal e era que autorize ligá-las automaticamente. Fazer essa ligação mudaria a aparência dos prefabs existentes sem uma captura Unity de comparação.

**Decisão:** não alterar os materiais nesta rodada.

### 3. LOD e colisores

Os prefabs isolados de `CityArchitecture` não apresentam `LODGroup` ou colliders próprios no YAML. Adicionar esses componentes pode modificar placement, bloqueio de unidades e NavMesh; além disso, a confirmação correta exige abrir o Unity e medir a cena.

**Decisão:** não adicionar componentes nesta frente visual.

### 4. Qualidade artística

Os modelos importam corretamente, mas a auditoria anterior continua válida: o kit ainda não comprova nível AAA nem superioridade frente ao Age of Empires IV. Faltam evidências equivalentes de close, gameplay, longe e vista aérea; também faltam dados de cidade densa e PBR efetivamente aplicado.

## Alterações aplicadas

Nenhuma alteração em assets, prefabs, scripts ou gameplay. Este arquivo é o único resultado novo desta auditoria.

## Próxima ação segura

Abrir uma cena visual isolada no Unity, capturar os quatro enquadramentos e conferir manualmente `Bolt`, `Trigger`, `HeraldicKnight_LOD2`, textura por canal e comportamento de LOD. Só depois disso considerar uma mudança pontual e reversível.

## Evidências

- `Docs/AAA/VisualAudit/visual-assets-audit.json`
- `Docs/AAA/VisualAudit/uv-review-2026-10-14.json`
- `Docs/AAA/VisualAudit/2026-10-14-visual-audit.md`
- `Docs/CityArchitecture/Audit/2026-10-14-AoE4-Comparison-and-Fix.md`
- `TestResults/EraBuildingKitDetailedPreview_Framed.png`
