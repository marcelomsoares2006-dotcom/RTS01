# Próxima correção visual — Paladino Curador

## Estado auditado

O relatório `edit-checks.txt` registra 398 PASS e 13 FAIL. A renderização não apresenta magenta em nenhum LOD, pose, acessório ou efeito. As falhas relevantes são de retarget/deformação:

- `Run` não fecha suavemente o ciclo no canal `Right Foot Twist In-Out`.
- A espada apresenta arestas alongadas acima do limite em todas as poses auditadas.
- Em `Attack` e `Death`, a ponta da espada atravessa o chão; isso é um problema de pose/retarget do acessório, não de material.
- O `CharacterData.attackRate` e a presença de `PaladinMeleeAttackController` são regras de gameplay e estão fora do escopo visual.
- A URL assinada em `Tools/CharacterRigging/Inputs/PaladinHealer/ref1.url.txt` é metadado de origem e não deve ser alterada nesta correção.

## Receita segura de correção

1. Abrir as fontes das animações `PaladinHealer@Run.fbx`, `Attack.fbx` e `Death.fbx` no Blender.
2. Corrigir a costura do Run com o mesmo frame inicial/final, ajustando apenas o twist do pé direito e preservando a raiz.
3. Para espada e escudo, revisar o socket no osso da mão e aplicar transform/escala antes de exportar; não editar o esqueleto compartilhado nem o runtime.
4. Nas poses Attack/Death, corrigir a trajetória da mão/arma para manter a ponta acima do piso; exportar novamente os FBX próprios da unidade.
5. Reimportar e repetir o audit visual completo, incluindo as poses e a verificação de pixels magenta.

Não foi aplicada correção automática nesta rodada: alterar curvas diretamente no Unity poderia mascarar a causa e produzir offsets diferentes por animação. Os prefabs e scripts próprios permanecem preservados.
