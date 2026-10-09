# Preparação do Arqueiro

Estado: **integrado e validado (2026-10-05)**. A fase anterior ("preparação segura; malha ausente") terminou quando a malha foi gerada na Meshy; o resultado completo está em `Docs/CharacterMeshAudit/Archer/RESULTADO.md`.

O arquivo `archer-preparation.contract.json` continua sendo a fonte de verdade: agora registra fonte, licença, artefatos exigidos e os portões de validação. As invariantes permanecem: nenhum personagem existente foi alterado e nenhuma malha foi inventada (o corpo veio da Meshy; arco, aljava e flecha são procedurais e documentados).

O teste `Tools/CharacterRigging/Test-ArcherPreparation.ps1` aceita os dois estados do contrato:
- `blocked-until-source-mesh`: sem fonte e sem assets;
- `integrated-validated`: fonte e licença registradas, todos os artefatos existem, a auditoria Unity (`unity-audit.txt`) e o Play Mode (`TestResults/archer-playmode.txt`) aprovados.

Reconstrução de ponta a ponta: `Tools/CharacterRigging/Build-Archer.ps1` (Blender → Unity). Validações: `ArcherAudit.Run` e `ArcherPlayModeCheck.Run` (ver RESULTADO.md).
