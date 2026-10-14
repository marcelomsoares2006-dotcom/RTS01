# Plano de integração do bloco 3D da cidade

**Escopo:** auditoria documental do asset `Assets/CityArchitecture/Generated/EraImperialCityBlock.fbx` e do arquivo-fonte `.blend`. Nenhum script, cena ou prefab foi alterado nesta etapa.

## 1. Auditoria do bloco recém-gerado

### Inventário observado

- 36 objetos de malha no Blender.
- 8 lotes de construção, 2 vias, 4 segmentos de muralha externa, 4 torres de canto, 8 torres de portão, 4 lintéis e 4 portas.
- Dimensão geral do terreno: aproximadamente **120 x 80 unidades**.
- Muralhas: 5 unidades de altura e 2 de espessura.
- Torres de canto: aproximadamente 6 x 6 x 7.
- Vias principais: eixo leste-oeste de 108 x 6 e eixo norte-sul de 6 x 68.
- Materiais encontrados: `City_GateWood`, `City_Lot`, `City_Road` e `City_Stone`.
- Todos os objetos possuem uma camada UV; as escalas de objeto observadas estão em 1,1,1.

### Qualidade e limites atuais

O asset é um **bloco de layout modular**, adequado para validar proporções urbanas, acessos e distribuição de lotes. Não deve ser tratado ainda como cenário AAA final: os componentes são predominantemente volumes simples, os lotes são marcadores de implantação e não há evidência, nesta auditoria, de atlas PBR, LODs, lightmap UV dedicado, colliders ou configuração de NavMesh.

Pontos positivos:

- escala coerente e facilmente mensurável;
- separação por objetos, permitindo substituir cada família de peça;
- UV presente em todos os objetos;
- vias e acessos já definidos como referências de circulação.

Pontos que exigem validação antes da produção final:

- confirmar escala FBX no importador Unity e a equivalência de 1 unidade para 1 metro;
- verificar normais, arestas abertas, sobreposições e interseções nos encontros de muralha/torre/portão;
- medir densidade texel entre pedra, madeira, estrada e lotes;
- criar UV2 separado para lightmaps, sem reutilizar a UV artística;
- substituir lotes e volumes de portão por peças de produção, mantendo os pivôs e envelopes atuais;
- produzir LOD0/LOD1/LOD2 para muralhas, torres e portões antes de montar uma cidade grande.

## 2. Estratégia de integração por eras

O bloco deve permanecer como **layout neutro**. A progressão visual deve trocar módulos por família, sem mudar a malha viária nem os pontos de acesso.

| Era | Direção visual | Módulos prioritários | Regra de compatibilidade |
|---|---|---|---|
| Ancient | terra compactada, madeira e pedra irregular | paliçada, portão leve, torre baixa, casas simples | manter o envelope dos lotes e a largura das vias |
| Classical | alvenaria regular, madeira reforçada e elementos cívicos | muralha de pedra, torres quadradas, portão com lintel | manter os quatro acessos e a altura livre da via |
| Medieval | muralha espessa, ameias, contrafortes e torres funcionais | muralha modular, portão fortificado, torre de vigia | preservar corredores de unidades e áreas de colocação |
| Imperial | pedra aparelhada, metal, telhados e ornamentação de elite | portão monumental, torres avançadas, edifícios administrativos | decoração não pode invadir o envelope de colisão |

Cada variante deve compartilhar:

1. pivô no mesmo ponto da peça-base;
2. dimensões externas compatíveis;
3. abertura de portão e faixa de passagem invariáveis;
4. sockets documentados para bandeiras, armas, escadas e efeitos;
5. nomes de material e convenção de LOD previsíveis.

## 3. Riscos de escala

- **FBX/Unity:** uma importação com fator 0,01 ou 100 distorce velocidade, alcance de ataque, câmera e altura das unidades. Validar uma torre e uma unidade humana lado a lado.
- **Altura de passagem:** lintéis e portas precisam ser comparados com a altura do cavalo, catapulta e balestra; não presumir que a abertura atual atende todas as unidades.
- **Lotes:** os lotes de 16 x 12 e 10 x 10 são marcadores, não contratos finais de footprint. Cada edifício deve declarar margem de fundação e área de serviço.
- **Câmera ortográfica:** a escala visual deve ser conferida em distância de jogo, pois detalhes pequenos podem ser inúteis e volumes grandes podem ocultar unidades.

## 4. Riscos de colisão

- O FBX auditado não demonstra colliders dedicados. Não adicionar MeshCollider diretamente ao bloco inteiro para gameplay.
- Separar colisão de navegação, bloqueio de projéteis e seleção visual; cada uma precisa de uma representação simples própria.
- Evitar colisores em terreno, estrada e lotes marcadores quando eles impedirem a colocação ou a movimentação.
- Portões precisam de estado aberto/fechado e de dois perfis de passagem; a decoração do lintel não deve fechar o corredor.
- Torres e muralhas devem usar colisores compostos simples, alinhados ao perímetro, em vez de malha visual detalhada.

## 5. Riscos de NavMesh

- O bloco não deve ser usado como superfície de navegação por padrão: o terreno e as estradas devem continuar sendo as superfícies caminháveis.
- Muralhas, torres, lotes e portões precisam ser classificados como obstáculos ou áreas não caminháveis conforme o estado da construção.
- A abertura dos quatro portões deve produzir conexão real entre interior e exterior; testar unidades humanas, cavalo e máquinas de cerco.
- Recalcular NavMesh somente depois de confirmar escala, colliders e estados de portão. Atualizações durante a construção devem ser controladas para não gerar custo por frame.
- Validar regiões estreitas nas esquinas, nos encontros com torres e nas entradas dos lotes; são os pontos mais prováveis de criar ilhas ou corredores sem passagem.
- Testar caminho nos dois sentidos, caminho com unidade grande e bloqueio temporário por edifício em construção.

## 6. Sequência recomendada de integração

1. Importar o FBX em uma área de teste e confirmar escala, eixos, pivôs e orientação.
2. Registrar medidas de referência e congelar os envelopes de muralha, torre, portão, estrada e lote.
3. Criar variantes visuais por era mantendo esses envelopes.
4. Preparar colisão simples e UV2 de lightmap como dados separados da malha visual.
5. Criar LODs e testar orçamento de triângulos em uma cidade completa.
6. Configurar NavMesh em uma cena de teste com quatro portões e tráfego nos dois sentidos.
7. Só depois transformar as peças aprovadas em prefabs e conectá-las à decoração/progressão de era.

## Critério de aprovação da próxima etapa

O bloco estará pronto para integração de produção quando passar por: escala confirmada, inspeção visual em quatro ângulos, validação de passagem de unidades, colisão sem bloqueio indevido, NavMesh com os quatro acessos funcionais, LODs sem popping perceptível e ausência de materiais magenta.
