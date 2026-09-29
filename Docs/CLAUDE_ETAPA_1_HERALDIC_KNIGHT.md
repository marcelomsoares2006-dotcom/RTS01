# Pedido para o Claude Code — Era Imperial, etapa 1

Execute esta tarefa no projeto local:
`C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido`

Quero que implemente e valide somente a etapa 1: preparar o personagem Heraldic
Knight e suas quatro animações para Unity 6000.6.3f1. Faça o trabalho, não apenas
um plano. Ao terminar, entregue evidências e PARE antes da etapa 2. O usuário
pediu explicitamente trabalho por etapas, com parada entre elas.

## Estado confirmado e arquivos de referência

- A avaliação de malhas terminou. Leia `Docs/CharacterMeshAudit/AVALIACAO.md`.
- Detalhes verificáveis: `Docs/CharacterMeshAudit/inventory.json` e
  `Docs/CharacterMeshAudit/animation-checks.json`.
- Fonte desta etapa: `Tools/CharacterRigging/Inputs/Meshy_AI_Heraldic_Knight_biped.zip`.
  Contém GLBs de Attack, Running, Triple_Combo_Attack e Walking, cada um com skin.
- Esse personagem tem 19.609 triângulos, 15.937 vértices após importação Blender,
  rig de 23 ossos com nomes `mixamorig:*` e `headfront`, UV e textura incorporada.
  Não tem esqueleto articulado de dedos.
- Os quatro clipes passaram no Blender: todos os vértices têm pesos normalizados,
  até quatro influências, e deformação em quatro amostras temporais por clipe.
  Isso NÃO comprova compatibilidade Humanoid nem qualidade visual no Unity.
- A etapa 1 ainda NÃO foi implementada: nenhum FBX, prefab, controller ou cena
  Heraldic Knight foi criado. O Codex apenas inspecionou o ambiente e preparou este pedido.
- Blender disponível: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`.
- Unity disponível: `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe`.
- O projeto usa Built-in Render Pipeline (`m_CustomRenderPipeline` é zero).
- Há scripts de referência em `Tools/CharacterRigging/BuildCharacterRigPrototypes.py`,
  `VerifyFbxRoundTrip.py` e `Assets/Editor/CharacterRiggingImportAudit.cs`.
  São para os protótipos Human/Horse/Wolf; não os substitua por testes do Heraldic.
- `TestResults/playmode-smoke.txt` e `TestResults/playmode.log` registram três
  cenários do jogo aprovados em 28/09/2026, às 18:21 locais. Portanto, notas antigas
  sobre Unity permanentemente bloqueado não representam necessariamente o estado atual.
  `Tools/Validate-Project.ps1` mostra o modo de execução usado anteriormente.

## Entregáveis desta etapa

1. Crie um script Blender reproduzível para extrair cópias dos quatro GLBs,
   aproveitar o rig existente e exportar um modelo-base e quatro clipes em FBX.
   Preserve duração, escala, orientação, skinning e poses. Não recrie o rig à toa.
   Exclua da exportação objetos auxiliares que o importador glTF cria para exibir ossos.
2. Extraia a textura incorporada e configure material apropriado ao pipeline atual.
   O GLB usa a mesma imagem em base color e emissive: examine a aparência e evite
   importar um material que brilha incorretamente ou deixar um material rosa.
3. Use uma pasta nova `Assets/CharacterRigging/HeraldicKnight/` para FBX, texturas,
   materiais, Animator Controller, prefab e uma cena independente de demonstração.
   Guarde o `.blend` editável fora de Assets, por exemplo em
   `Tools/CharacterRigging/Working/HeraldicKnight/`, evitando importação automática.
4. Configure e valide o Avatar Humanoid. Faça o mapeamento correto se necessário.
   Se algum impedimento real persistir, registre-o; não troque silenciosamente por
   Generic e declare Humanoid aprovado. Reaproveitamento em outros rigs depende disso.
5. Crie prefab com Animator e acesso às quatro animações. A cena deve permitir ver
   cada animação e distinguir as ações; não invente um clipe Idle existente no ZIP.
   Caminhada/corrida podem repetir; ataques devem ter tratamento adequado de término.
   Verifique deslocamento da raiz para evitar que o modelo fuja da demonstração.
6. Preserve as cenas de jogo atuais. A integração desta etapa é o personagem
   funcional na cena de demonstração, sem substituir unidades de produção ainda.

## Validação mínima e critério de conclusão

Realize três verificações diferentes, corrigindo as falhas encontradas:

1. **FBX round-trip:** reimportação no Blender, bones, pesos, escala e movimento dos
   quatro clipes, com comparação das durações e da geometria esperada.
2. **Unity importação:** compile os scripts, confira material/textura, prefab,
   SkinnedMeshRenderer, Avatar válido e Humanoid, e presença dos quatro AnimationClips.
3. **Execução visual:** rode a cena no Unity, percorra os quatro estados, confirme
   deformação, câmera, materiais e Console sem erros. Capture imagens reais do Unity;
   imagens do Blender não substituem essa validação. Registre interseções visíveis
   de capa/escudo/armadura e qualquer limitação remanescente.

Registre comandos, resultados, paths e eventuais limitações em
`Docs/CharacterMeshAudit/ETAPA_1_RESULTADO.md`, e atualize
`Docs/CharacterMeshAudit/ETAPAS.md`. Se um teste não puder rodar, marque como
NÃO VALIDADO e descreva a causa observada. Não declare a etapa concluída apenas
porque o script foi escrito ou o FBX foi exportado.

## Cuidados específicos deste projeto

O repositório tem muitas alterações anteriores do usuário. Inspecione `git status`
e preserve-as. Trabalhe em cópias; não sobrescreva arquivos de Inputs, não faça
reset/clean, não apague backups e não reverta alterações não relacionadas.
Leia AGENTS.md/CLAUDE.md existentes aplicáveis ao projeto.

Para esta etapa não é necessário instalar novas IAs, baixar pesos, terminar Vulkan,
usar Kimodo/SkinTokens, migrar de engine, publicar, comprar serviços ou fazer commits.
Se o Unity já estiver aberto, não encerre uma sessão do usuário à força. Verifique
processos e logs antes de abrir outro Editor. Não altere UAC, registro ou segurança
do Windows para resolver avisos de administrador. Registre bloqueios reais.

Ao concluir, responda em português com arquivos entregues, testes executados,
limitações e o caminho da cena. Encerre com “Etapa 1 concluída; aguardando autorização
para a etapa 2” somente se os critérios passaram. Caso contrário, informe precisamente
o que falta. NÃO inicie Farmhand, Spearman, cavalo ou cervos nesta execução.
