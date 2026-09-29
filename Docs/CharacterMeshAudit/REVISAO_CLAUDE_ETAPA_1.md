# Revisão da entrega do Claude — etapa 1

## Atualização posterior — correções Codex em 28/09/2026

Os achados 1 e 2 abaixo foram corrigidos no código: salvamento/cancelamento antes
de substituir cenas, bloqueio durante Play Mode e encerramento do processo somente
em batch. Em sessão interativa o teste encerra apenas Play Mode. O estado das
amostras também é reiniciado entre execuções. Compilação C# isolada contra os módulos
do Unity 6000.6.3f1 aprovada; resta o aviso preexistente em EventManager.cs.
Não houve nova execução Unity, portanto os fluxos interativos ainda precisam de teste.
Escudo/tabardo permanecem pendentes. Pedido de continuação preparado em
`Docs/CLAUDE_ETAPA_2_CONCLUIR.md`, com script
`Tools/CharacterRigging/Pedir-Ajuda-Claude-Etapa2.ps1` (sintaxe e leitura verificadas).
O Claude não foi iniciado por este script nesta entrega. Histórico da revisão abaixo.

28/09/2026. Conferidos scripts de exportação, verificação, setup e demonstração,
`ETAPA_1_RESULTADO.md`, round-trip atualizado (rest poses iguais), relatórios
de importação/Play Mode em `Etapa1/` e captura frontal de Attack. Existem os
FBXs, material, controller, prefab e cena informados. Não alterei a implementação.

Os relatórios sustentam a integração técnica: Avatar Humanoid válido, quatro
clipes com duração correta, ataques com retorno e zero erros em execução. Esta
revisão não executou nova sessão Unity; essas evidências foram produzidas pelo Claude.

## Pendências

1. **P2: teste pelo menu fecha o Editor.** `HeraldicKnightPlayModeCheck.Run` está
   exposto no menu, mas chama `EditorApplication.Exit(0/1)` incondicionalmente ao
   concluir/falhar (linhas 146/219 na versão revisada). Usar Exit somente em batch;
   em sessão interativa, sair apenas do Play Mode. A abertura de cena na linha 68
   também deve oferecer salvamento antes de substituir a cena aberta.
2. **P2: build pelo menu sem salvamento.** `HeraldicKnightSetup.BuildAndAudit`
   chega a `CreateDemoScene`, que cria uma cena Single (linha 298) sem proteger
   alterações não salvas da cena atual. Acrescentar salvamento no caminho interativo.
3. **Acabamento: escudo e tabardo.** A captura `Etapa1/heraldic-3-Attack-front.png`
   confirma que o escudo dobra e atravessa cabeça/ombro. O tabardo também estica.
   O round-trip mostra que os pesos vieram da fonte. Rigidificar/separar o escudo
   e revisar pesos/poses antes de aprovar o personagem como unidade final.

As limitações visuais já foram declaradas pelo Claude. A entrega valida a
integração técnica; não equivale à aprovação artística final do personagem.
