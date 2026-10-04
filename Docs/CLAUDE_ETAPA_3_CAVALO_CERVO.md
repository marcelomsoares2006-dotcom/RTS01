# Claude Code — etapa 3: cavalo independente e cervo

O usuário autorizou continuar as etapas do projeto por ordem. Execute somente esta
etapa depois de verificar a entrega da etapa 2. Trabalhe na raiz do projeto.
Leia `AGENTS.md` se existir, `Docs/CharacterMeshAudit/ETAPAS.md`,
`AVALIACAO.md`, `ETAPA_2_RESULTADO.md` e
`CLAUDE_ETAPA_2_RESULTADO.md` se houver. Confira artefatos e testes reais.

## Condição de início

Se a etapa 2 ainda carecer de validação Unity, não marque a etapa 3 como concluída.
Pode preparar malhas, rigs e testes Blender/FBX da etapa 3 independentemente,
mas registre a dependência de integração e pare antes da etapa 4. Não repita
o trabalho da etapa 2 nem altere seus arquivos enquanto outro agente os edita.
Não peça ao usuário para executar o antigo script da etapa 2 se os resultados
já tiverem sido gerados; confira o estado atual.

## Fontes e objetivo

- Cavalo separado: `Tools/CharacterRigging/Inputs/Meshy_AI_knight_horse_0928223644_texture.blend`
  (avaliação: 1.001.521 triângulos). Examine patas, crina, rédeas e arreios.
- Cervo: `Tools/CharacterRigging/Inputs/Meshy_AI_Red_Deer_Stag_0928222254_texture.blend`
  (avaliação: 190.339 triângulos). Mantenha galhadas rígidas ligadas à cabeça.
- `Horse_SkinningFixture.glb` é referência técnica, não substitui o modelo final.
- Criar cópias leves, UV/texturas preservadas, rigs quadrúpedes próprios e FBX
  separados, prontos para importação no Unity 6000.6.3f1. Deixar cavalo
  independente do cavaleiro. Não usar Avatar Humanoid para quadrúpedes; avaliar
  importação Generic. Não fingir que o rig humano da etapa 2 serve ao cavalo.

## Implementação

1. Inventariar objetos, componentes separados, transformações, materiais e textura
   de cada BLEND. Registrar SHA-256 antes/depois dos originais. Trabalhar em cópias.
2. Reduzir polígonos com metas justificadas para RTS sem destruir silhueta,
   articulações, cascos ou UV. Se uma parte exigir malha rígida (arreios, galhadas),
   separá-la/ligá-la ao osso apropriado. Preservar cópias de mapas disponíveis.
3. Montar esqueletos separados, eixo/orientação/escala consistentes, pesos
   normalizados e até quatro influências por vértice. Conferir que cada vértice
   deformável recebeu peso. Criar poses diagnósticas de flexão das quatro pernas,
   pescoço e cabeça; diferenciar pose de teste de animação final.
4. Exportar FBX e Blender editável em `Tools/CharacterRigging/Working/Stage3/`
   e `Assets/CharacterRigging/Stage3/{Horse,RedDeer}/`.
5. Fazer verificação independente por reimportação FBX: contagens, altura,
   ossos, pesos, hierarquia de acessórios, UV/material, poses finitas e amplitude
   de deformação. Produzir capturas de repouso e flexão de ambos os animais.
6. Se Unity estiver acessível como usuário padrão, configurar materiais, import
   Generic, prefabs e cena de inspeção isolada; testar Play Mode, Console e
   capturas reais. Distinguir claramente esses testes dos renders Blender.
   Se não estiver acessível, registrar o bloqueio e conservar gerador/testes
   preparados, sem declarar integração concluída.

## Limites operacionais

Preserve trabalho preexistente. Não usar `git reset`, `clean`, exclusões amplas,
commits ou push. Não abrir duas instâncias Unity no projeto. A revisão automática
anterior negou a execução elevada do Editor porque scripts do projeto poderiam
alterar arquivos e configurações fora dele. Este pedido não autoriza executar Unity
como administrador nem contornar o bloqueio por outros processos, UAC ou flags.
Use sessão padrão quando disponível. Não aceite licenças/termos pelo usuário.

## Entrega

Criar `Docs/CharacterMeshAudit/ETAPA_3_RESULTADO.md` com fontes, hashes,
arquivos gerados, contagens antes/depois, testes e capturas, limitações e estado
da validação Unity. Atualizar somente a linha da etapa 3 em `ETAPAS.md`, mantendo
o histórico de outras etapas. Critérios mínimos: originais intactos, FBX reimportado,
pesos corretos, deformação observada, partes rígidas sem dobrar e evidência visual.
Se Unity não puder ser executado, informar precisamente o que falta. Pare antes
da etapa 4: materiais/LODs conjuntos e variantes montadas ficam para pedidos futuros.
