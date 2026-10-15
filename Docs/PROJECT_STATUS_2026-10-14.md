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
- EditMode: 390/390 aprovados no ciclo atual.
- PlayMode: `PLAYMODE_SMOKE_PASSED`, 3 cenários e 0 erros de runtime no ciclo atual.
- Build Windows: `Succeeded`, 0 erros; avisos mantidos e classificados no relatório de qualidade.

### Correção executada

O smoke test deixou de instanciar o worker no `origin` fora do NavMesh antes de aplicar `Warp`; ele agora usa temporariamente o ponto inicial apenas na instância em memória e restaura a posição do prefab. Isso elimina o erro de criação de agente sem alterar assets de gameplay.

## Bloqueios que permanecem

1. O callback de produção agora está ligado ao runtime e protegido por gates, mas ainda precisa de uma prova PlayMode específica que enfileire e conclua uma unidade em cada um dos quatro prédios avançados.
2. A progressão de eras ainda precisa ser ligada ao comando real de avanço da partida; o driver já consome `AdvancedEraState`, mas esse estado continua sendo alterado pelo console/debug.
3. Falta playtest interativo do player standalone e medição de FPS.
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
