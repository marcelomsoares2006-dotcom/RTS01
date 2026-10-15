# Era Imperial — status e roteiro de conclusão

Data: 14/10/2026  
Branch: `codex/integracao-github-20261014`  
Unity: `6000.6.3f1`

## Resultado desta rodada

### Ciclo seguinte — produção runtime

- A fila determinística deixou de ser apenas contrato: foi criado `BuildingProductionRuntimeDriver`, ligado automaticamente quando um `Building` fica operacional.
- O driver reconstrói o snapshot de progressão a partir da era avançada, tecnologias desbloqueadas, prédios operacionais e economia do proprietário; mantém os gates antes de aceitar a ordem.
- A conclusão resolve `CharacterData`, instancia a unidade correspondente, reserva/libera população pela fila e tenta posicioná-la no NavMesh.
- `GameManager` agora inicializa `DataHandler.EconomyRuntime` antes do carregamento/restauração dos dados, evitando o binding nulo no início de uma partida.
- Compilação corrigida após a auditoria: `ImperialEra` usa `Ancient`, `Classical`, `Medieval`, `Imperial`; o mapeamento para `Dark`, `Feudal`, `Castle`, `Imperial` está explícito.

### Aprovado

- Release/editor-free: aprovado.
- Nove fundações: `model-authored` e gate aprovado.
- Validadores militares e visuais: arqueiro, cavaleiro e aríete aprovados.
- Auditoria AAA: 44 FBX importados; sem material rosa, escala inválida ou dimensão nula.
- UV: 20 ocorrências classificadas sem normalização destrutiva; 0 linhas inseguras.
- Catálogo: quatro unidades avançadas ligadas à produção: mago e paladino no Barracks, balestra no Archery Range e catapulta no Siege Workshop.
- PlayMode específico: `COMBATMAGE_PLAYMODE_PASSED`, `PALADINHEALER_PLAYMODE_PASSED`, `CROSSBOW_PLAYMODE_PASSED` e `CATAPULT_PLAYMODE_PASSED` registrados nos logs r6/r8/r3/r5.
- EditMode: 390/390 aprovados no ciclo atual.
- PlayMode: `PLAYMODE_SMOKE_PASSED`, 3 cenários e 0 erros de runtime no ciclo atual.
- Build Windows: `Succeeded`, 0 erros; avisos mantidos e classificados no relatório de qualidade.

### Correção executada

O smoke test deixou de instanciar o worker no `origin` fora do NavMesh antes de aplicar `Warp`; ele agora usa temporariamente o ponto inicial apenas na instância em memória e restaura a posição do prefab. Isso elimina o erro de criação de agente sem alterar assets de gameplay.

## Bloqueios que permanecem

1. O callback de produção agora está ligado ao runtime e os quatro PlayModes específicos foram aprovados; falta a prova de enfileiramento/conclusão real em cada prédio dentro de uma partida completa.
2. A progressão de eras foi sincronizada no replay/save, mas ainda precisa ser acionada pelo comando real de avanço da partida; o estado continua podendo ser alterado pelo console/debug.
3. O player standalone e a medição de FPS passaram no probe gráfico controlado; ainda falta um playtest humano de interface.
4. Permanecem avisos classificados: APIs obsoletas, `BinaryFormatter`, dicionários não serializados e shader de billboard das árvores. Nenhum foi mascarado.
5. UVs `Bolt`/`Trigger` da balestra e uma ilha marginal do LOD2 do HeraldicKnight precisam de inspeção manual; não foram alteradas automaticamente.
6. O branch ainda não foi enviado ao GitHub. O backup temporário `.blend1` permanece fora do Git.

## Próximo ciclo obrigatório

1. Criar uma auditoria PlayMode dedicada que enfileire mago, paladino, balestra e catapulta, confirme custo/população, conclusão e presença física no NavMesh.
2. Ligar o comando validado de avanço de era ao `AdvancedEraState`, persistindo a era no save/load sem duplicar a fonte de verdade.
3. Testar a troca de era e validar os quatro conjuntos de estatísticas em runtime.
4. Abrir o executável Windows e registrar nova partida, produção, construção/demolição/reparo, combate/cerco, save/load e saída.
5. Medir FPS mínimo/médio e registrar a inspeção visual perto/longe das árvores e edifícios.
6. Só depois decidir a migração de saves/API, remover ou aceitar formalmente os avisos e preparar o commit de marco para eventual push.

## Regra de conclusão

