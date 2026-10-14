# Especificação de assets de construções

**Projeto:** RTS01 / Era Imperial  
**Escopo:** auditoria visual e especificação de produção de construções  
**Data da auditoria:** 2026-10-14  
**Modo:** somente leitura; este documento não altera scripts, prefabs ou assets existentes.

## 1. Estado observado

O projeto já possui dois tipos de construção registrados:

| Tipo | Dados | Prefab | Malhas de construção | Collider de colocação | NavMesh obstacle |
|---|---|---|---:|---|---|
| House | `Assets/Resources/ScriptableObjects/Units/Buildings/House.asset` | `Assets/Resources/Prefabs/Units/Buildings/House.prefab` | 3 | Box, trigger, 4 x 2 x 4, centro Y=1 | Box, extents 2 x 1 x 2, carve |
| Tower | `Assets/Resources/ScriptableObjects/Units/Buildings/Tower.asset` | `Assets/Resources/Prefabs/Units/Buildings/Tower.prefab` | 3 | Box, trigger, 1 x 5 x 1, centro Y=2.5 | Box, extents 0.5 x 2.5 x 0.5, carve |

As seis malhas são os FBX em `Assets/Resources/Imports/Units/Buildings/`:

- `house-construction_00.fbx`
- `house-construction_01.fbx`
- `house.fbx`
- `tower-construction_00.fbx`
- `tower-construction_01.fbx`
- `tower.fbx`

O `BuildingData` atual expõe `constructionMeshes`, `maxConstructors`, `minimumSupportedCorners`, `terrainProbeDistance` e `terrainProbeStartOffset`. O fluxo existente escolhe uma das malhas conforme a razão de construção e troca materiais durante a prévia de colocação. Não há, no conjunto auditado, malha de ruína, estágio explícito de dano, LODGroup, sockets nomeados ou biblioteca de materiais por era/civilização declarada no asset de construção.

## 2. Princípios de produção

As construções devem seguir uma escala única e verificável. A unidade de trabalho do Unity é **1 unidade = 1 metro**. O plano do mapa é XZ, o eixo Y é vertical, e a origem do prefab representa o ponto de implantação no terreno.

Toda construção deve ser legível na câmera RTS, funcionar em zoom próximo sem artefatos e manter silhueta reconhecível em zoom distante. A qualidade alvo é superior ao protótipo atual e deve priorizar leitura tática, coerência histórica/visual, bom desempenho e capacidade de crescer para várias civilizações e eras.

## 3. Padrão de escala, origem e orientação

### Requisitos obrigatórios

- Escala do root do prefab: `(1, 1, 1)`; escala aplicada no arquivo de origem antes da exportação.
- Rotação do root: `(0, 0, 0)`; frente lógica da construção em `+Z`.
- Origem do root: centro do footprint no plano XZ e Y no nível do terreno, sem offset oculto.
- Base visual e collider devem tocar o mesmo plano Y=0; não deixar fundação flutuando nem enterrada por padrão.
- Todas as dimensões devem ser documentadas em metros e aprovadas com uma unidade humana de referência (trabalhador de aproximadamente 1,7–1,8 m).
- A rotação de gameplay deve ocorrer no root do prefab, nunca compensando pivô dentro da malha.

### Referências atuais

House e Tower já usam root com escala `(1,1,1)`, rotação zero e posição local zero. O `House` tem uma seleção visual em Y=0,2 e o `Tower` também; esse offset é aceitável para o decal, mas não deve ser usado como referência da base da construção. Os footprints observados são 4 x 4 m e 1 x 1 m, respectivamente, e devem ser confirmados visualmente no Unity antes de se tornarem padrão definitivo.

### Tolerâncias de aprovação

- Escala do root: erro máximo de 0,001.
- Centro XZ do footprint contra o root: erro máximo de 0,02 m.
- Base contra Y=0: erro máximo de 0,02 m em terreno plano.
- Nenhuma parte estrutural deve atravessar o terreno mais de 0,05 m, salvo fundação deliberada.

## 4. Footprint e colliders

Cada prédio deve ter uma camada de colisão documentada para três finalidades distintas:

1. **Placement footprint:** volume simples, trigger, usado para impedir sobreposição e validar terreno.
2. **Gameplay blocker:** volume que alimenta o obstáculo de navegação quando a obra está concluída.
3. **Damage/interaction volumes:** volumes específicos para ataques, guarnição, portas, entradas e pontos de coleta.

O footprint não deve ser inferido da malha renderizada. Ele deve ser estável entre os estados de construção e permitir uma folga de acesso para trabalhadores e unidades. Para prédios regulares, BoxCollider é preferível; prédios com pátios, torres anexas ou formatos em L devem usar volumes compostos simples. MeshCollider concavo não deve ser usado em massa em partidas RTS.

