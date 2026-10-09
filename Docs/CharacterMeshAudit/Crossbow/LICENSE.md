# Licencas da Balestra (Crossbow) - Era Imperial

Verificado em 2026-10-06. Este arquivo registra a origem de cada componente. Nao e aconselhamento juridico.

| Componente | Origem | Situacao |
|---|---|---|
| Malha da balestra (carreta, rodas, cabecote, bracos do arco, corda, cabrestante, gatilho, apoio traseiro), 3 LODs | Modelada **proceduralmente** neste projeto, em Python/Blender (`Tools/CharacterRigging/BuildCrossbow.py`). Nenhuma malha, textura ou rig de terceiros; nenhuma geracao por IA. | Obra do projeto, sem terceiros. Sem restricao de uso ou redistribuicao alem da licenca do repositorio. |
| Virote (projetil) | Mesmo script (`build_bolt`); exportado em separado como `Crossbow_Bolt.fbx`. | Obra do projeto, sem terceiros. |
| Esqueleto generico e rig (pesos por funcao de posicao) | Mesmo script (`build_rig`, `finalize`). | Obra do projeto, sem terceiros. |
| Animacoes Idle, Move, Reload, Fire, Hit, Destroy | Geradas proceduralmente (poses calculadas por codigo, uma chave por quadro) no mesmo script; exportadas como `Crossbow@<Clipe>.fbx`. Nenhum clipe Mixamo ou de terceiros. | Obra do projeto, sem terceiros. |
| Texturas (`Crossbow_Wood`, `Crossbow_Iron`, `Crossbow_Rope`, `Crossbow_Smoke`, `Crossbow_Spark`) | Geradas por ruido e funcoes senoidais (numpy) no mesmo script. | Obra do projeto, sem terceiros. |
| Efeitos (disparo, impacto, poeira da queda), prefabs, Animator Controller | Criados em codigo no Unity (`Assets/CharacterRigging/Crossbow/Editor/CrossbowSetup.cs`) com os shaders embutidos do Unity. | Obra do projeto. |
| Codigo (Python/C#/PowerShell) | Este projeto. | Mesma licenca do repositorio. |

## Ferramentas usadas (nao fazem parte do resultado)
- Blender 5.2 (GPL, apenas como ferramenta; o conteudo gerado nao herda a GPL).
- Unity 6000.6.3f1 (shaders `Standard`, `Unlit/Color`, `Legacy Shaders/Particles/*` embutidos).

## Rastreabilidade
- Nao ha modelo licenciado de balestra no projeto e nao foi usada chave de geracao 3D. Nenhum segredo foi gravado em arquivo.
- Para reconstruir tudo: `Tools\CharacterRigging\Build-Crossbow.ps1`.
