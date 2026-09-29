# Claude Code — concluir etapa 2 e reparar pendências da etapa 1

Trabalhe neste projeto local. Leia primeiro AGENTS.md se existir, as skills locais
aplicáveis, Docs/CharacterMeshAudit/ETAPAS.md, ETAPA_2_RESULTADO.md,
ETAPA_1_RESULTADO.md e REVISAO_CLAUDE_ETAPA_1.md. Confira os arquivos reais.
O usuário pediu esta delegação. Execute apenas o escopo abaixo e pare ao terminar.

## Segurança e estado

- O repositório contém muitas alterações legítimas. Não usar reset/clean nem
  sobrescrever trabalho de outros agentes. Preserve as malhas originais em Inputs.
- Codex corrigiu HeraldicKnightSetup.cs e HeraldicKnightPlayModeCheck.cs para
  solicitar salvamento, respeitar Cancelar e encerrar o processo só em batch.
  Revise e teste essas alterações; não as reverta.
- A execução Unity restrita não conectou ao LicenseClient-User. A revisão automática
  NEGOU a execução elevada. O usuário ainda não autorizou explicitamente Unity
  como administrador. Não contorne isso com helpers, outro agente, tarefas agendadas,
  registro/UAC ou flags para ignorar avisos. Prefira sessão de usuário padrão;
  se o bloqueio persistir, registre-o e peça aprovação específica. Este pedido NÃO
  concede autorização elevada. Não aceite termos ou licenças pelo usuário.
- Não encerrar processos do usuário nem abrir duas instâncias no mesmo projeto.

## Trabalho

1. Reparar visualmente o Heraldic Knight em cópias: escudo rígido corretamente ligado
   ao braço/mão, sem dobrar ou atravessar cabeça/ombro; revisar pesos/interseções do
   tabardo. Preserve clipes e originais. Ajuste verificações cuja expectativa de
   malha única deixar de valer. Não mascare defeitos removendo testes.
2. Concluir etapa 2 existente: Farmhand e Spearman já têm malhas leves, rigs e FBX,
   aprovados no Blender. Leia BuildStage2Characters.py, VerifyStage2Characters.py,
   Assets/Editor/Stage2CharacterPreparation.cs e Stage2RigDemo.cs. Não refaça do zero.
   Execute/verifique importação Humanoid, materiais, controllers, prefabs e cena
   Stage2RigDemo. Corrija problemas encontrados. Ajuste encaixe dos acessórios;
   mãos sem dedos articulados e PoseCheck diagnóstica são limitações documentadas.
3. Faça três grupos de verificações com evidências atuais:
   - Blender/FBX: hashes originais, escala, ossos, pesos normalizados (máximo 4),
     deformação finita e ligação correta de lança/escudo.
   - Unity: compilação, Avatar Humanoid válido, texturas sem rosa, referências
     corretas e testes de menu sem fechar Editor ou perder cena não salva.
   - Play Mode: amostras de várias poses, capturas frontal/traseira, Console sem
     erros; validar os quatro clipes do cavaleiro e poses de ambos os personagens.
   Reexecute os três testes unitários existentes e smoke tests se viável.
   Relatórios antigos não contam como nova execução. Não chame inspeção estática
   de teste runtime nem declare aprovado o que não foi executado.

## Entrega e parada

Escreva Docs/CharacterMeshAudit/CLAUDE_ETAPA_2_RESULTADO.md com arquivos alterados,
comandos, resultados, capturas, limitações e bloqueios. Atualize ETAPAS.md e
ETAPA_2_RESULTADO.md preservando histórico. Atualize a revisão da etapa 1 indicando
quais achados foram corrigidos e realmente testados. Concluir apenas com evidências.
Pare antes da etapa 3: NÃO iniciar cavalo, cervo, novos serviços, downloads grandes,
publicação, commits ou push. Se travar no Unity, conclua o trabalho independente e
documente exatamente o que falta. Não rode etapas simultaneamente com outro agente.