O collider de colocação atual de House e Tower é trigger e usa Rigidbody cinemático, o que é compatível com a validação atual por `OnTriggerEnter/Exit`. Antes de expandir o catálogo, validar que o volume cobre toda a projeção da fundação, que não bloqueia o próprio terreno e que os cantos usados pela sondagem de terreno estão alinhados com o footprint.

## 5. Organização de prefab recomendada

Cada prefab final deve seguir esta hierarquia lógica, com nomes estáveis:

```text
Building_<Code>
├── Visual
│   ├── Construction_00
│   ├── Construction_01
│   ├── Construction_02
│   ├── Complete
│   ├── Damage_01
│   ├── Damage_02
│   └── Ruin
├── Collision
│   ├── PlacementFootprint
│   ├── GameplayBlocker
│   └── DamageVolumes
├── Sockets
│   ├── Entrance_01
│   ├── Worker_01
│   ├── Worker_02
│   ├── Garrison_01
│   ├── Attack_01
│   ├── Projectile_01
│   ├── Smoke_01
│   └── Fire_01
├── Selection
├── HealthbarAnchor
└── Audio
```

O protótipo atual usa referências de MeshFilter, seleção, FOV, áudio, healthbar e NavMeshObstacle no root. Essa composição deve ser preservada para compatibilidade, enquanto os novos pontos visuais e de interação ficam em filhos explicitamente nomeados.

## 6. Estados de construção

O mínimo para produção é ter estados visualmente distintos e deterministicamente ordenados:

| Estado | Requisito visual | Interação |
|---|---|---|
| Preview valid | material de prévia válido, sem sombra enganosa | não bloqueia caminho nem recebe dano |
| Preview invalid | vermelho/âmbar legível, transparência controlada | não pode ser confirmado |
| Foundation | fundação, marcação e primeiros materiais | footprint de obra ativo |
| Frame | estrutura parcial, andaimes/armação | recebe construção e dano conforme regra |
| Shell | paredes e cobertura incompletas | leitura clara da porcentagem |
| Complete | asset final e materiais definitivos | ativa produção, ataque e NavMesh final |
| Damaged | variações de dano sem alterar o footprint | recebe reparo e combate |
| Ruin | escombros e fumaça/fogo conforme tipo | deixa de produzir e libera/transforma bloqueio |

Os três meshes existentes por prédio são suficientes apenas para um protótipo de progresso. Para a versão final, cada asset deve declarar a faixa de construção a que pertence e não depender de índice implícito. A transição deve ser monotônica, sem pop visual reverso ao restaurar save ou adicionar/remover trabalhadores.

## 7. Dano, destruição e ruínas

Cada construção futura deve fornecer pelo menos três faixas de dano (saudável, danificada, crítica) e uma ruína específica ou reutilizável por família visual. A alteração de vida deve refletir-se na aparência com:

- material ou decal de sujeira e rachaduras;
- partes quebradas ou deformadas em prédios apropriados;
- fumaça/fogo com pontos de emissão consistentes;
- áudio contextual com limites de repetição;
- estado crítico legível à distância;
- ruína estável para replay, save/load e sincronização.

O footprint de combate, a altura de bloqueio e o estado da navegação devem ser definidos para a ruína. Não usar apenas ocultação do renderer para simular destruição: a unidade deve deixar de produzir, atacar, aceitar guarnição e aplicar bônus quando destruída. O material de seleção e o healthbar devem continuar coerentes em todos os estados ativos.

## 8. LOD, sombras e desempenho

Todo prédio definitivo deve conter LODs testados na escala de câmera do jogo:

- **LOD0:** detalhe completo, usado em zoom próximo e inspeção.
- **LOD1:** remoção de detalhes pequenos, usado na distância de jogo normal.
- **LOD2:** silhueta e materiais essenciais, usado em zoom distante.
- **Culled/impostor opcional:** para mapas grandes e visão estratégica.

Como ponto de partida de produção, usar aproximadamente 100% / 55% / 20% dos triângulos do LOD0, medindo o resultado no profiler e na tela. São metas iniciais, não números rígidos. Cada LOD deve preservar telhado, entrada, torre, bandeira e demais elementos que comuniquem função. Transições precisam evitar popping perceptível.

Requisitos adicionais:

- LODGroup no root visual, com `Fade` somente se o shader e o projeto suportarem isso sem ruído.
- Sombras em tempo real apenas quando justificadas; usar baked/occlusion conforme o cenário.
- Materiais compartilhados por família para reduzir draw calls.
- Instancing habilitado para peças repetidas como muralhas, estacas, telhas e bandeiras.
- Sem materiais duplicados gerados em runtime para o asset final; a prévia deve possuir caminho controlado para não vazar memória.
- Testar pelo menos 50, 100 e 300 prédios visíveis em cena.

