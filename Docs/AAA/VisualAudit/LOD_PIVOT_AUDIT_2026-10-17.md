# Auditoria de pivôs dos LODs — 2026-10-17

## Escopo

Auditoria isolada dos LOD1/LOD2 de House Medieval, Barracks Medieval, Market Classical e Temple Imperial. Os prefabs de produção e a cena jogável não foram alterados.

## Causa encontrada

`EraBuildingKit.blend` mantém os quatro edifícios afastados no mesmo kit, enquanto as raízes permanecem na origem comum. O exportador preservava esse deslocamento em cada FBX. A redução assimétrica do Decimate também altera ligeiramente os bounds do LOD2.

## Correção aplicada

`Tools/CityArchitecture/GenerateBuildingLODKit.py` agora cria uma raiz neutra por edifício, calcula os bounds da geometria e centraliza o conjunto no piso antes da exportação. A origem `.blend` não é salva.

Os oito arquivos corrigidos foram gerados em uma pasta de teste fora de `Assets`:

`D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedTest`

## Resultado Unity

O LODGroup foi testado somente no showcase. Mesmo com a raiz neutra, a hierarquia interna dos FBX ainda apresentou fragmentos deslocados nas capturas. A integração foi retirada e não foi promovida.

## Decisão

Manter os LODs atuais fora dos prefabs de produção. A próxima revisão deve abrir os FBX alinhados no Blender, verificar cada subobjeto e exportar com a hierarquia consolidada antes de uma nova tentativa de LODGroup.

**Status:** infraestrutura corrigida; integração visual ainda pendente.
