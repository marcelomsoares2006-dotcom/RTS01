# Etapas dos personagens — Era Imperial

Atualizado em 28/09/2026. Pedido do usuário: executar por etapas, concluir o escopo
de cada etapa e parar entre elas. Retomar a etapa seguinte somente após nova instrução.

| Etapa | Escopo | Estado |
|---|---|---|
| Avaliação | Inspeção de 30 BLENDs e dois ZIPs; catálogo e teste de 13 clipes | Concluída; ver AVALIACAO.md |
| 1 | Heraldic Knight: FBX, material, prefab, quatro clipes, Avatar e cena Unity testada | Concluída por Claude Code em 28/09/2026; três verificações aprovadas; ver ETAPA_1_RESULTADO.md |
| 2 | Trabalhador Farmhand e lanceiro Spearman: malhas leves, rig, acessórios e validação | Em andamento pelo Codex, autorizado pelo usuário; Blender/FBX aprovados, execução Unity pendente de autorização; ver ETAPA_2_RESULTADO.md |
| 3 | Cavalo independente e Red Deer Stag: malhas leves, rigs e deformação | Aguardando etapa 2 e instrução para continuar |
| 4 | Materiais, escala, LODs e validação conjunta dos personagens preparados | Aguardando etapa 3 e instrução para continuar |
| 5 | Variantes de armaduras e preparação dos conjuntos montados | Aguardando etapa 4 e instrução para continuar |

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