## 9. Materiais e texturas

Cada construção deve ter materiais nomeados por função, não por número de slot: `M_<Code>_Stone`, `M_<Code>_Wood`, `M_<Code>_Roof`, `M_<Code>_Metal`, `M_<Code>_Trim`, `M_<Code>_Construction`, `M_<Code>_Ruin`.

O pacote mínimo é albedo/base color, normal, máscara de roughness/metallic/occlusion quando necessário e variação de sujeira. A resolução deve ser definida por importância e distância de câmera; 2K é aceitável para heróis arquitetônicos, enquanto peças de muralha e variações modulares devem compartilhar atlas quando possível. Evitar transparência total na prévia: usar um shader de placement dedicado que preserve silhueta e profundidade.

Materiais de civilização/era devem trocar apenas os canais previstos, sem duplicar a malha inteira quando a diferença for decorativa. A identidade visual deve ser legível por cor, estandarte, arquitetura e materiais, sem depender apenas de saturação.

## 10. Sockets e pontos de interação

Sockets são Transform filhos com escala 1, rotação coerente e nomes estáveis. Todos devem estar documentados por função e orientação.

- `Entrance_*`: ponto de entrada/saída, frente em `+Z`.
- `Worker_*`: locais de aproximação e animação de construção.
- `Garrison_*`: pontos de guarnição e saída.
- `Attack_*`: origem lógica para ataques de defesa.
- `Projectile_*`: muzzle de projétil, frente apontando para fora.
- `Smoke_*` e `Fire_*`: emissores por faixa de dano.
- `WallStart`, `WallEnd`, `GateLeft`, `GateRight`: necessários para segmentos conectáveis.
- `RallyPoint`: ponto de reunião, sempre fora do blocker.

Nenhum sistema deve localizar esses pontos por índice de filho. A fiscalização deve falhar se um socket obrigatório estiver ausente, duplicado ou fora do volume visual esperado.

## 11. Requisitos futuros do catálogo

O catálogo mínimo de produção deve incluir Centro da cidade, casa, acampamentos de madeira e mineração, moinho, depósito, quartel, campo de arqueiros, estábulo, oficina de cerco, mercado, ferreiro, universidade, posto avançado, torre, muralha, portão, marco de era e maravilha. Cada novo prédio deve entregar o mesmo pacote: FBX/GLB de origem, prefab, estados de construção, dano e ruína, materiais, colliders, LODs, sockets, ícone, dados e critérios de teste.

A implementação desses itens é requisito futuro e não faz parte desta auditoria. O documento serve como contrato para os agentes de modelagem, integração, gameplay e fiscalização.

## 12. Critérios de aprovação por asset

Um prédio somente entra no catálogo principal quando todos os itens abaixo forem verificados:

- root, escala, pivô e base dentro das tolerâncias da seção 3;
- footprint documentado e sem sobreposição indevida com a malha;
- placement válido em terreno plano, inclinado permitido e posição inválida;
- blocker de navegação ativo apenas no estado definido;
- todos os estados de construção carregam sem referência quebrada;
- dano, estado crítico e ruína têm aparência e gameplay coerentes;
- LODGroup testado em três distâncias, sem pop impeditivo;
- materiais sem slots sem nome, texturas ausentes ou shader rosa;
- sockets obrigatórios presentes e orientados corretamente;
- seleção, healthbar, áudio e VFX funcionam no prefab instanciado;
- save/load preserva estado, vida, progresso e ruína;
- desempenho medido em cena de estresse;
- prefab e dados passam validação automatizada e revisão visual em Unity.

## 13. Checklist de fiscalização

O agente de fiscalização deve registrar para cada asset: caminho do arquivo, commit, data, versão do Unity, dimensões, escala, bounds, número de materiais, triângulos por LOD, colliders, sockets, referências quebradas, estados disponíveis, resultado de colocação, resultado de save/load e captura de revisão. Qualquer campo desconhecido deve ser marcado como `PENDENTE`, nunca presumido como aprovado.

### Pendências verificadas na auditoria atual

- House e Tower não possuem LODGroup identificado nos prefabs auditados.
- Não foram identificados estados explícitos de dano ou ruína.
- Não foram identificados sockets nomeados de entrada, ataque, guarnição ou construção.
- Há apenas três malhas de construção por prédio, com seleção por índice/percentual.
- As dimensões físicas dos FBX precisam ser confirmadas no Unity, pois esta auditoria não abriu a cena nem renderizou os modelos.
- A torre está registrada com `attackRange: 0`, `attackDamage: 0` e `attackRate: 0`; isso é uma pendência de gameplay, embora afete a leitura do asset defensivo.

Este documento define o padrão futuro. A conversão dos assets existentes deve ser feita em tarefas isoladas, com revisão visual e commit próprio por construção/família.
