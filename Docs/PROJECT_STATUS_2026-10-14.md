# Era Imperial — status e roteiro de conclusão

Data: 14/10/2026  
Branch: `codex/integracao-github-20261014`  
Unity: `6000.6.3f1`

## Resultado desta rodada

### Aprovado

- Release/editor-free: aprovado.
- Nove fundações: `model-authored` e gate aprovado.
- Validadores militares e visuais: arqueiro, cavaleiro e aríete aprovados.
- Auditoria AAA: 44 FBX importados; sem material rosa, escala inválida ou dimensão nula.
- UV: 20 ocorrências classificadas sem normalização destrutiva; 0 linhas inseguras.
- Catálogo: quatro unidades avançadas ligadas à produção: mago e paladino no Barracks, balestra no Archery Range e catapulta no Siege Workshop.
- EditMode: 390/390 aprovados no último resultado Unity registrado.
- Build Windows anterior: `Succeeded`, 0 erros, 33 avisos classificados.

### Correção executada

O smoke test deixou de instanciar o worker no `origin` fora do NavMesh antes de aplicar `Warp`; ele agora usa temporariamente o ponto inicial apenas na instância em memória e restaura a posição do prefab. Isso elimina o erro de criação de agente sem alterar assets de gameplay.

## Bloqueios que permanecem

1. A execução batch mais recente do PlayMode encerrou de forma intermitente durante a troca de cenas/save-load, sem exceção de gameplay no log. Há evidência anterior de 3 cenários aprovados, mas a rodada atual não deve ser marcada como nova aprovação até repetir em uma sessão Unity totalmente limpa.
2. O callback final da fila de produção ainda precisa ser confirmado no runtime para os quatro códigos, não apenas no catálogo e nos contratos.
3. A progressão de eras ainda precisa ser ligada ao estado real da partida/árvore tecnológica.
4. Falta playtest interativo do player standalone e medição de FPS.
5. Permanecem 33 avisos classificados: APIs obsoletas, `BinaryFormatter`, dicionários não serializados e shader de billboard das árvores. Nenhum foi mascarado.
6. UVs `Bolt`/`Trigger` da balestra e uma ilha marginal do LOD2 do HeraldicKnight precisam de inspeção manual; não foram alteradas automaticamente.
7. O branch ainda não foi enviado ao GitHub. O backup temporário `.blend1` permanece fora do Git.

## Próximo ciclo obrigatório

1. Fechar todas as instâncias do Unity e executar novamente o smoke em processo único, confirmando os três cenários no mesmo `playmode-smoke.txt`.
2. Produzir uma unidade avançada em cada edifício e comprovar o spawn físico, custo, população e conclusão da fila.
3. Testar a troca de era e validar os quatro conjuntos de estatísticas em runtime.
4. Abrir o executável Windows e registrar nova partida, produção, construção/demolição/reparo, combate/cerco, save/load e saída.
5. Medir FPS mínimo/médio e registrar a inspeção visual perto/longe das árvores e edifícios.
6. Só depois decidir a migração de saves/API, remover ou aceitar formalmente os avisos e preparar o commit de marco para eventual push.

## Regra de conclusão

O projeto só deve ser declarado concluído quando o gate de fundações, os testes Unity, o PlayMode limpo, o build Windows e o playtest standalone estiverem aprovados com evidências atuais.
