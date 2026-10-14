# Auditoria fiscal — lacuna de detalhe em relação ao padrão de AoE IV

**Data:** 2026-10-13  
**Projeto:** `RTS01-Corrigido`  
**Escopo:** somente leitura. Nenhum código, cena, prefab, FBX, Blend ou material foi alterado.

## Parecer executivo

**Resultado: NÃO APROVADO para produção AAA. Aprovado somente para prototipagem e validação de pipeline.**

O kit atual funciona como blockout modular e já pode ser importado no Unity, mas ainda não comprova o conjunto técnico necessário para uma construção comparável ao nível de detalhe de um RTS AAA como Age of Empires IV. A comparação abaixo usa critérios objetivos de produção; não copia assets protegidos, nomes, texturas ou modelos de terceiros.

## Evidências verificadas

Assets encontrados:

- `Assets/CityArchitecture/Generated/Buildings/House_Medieval.fbx`
- `Assets/CityArchitecture/Generated/Buildings/Barracks_Medieval.fbx`
- `Assets/CityArchitecture/Generated/Buildings/Temple_Imperial.fbx`
- `Assets/CityArchitecture/Generated/Buildings/Market_Classical.fbx`
- `Assets/CityArchitecture/Generated/PremiumFortificationKit.fbx`
- fontes `.blend` correspondentes para os kits gerados;
- prefabs individuais em `Assets/CityArchitecture/Prefabs/EraBuildings`;
- plano de LOD em `Docs/CityArchitecture/LOD_BUILD_PLAN.md`.

Também foi verificado que o Unity criou metadados para os FBX e que as auditorias existentes tratam o City Block como layout/prototipagem, não como cenário final.

## Matriz objetiva de comparação

| Critério | Estado atual observado | Critério de produção AAA/RTS | Parecer |
|---|---|---|---|
| **Malha e silhueta** | Construções geradas proceduralmente a partir de volumes simples; há paredes, torres e volumes de edifícios, mas não há evidência de modelagem hero detalhada, modularidade arquitetônica completa ou variações suficientes de fachada. | Silhueta identificável à distância, telhados e beirais com espessura, contrafortes, vãos, vigas, ameias, portas e elementos funcionais legíveis na câmera RTS. | **Gap alto** |
| **Topologia** | Não existe no relatório atual contagem consolidada por LOD, inspeção de normais, manifold, n-gons, interseções ou distribuição de arestas. | Geometria intencional, normais consistentes, sem faces degeneradas/interseções visíveis e orçamento de triângulos por família documentado. | **Não comprovado** |
| **UV artística** | O plano do bloco registra UV presente em objetos, mas não comprova atlas final, ausência de sobreposição indevida, empacotamento, cortes orientados à silhueta ou densidade uniforme. | UV0 0–1 organizada, texel density coerente entre pedra, madeira, metal e tecido, atlas compartilhado e ilhas sem sobreposição acidental. | **Gap alto** |
| **UV2/lightmap** | Não há evidência de UV2 dedicada e validada para os edifícios/fortificações. | UV2 separada, sem sobreposição, com margem compatível com a resolução do lightmap e teste de iluminação sem bleeding. | **Ausente/não comprovado** |
| **PBR** | Materiais e nomes existem no pipeline, mas os assets de cidade não apresentam conjunto comprovado de texturas base color, normal, roughness/smoothness, AO e variações de desgaste. | PBR completo, variação controlada de pedra/madeira/metal, microdesgaste, sujeira, bordas gastas e atlas para reduzir draw calls. | **Gap crítico** |
| **LOD** | Existe um gerador não destrutivo de oito FBX LOD em `Tools/CityArchitecture/GenerateBuildingLODKit.py`, mas o plano informa que o `LODGroup` no prefab ainda fica para etapa posterior. O kit de fortificação não tem LOD0/1/2 comprovados. | LOD0 hero, LOD1 aproximadamente 50–60% dos triângulos, LOD2 aproximadamente 15–35%, transições testadas na câmera ortográfica e popping controlado. | **Parcial; não integrado** |
| **Escala e pivôs** | O projeto define 1 unidade = 1 metro, personagem de referência de 1,8 m, porta de 2,2–2,6 m e módulo de muralha de 4 m. A documentação ainda exige confirmação visual FBX/Unity e comparação com humanoide. | Medidas congeladas, pivôs na base/encaixe, vãos compatíveis com humano, cavalo e máquinas de cerco, sem fator 0,01/100 no importador. | **Contrato definido; evidência incompleta** |
| **Colisão e navegação** | Os kits visuais de muralha não adicionam colliders; o City Block não demonstra colisão dedicada nem NavMesh final. Isso é seguro para protótipo, mas insuficiente para produção. | Colisores simples separados, portões com passagem real, NavMesh testado nos dois sentidos e sem detalhes decorativos bloqueando unidades. | **Não aprovado** |
| **Leitura RTS** | Há prévias e showcase, porém os relatórios registram que a leitura é de blockout/protótipo e que falta inspeção completa em quatro distâncias/ângulos. | Identidade imediata de casa, quartel, templo, mercado, torre e portão; leitura clara perto, em gameplay, longe e na vista aérea. | **Gap alto** |
| **Performance** | Não há orçamento consolidado de triângulos, materiais, submeshes, batches, memória ou FPS para uma cidade completa. | Instancing/atlas, LODGroups ativos, materiais compartilhados, teste com densidade urbana alvo e medição no player. | **Não comprovado** |

