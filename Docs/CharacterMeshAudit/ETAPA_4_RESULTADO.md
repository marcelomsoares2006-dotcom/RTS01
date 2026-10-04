# Etapa 4 — LODs e validação conjunta

Em 29/09/2026 foram gerados e reimportados oito FBXs LOD: níveis 1 e 2 para
Heraldic Knight, Farmhand, Spearman e Red Deer. Os arquivos ficam em
`Assets/CharacterRigging/LODs/<modelo>/`.

O pipeline `BuildStage4LODs.py` reduziu os corpos de cerca de 50% e 25% dos
triângulos de referência, exceto Spearman LOD2, ajustado para 40% após a
versão de 25% mostrar armadura destruída no Unity. `VerifyStage4LODs.py`
confirmou, por reimportação,
UV, pesos presentes/normalizados, até quatro influências e redução de triângulos.
Veja `Stage4/lod-build.json` e `Stage4/lod-validation.json` para contagens exatas.
No lanceiro, lança e escudo são objetos rígidos independentes e não fazem parte
dos FBXs de LOD do corpo; continuam exigindo decisão de LOD em Unity.

O cavalo ficou pendente: repetidas passagens de decimação só reduziram 29.842
para 26.919 triângulos, insuficiente para um LOD útil. O pipeline recusou esses
arquivos em vez de publicá-los como LOD1/LOD2. Precisará de reconstrução ou
transferência de UV/pesos para malha remesh.
Uma tentativa adicional de decimar diretamente a fonte de 1.001.521 triângulos
para 15.000 parou em 27.836; o teste foi descartado, sem substituir os FBXs bons.
Uma terceira tentativa com remesh, UV e pesos transferidos passou nos testes
estruturais (14.994/7.498 triângulos reimportados), porém a inspeção das capturas
`Stage4/Horse-LOD{1,2}.png` mostrou falhas visuais na crina, cauda e superfície.
Esses FBXs foram retirados de `Assets` e guardados, recuperáveis, em
`Tools/CharacterRigging/Working/Stage4/Rejected/`. Os relatórios marcam ambos
como rejeitados. Nenhum LOD do cavalo foi aprovado para uso no jogo.

`Assets/Editor/Stage4LODSetup.cs` criou prefabs separados com LODGroup para
Heraldic Knight, Farmhand, Spearman e Red Deer. Uma primeira execução Unity
encontrou `MissingComponentException` por checagem de componente destruído;
o código foi corrigido e a repetição criou os quatro prefabs. O teste em Play
Mode confirmou três renderers por prefab, ossos dos LODs remapeados ao rig
base e materiais suportados (`Stage3/unity-runtime.txt`). O teste visual
posterior revelou bind poses incompatíveis nos FBXs independentes; os prefabs
agora usam cópias de malha com bind poses recalculados para o rig base.
`Stage4/unity-lod-visual.txt` e 24 imagens `Unity-<nome>-LOD<n>-<vista>.png`
comparam níveis 0/1/2 frontal e lateralmente. Todos renderizam sem rosa nem
partes ausentes. O Spearman LOD2 de 40% preserva a silhueta, mas apresenta
perda de detalhes heráldicos de perto; deve ser usado apenas à distância.
Ainda faltam verificar trocas em distância real, medir FPS e produzir LOD
visualmente aceitável para o cavalo; a etapa não está finalizada.
