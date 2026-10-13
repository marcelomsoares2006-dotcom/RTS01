# Auditoria AAA de Cenário

Esta frente é deliberadamente isolada: não altera runtime compartilhado, cenas de produção nem assets dentro de `Assets`.

## Execução

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\CharacterRigging\AAA\AuditScenario.ps1
```

Para gerar uma prévia conceitual 2K fora de `Assets`:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b -P Tools\CharacterRigging\AAA\GenerateAAAConceptPreview.py
```

## Critérios

- Escala: confirmar no Unity a relação TerrainData/personagem.
- UV: verificar layers do terreno, UV2 de meshes e densidade de texel.
- Normais: confirmar normal maps e tangentes; nenhum material rosa/magenta.
- LOD: medir custo visual e triângulos por distância de câmera.
- Iluminação/shaders: confirmar luz principal, probes, sombras e compatibilidade com Built-in RP.
- Resolução: a prévia tem 2560x1440 (2K horizontal), mas é conceito procedimental e não substitui um render do Map1/Map2.

## Limitação

`Terrain-Map1.asset` e `Terrain-Map2.asset` são binários Unity. A confirmação de altura, layers e normal maps exige abrir o projeto no Unity e capturar dados pelo Editor; o scanner textual registra essa pendência sem modificar os assets.
