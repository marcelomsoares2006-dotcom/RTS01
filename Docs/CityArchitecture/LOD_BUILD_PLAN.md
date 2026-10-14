# Plano de LOD das construções

## Escopo

Esta etapa cria variantes LOD1 e LOD2 para as quatro construções do
`EraBuildingKit.blend`:

- `House_Medieval`
- `Barracks_Medieval`
- `Temple_Imperial`
- `Market_Classical`

O arquivo-fonte, os FBX atuais e os prefabs atuais não são substituídos.

## Saída

O script `Tools/CityArchitecture/GenerateBuildingLODKit.py` exporta 8 FBX
novos para `Assets/CityArchitecture/Generated/LOD/`:

- LOD1: aproximadamente 55–60% dos triângulos do original;
- LOD2: aproximadamente 30–35% dos triângulos do original.

Os percentuais são limites de decimação, não uma promessa de contagem exata:
modificadores e partes com geometria mínima podem produzir uma redução menor.

## Segurança do pipeline

1. O `.blend` é aberto somente como fonte.
2. Cada prédio é duplicado em uma coleção temporária.
3. O modificador Decimate é aplicado somente às cópias.
4. Cada cópia é exportada para um nome novo.
5. A coleção temporária é removida da memória do Blender sem alterar a fonte.

## Critérios de aceitação

- 8 arquivos FBX presentes em `Generated/LOD`;
- nenhum arquivo FBX existente alterado;
- escala FBX preservada com `apply_unit_scale`;
- sem animação ou ossos, pois são construções estáticas;
- materiais não incorporados no arquivo, mantendo o fluxo de materiais do
  Unity;
- Unity deverá importar os 8 arquivos antes de criar o `LODGroup` dos prefabs.

## Próxima etapa

Após a inspeção visual no Unity, criar um `LODGroup` por prefab e medir a
troca em distância de câmera RTS. Essa integração fica deliberadamente fora
desta etapa para manter o gerador isolado e não alterar Unity/C#.
