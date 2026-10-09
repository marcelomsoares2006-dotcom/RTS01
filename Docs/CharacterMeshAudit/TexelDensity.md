# Densidade de texels (px/cm) por unidade

Medido em 2026-10-06 com `Tools\CharacterRigging\AuditTexelDensity.py` (add-on Texel Density Checker, Blender 5.2.1), lendo os FBX de `Assets\CharacterRigging`. Somente leitura, nada foi alterado.

Os FBX nao embutem textura. As texturas BaseColor dos personagens sao todas 2048x2048, entao os valores abaixo sao os medidos a 1024 multiplicados por 2 (a densidade escala linearmente com a resolucao).

## Humanoides (malha LOD0 / corpo, textura 2048)

| Unidade | px/cm @2048 | Altura (m) | Triangulos | Situacao (alvo 8,5 +-20% = 6,8 a 10,2) |
|---|---|---|---|---|
| Farmhand | 9,38 | 1,80 | 14.980 | OK |
| Spearman (corpo) | 6,81 | 1,80 | 17.977 | OK (no limite) |
| HeraldicKnight | 7,85 | 1,70 | 19.609 | OK |
| ArmoredKnight | 6,68 | 1,80 | 21.985 | abaixo (-21%) |
| DarkKnight | 9,14 | 1,80 | 21.999 | OK |
| Archer (LOD0) | 9,70 | 1,78 | 30.912 | OK |
| CombatMage (LOD0) | 9,21 | 1,76 | 30.939 | OK |
| PaladinHealer (LOD0) | 7,62 | 1,85 | 31.000 | OK |

Faixa: 6,68 a 9,70 px/cm, mediana 8,5. A variacao e de cerca de 1,45x entre o menor e o maior, entao o conjunto ja e coerente. Os LODs 1 e 2 de Archer, CombatMage e PaladinHealer ficam 3 a 11% acima do LOD0, o que e normal.

## Quadrupedes (fora da meta dos humanoides)

| Unidade | px/cm @2048 | Observacao |
|---|---|---|
| Horse | 5,24 | corpo grande, uma textura para mais area |
| RedDeer | 11,44 | altura de 1,8 m inclui chifres |

Devem ter meta propria. Sugestao: Horse 5 a 6, RedDeer 8 a 10.

## Maquinas de cerco (nao comparaveis)

Catapult e Crossbow usam varias texturas pequenas e repetidas por material (Wood 512/1024, Iron 256, Rope 128/512) e UVs normalizadas por peca. A densidade medida a 1024 (Catapult LOD0 10,1; Crossbow corpo 180) nao representa detalhe real e nao foi comparada. Devem ser tratadas como textura repetida, nao como atlas.

## Recomendacao

- Meta para humanoides: **8,5 px/cm a 2048 (4,25 px/cm a 1024), tolerancia +-20%**.
- Unico humanoide fora: ArmoredKnight (6,68), cerca de 2% abaixo do limite inferior (6,8); corrigir so se houver motivo visual.
- Contexto de camera: com a camera ortografica 8..26 (valores das notas do projeto), a tela mostra no maximo cerca de 0,7 px/cm a 1080p. Texturas a 2048 ficam muito acima do que aparece; 1024 (4,25 px/cm) ainda seria suficiente e usaria 4x menos memoria. Decisao de reduzir fica para quando houver medicao de FPS/memoria.

Comando para repetir:

```
blender.exe -b -P Tools\CharacterRigging\AuditTexelDensity.py -- <fbx...> --size 2048 --target 8.5 --csv saida.csv
```