## Comparação de detalhe visual

O padrão de referência de um RTS AAA exige que o detalhe seja distribuído em três escalas:

1. **Macro:** silhueta, altura, telhado, torres, portões e relação com a muralha.
2. **Médio:** portas, janelas, vigas, escadas, ameias, contrafortes, bandeiras e props funcionais.
3. **Micro:** variação de pedra, juntas, desgaste, sujeira, metal, madeira, normal map e roughness.

O estado atual cobre principalmente a escala **macro** e parte da escala **média** por meio de volumes e decorações procedurais. A escala **micro** não está demonstrada nos assets auditados. Portanto, não é correto afirmar que a malha/UV atual está “igual” a AoE IV.

## Limitações da auditoria

- O add-on local de medição de texel density não estava disponível no Blender batch desta sessão; por isso não foi inventado um número de densidade. A falha do add-on não altera os assets.
- Não foi feita alteração de código para criar um auditor novo, conforme o escopo somente leitura.
- A comparação é contra critérios de produção e leitura visual de RTS; não é uma alegação de acesso ao conteúdo proprietário interno de Age of Empires IV.
- Arquivos existentes, incluindo fontes Blender e FBX, foram preservados.

## Ações necessárias antes de promover os assets

1. Criar malha de produção para cada família, com fachadas, telhados, vãos e módulos reutilizáveis.
2. Aplicar escala/rotação, recalcular normais e verificar manifold, degeneradas e interseções.
3. Criar UV0 de produção e UV2 de lightmap, com relatório de sobreposição e texel density.
4. Produzir atlas PBR compartilhado por família/era, com variação de pedra, madeira, metal e sujeira.
5. Gerar e integrar LODGroup LOD0/LOD1/LOD2 nos prefabs, com orçamento por peça.
6. Criar colisão separada e testar NavMesh, portões e passagem de unidades.
7. Registrar quatro capturas por asset: close, gameplay, longe e vista aérea.
8. Medir draw calls, triângulos, memória e FPS em uma cidade com a densidade alvo.

## Decisão fiscal

**Não substituir os prédios da produção nem declarar equivalência com AoE IV nesta rodada.** Os assets devem continuar isolados como protótipos até que UV0/UV2, PBR, LOD, escala, colisão, NavMesh e leitura RTS tenham evidências reproduzíveis.

