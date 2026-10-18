# Auditoria antes/depois â€” candidatos PBR das muralhas

Data: 2026-10-18

Veredito: **PASS_STATIC_CANDIDATE_ONLY**. Materiais de produÃ§Ã£o nÃ£o alterados e sem referÃªncias candidatas.

## Antes

- Wall_Ancient, Wall_Classical, Wall_Medieval e Wall_Imperial usam Standard/Built-in somente com albedo.
- Diff Git dos quatro materiais de produÃ§Ã£o nesta execuÃ§Ã£o: 0 arquivo(s); esperado: 0.

## Depois

- 4 materiais Standard candidatos, cada um com MainTex, BumpMap, MetallicGlossMap e OcclusionMap.
- 16 mapas PNG 512x512, derivados somente de texturas existentes do projeto; hashes estÃ£o no JSON.
- Importador: albedo sRGB; normal linear com textureType 1; metallic/gloss e AO lineares; mipmaps habilitados.
- Tiling/offset candidatos: escala (1,1) e offset (0,0).

## UV e texel density

- Os quatro prefabs usam o mesh Unity embutido fileID 10202; o YAML nÃ£o contÃ©m coordenadas UV. A checagem registra UV canÃ´nica esperada 0â€“1 e marca confirmaÃ§Ã£o no Unity como REVIEW_RUNTIME_IMPORT.
- Com mapas 512Â² e UV 1x1, a faixa nominal conservadora Ã© calculada pelas escalas serializadas de cada prefab; valores por prefab estÃ£o no JSON.

## Gate

- PASS estÃ¡tico: GUIDs, resoluÃ§Ã£o, tipos de textura, canais PBR e ausÃªncia de referÃªncia candidata nos prefabs.
- NÃ£o promover: falta abrir no Unity 6 e confirmar importaÃ§Ã£o, shader efetivo, seams, iluminaÃ§Ã£o e leitura em perto/mÃ©dio/longe.

RelatÃ³rio detalhado: Docs/QA/2026-10-18-wall-material-candidate-audit.json.