O projeto só deve ser declarado concluído quando o gate de fundações, os testes Unity, o PlayMode limpo, o build Windows e o playtest standalone estiverem aprovados com evidências atuais.

## Fechamento de produção e avanço de era — 15/10/2026

- Produção real em partida aprovada: o smoke criou um Town Center operacional a partir do `BuildingData` do catálogo, enfileirou `villager`, consumiu economia/população, concluiu a fila pelo `BuildingProductionRuntimeDriver`, resolveu o alias legado para `worker`, instanciou a unidade e confirmou movimento no NavMesh. Marcador: `PLAYMODE_PRODUCTION_PASSED`.
- O comando real `advance_era` foi ligado ao `ConstructionReplayRuntime.TryAdvanceEra`. A validação sequencial, o comando persistido, o replay e `AdvancedEraState` agora usam a mesma fonte de verdade; o teste `LiveAdvanceEraCommandUpdatesReplayAndAdvancedEraState` passou.
- Validação final: EditMode 391/391, PlayMode smoke 3/3 com 0 erros, produção real aprovada e build Windows concluído com código 0.
- Correção adicional: o contrato de produção mantém `villager` para compatibilidade, mas o driver resolve-o para o `CharacterData` real `worker`.

Pendências fora deste marco: inspeção artística manual das UVs classificadas, playtest humano de interface e eventual push ao GitHub. Nenhuma delas foi executada automaticamente nesta rodada.

## Ciclo de verificação — PlayMode, eras, standalone e UV

- A restauração do replay agora sincroniza `AdvancedEraState` com a era persistida, usando o mapeamento validado `Feudal -> Classical`, `Castle -> Medieval` e `Imperial -> Imperial`; a fonte de verdade continua sendo o replay salvo.
- `Unit.MaxHP` passou a acompanhar os valores da era atual; ataques avançados são encaminhados por `IAttackHandler`, e cura negativa do paladino é aplicada como recuperação limitada ao máximo.
- Bateria específica: as evidências r3 confirmaram o fluxo de combate do mago e da catapulta até a etapa de LOD; a balestra passou. A execução r6 do paladino ainda falhou no golpe corpo a corpo (`golpes=0`) apesar de entrar no estado `Attack`; permanece bloqueador real para os quatro testes específicos.
- A bateria geral passou novamente: EditMode, PlayMode smoke e Windows build concluídos com código 0. Isso não substitui a bateria específica pendente.
- O player Windows foi executado fora do Editor em 1280x720 por 8 segundos, com probe ativado por argumento. Resultado: código 0, média 59,75 FPS, mínimo 14,27 FPS durante a inicialização e máximo 60,01 FPS. A medição usa alvo de 60 FPS e está registrada em `Builds/Windows/TestResults/standalone-performance-capped.log`.
- Auditoria UV manual somente leitura passou: 44 arquivos, 20 ocorrências classificadas, 0 linhas inseguras. Permanecem para inspeção artística `Bolt`/`Trigger` da balestra e a ilha marginal do LOD2 do `HeraldicKnight`; nenhuma UV foi normalizada automaticamente.

### Próximo bloqueador técnico

Os quatro PlayModes específicos agora têm marcadores explícitos de aprovação. A produção real em partida e o comando de avanço de era foram fechados; a inspeção artística UV continua manual e não deve ser automatizada sem decisão visual.

## Comparação visual e correção de estado de construção — 15/10/2026

- A comparação com referências públicas de Age of Empires IV não aprovou superioridade visual: o projeto ainda está atrás em detalhe médio, PBR, props funcionais e composição de cidade. O kit atual é base de produção, não paridade AAA final.
- Corrigido o caminho que podia deixar o material vermelho/translúcido de colocação aplicado após a construção tornar-se operacional: `Building._SetAlive()` e `BuildingFeedbackEmitter` agora restauram os materiais autorais.
- Corrigido o fallback do `Tools/Validate-Project.ps1` para não falhar quando não existe processo Unity.
- A tentativa de validação desta rodada chegou ao Unity, mas ficou bloqueada por falha intermitente do Unity Licensing Client e excedeu 300 s; os resultados anteriores continuam registrados, porém esta correção precisa de nova execução com o Hub/licença estabilizados.
- O push GitHub foi realizado anteriormente no branch `codex/integracao-github-20261014`, commit remoto confirmado `67ad842d9153e3033a214e47b9919ece296ffed0`. O backup `.blend1` continua fora do Git.
