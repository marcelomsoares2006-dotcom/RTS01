# Etapas dos personagens — Era Imperial

Atualizado em 29/09/2026. O pedido inicial era parar entre etapas; o usuário
posteriormente autorizou continuar as etapas 3–5 sem interrupção. As partes
independentes do Unity foram executadas; pendências de validação estão abaixo.

| Etapa | Escopo | Estado |
|---|---|---|
| Avaliação | Inspeção de 30 BLENDs e dois ZIPs; catálogo e teste de 13 clipes | Concluída; ver AVALIACAO.md |
| 1 | Heraldic Knight: FBX, material, prefab, quatro clipes, Avatar e cena Unity testada | Concluída por Claude Code em 28/09/2026; três verificações aprovadas; ver ETAPA_1_RESULTADO.md |
| 2 | Trabalhador Farmhand e lanceiro Spearman: malhas leves, rig, acessórios e validação | Importação e Play Mode Unity aprovados; ver ETAPA_2_RESULTADO.md |
| 3 | Cavalo independente e Red Deer Stag: malhas leves, rigs e deformação | Importação e deformação amostrada no Unity aprovadas; animações de jogo pendentes; ver ETAPA_3_RESULTADO.md |
| 4 | Materiais, escala, LODs e validação conjunta dos personagens preparados | Quatro LODGroups corrigidos e 24 capturas comparativas; LOD do cavalo e FPS pendentes; ver ETAPA_4_RESULTADO.md |
| 5 | Variantes de armaduras e preparação dos conjuntos montados | Variantes Unity validadas; pose montada Generic testada em Play Mode e 10 ângulos; movimento/combate montados pendentes; ver ETAPA_5_RESULTADO.md |

Etapa 1 (28/09/2026, 20:45–21:05): Claude Code criou os FBX, material, controller, prefab e a cena
`Assets/CharacterRigging/HeraldicKnight/Scenes/HeraldicKnight_Demo.unity`. Foram aprovados o round-trip
FBX no Blender, a auditoria de importação Unity (Avatar Humanoid válido) e o Play Mode com capturas reais.
O smoke test do jogo continuou aprovado. Limitações: escudo skinnado dobra nos ataques; há interseções de
escudo/cabeça e tabardo; não existe Idle. Detalhes e evidências: `ETAPA_1_RESULTADO.md` e `Etapa1/`.

Observação: arquivos da etapa 2 (`Assets/CharacterRigging/Stage2/`, `Docs/CharacterMeshAudit/Stage2/`,
`Tools/CharacterRigging/BuildStage2Characters.py`) apareceram às 20:58–20:59 por outro processo
(Codex ativo) durante a etapa 1. Claude Code não os criou nem validou.

Pedido: `Docs/CLAUDE_ETAPA_1_HERALDIC_KNIGHT.md`.
Script: `Tools/CharacterRigging/Pedir-Ajuda-Claude.ps1`.
Resultado esperado do Claude: `Docs/CharacterMeshAudit/ETAPA_1_RESULTADO.md`.

Na retomada pelo Codex, ler o resultado, conferir os arquivos efetivamente gerados
e as evidências dos testes antes de marcar a etapa concluída ou iniciar outra.
