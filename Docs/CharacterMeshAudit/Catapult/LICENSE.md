# Licencas da Catapulta (Era Imperial)

Verificado em 2026-10-06. Este arquivo registra a origem e a base de licenciamento de cada componente. Nao e aconselhamento juridico.

**Resumo: a catapulta e obra do proprio projeto. Nenhum modelo, textura, animacao, som ou codigo de terceiros foi usado.** Nao houve
modelo licenciado de catapulta disponivel, nenhum servico de geracao 3D/IA foi chamado e nenhuma chave de API existe ou foi gravada.

| Componente | Origem | Situacao |
|---|---|---|
| Malha da catapulta (3 LODs), esqueleto generico (`Root`, `Arm`, `Ammo`, `Winch`, `Rope`, `Wheel_FL/FR/BL/BR`) e UVs | Modelados proceduralmente em Python/Blender (`Tools/CharacterRigging/BuildCatapult.py`): caixas, vigas, cilindros, rodas, engrenagens e concha gerados por codigo | Obra do projeto, sem terceiros |
| Texturas `Catapult_Wood`, `Catapult_Iron`, `Catapult_Rope`, `Catapult_Stone` | Geradas por codigo (ruido de valor/fBm com numpy, `BuildCatapult.py`), sem fotografia nem imagem externa | Obra do projeto |
| Pedra do projetil (`CatapultBoulder.fbx`), fragmento (`CatapultDebris.fbx`) e texturas de particulas `Fx_Smoke`, `Fx_Flash`, `Fx_Spark` | Gerados por codigo (`Tools/CharacterRigging/BuildCatapultProps.py`) | Obra do projeto |
| Clipes `Idle`, `Move`, `Reload`, `Fire`, `Hit`, `Destroy` | Animados por codigo (formulas de movimento e fisica simples em `BuildCatapult.py`); nao ha captura de movimento nem biblioteca de animacao (Mixamo etc.) | Obra do projeto |
| Efeitos (disparo, impacto, poeira do colapso) | Sistemas de particulas montados por `CatapultSetup.cs` com as texturas acima e shaders internos do Unity | Obra do projeto |
| Codigo (Unity C#, Blender Python, PowerShell) | Este projeto | Mesma licenca do repositorio |

## Ferramentas (nao distribuidas com o jogo)
- Blender 5.2 (GPL; a saida de uma ferramenta nao herda a licenca) e Python/numpy embutidos no Blender, usados so para gerar os arquivos.
- Shaders `Standard` e `Legacy Shaders/Particles/*` do Unity (Built-in RP), usados pelos materiais.

## Rastreabilidade
- Reconstrucao completa: `Tools/CharacterRigging/Build-Catapult.ps1`.
- Relatorio de geracao: `Docs/CharacterMeshAudit/Catapult/build-report.json` e `props-report.json`.
- Nenhum arquivo de entrada de terceiros em `Tools/CharacterRigging/Inputs/Catapult` (a pasta nao existe).
