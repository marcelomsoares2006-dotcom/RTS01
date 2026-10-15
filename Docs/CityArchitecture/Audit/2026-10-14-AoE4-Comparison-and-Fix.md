# Comparação visual com Age of Empires IV — 2026-10-14

## Veredito

As construções atuais **não estão comprovadamente melhores que as de Age of Empires IV**. O projeto já tem uma base funcional de RTS, variação por era, kit modular de muralhas e materiais de autoria, mas a captura de partida ainda fica abaixo da referência em detalhe de média/curta distância, riqueza PBR, props funcionais e composição de cidade.

Esta conclusão é deliberadamente conservadora: a referência de AoE IV foi usada para comparar linguagem visual e acabamento, não para copiar assets.

## Diferenças observadas

| Critério | Projeto atual | Referência AoE IV | Ação |
|---|---|---|---|
| Silhueta macro | legível, porém genérica em parte do catálogo | silhuetas fortes por função e era | manter catálogo, priorizar Town Center, quartéis e muralhas |
| Materiais | materiais planos e majoritariamente sem mapas PBR | madeira, pedra e metal com variação e desgaste | preparar atlas PBR e variações por era |
| Detalhe médio | kit CityArchitecture ainda é blockout/protótipo | beirais, contrafortes, portões, ameias e props funcionais | promover peças hero para os nove prédios |
| Distância RTS | leitura suficiente no smoke | leitura consistente em vários níveis de zoom | validar LOD, UV2, iluminação e captura perto/longe |
| Cidade | cenário de teste funcional | composição com ruas, defesa, economia e densidade | montar um showcase de cidade após os prefabs hero |

## Correções executadas nesta rodada

- `Building._SetAlive()` restaura explicitamente os materiais autorais quando a construção chega ao HP máximo.
- `BuildingFeedbackEmitter` agora restaura os materiais autorais ao entrar no estado operacional, protegendo contra eventos de spawn/load fora de ordem que deixem o material vermelho de colocação aplicado.
- `Tools/Validate-Project.ps1` deixou de interpretar a ausência de um processo Unity como falha quando o fallback de inspeção de processos é acionado.

## Evidências e limite atual

- O smoke anterior registrou `magenta pixels=0,000%`, portanto o vermelho observado não é shader magenta; é feedback/material de estado de construção ou colocação.
- O smoke e o build funcionais permanecem evidências válidas anteriores, mas a nova execução automatizada ficou bloqueada no inicializador de licenciamento do Unity após 300 segundos; é necessário repetir com o Unity Hub/licença estabilizados.
- O kit detalhado atual continua classificado como protótipo em `Docs/CityArchitecture/Audit/2026-10-13-AoE4-DetailGapAudit.md`; não deve ser anunciado como AAA final.

## Próximo marco visual

1. Reexecutar PlayMode e confirmar visualmente que a construção operacional não conserva material de colocação.
2. Promover Town Center, Barracks, Archery Range, Stable, Siege Workshop e Tower a prefabs hero com PBR/LOD/UV2 auditados.
3. Completar muralhas, portões, ruas e props de cidade num showcase dedicado.
4. Só então comparar novamente com AoE IV em capturas equivalentes e medir desempenho.
