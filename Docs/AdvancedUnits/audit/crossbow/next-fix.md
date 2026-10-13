# Próxima correção visual — Balestra

## Estado auditado

O relatório `edit-checks.txt` registra 269 PASS e 4 FAIL. As falhas não indicam malha quebrada ou material magenta:

- LOD2 está com transição `0`; revisar no prefab visual para um limiar positivo, mantendo a ordem LOD0 > LOD1 > LOD2.
- `Crossbow_Feather` não possui textura principal. A cor sólida é intencional no modelo procedural, mas pode receber uma textura pequena/atlas própria em uma etapa de acabamento.
- `FOV` também não possui textura principal, mas pertence ao sistema compartilhado de FOV e está fora do escopo desta unidade.
- A falha de `CharacterData` é de dados de gameplay e não deve ser corrigida nesta auditoria visual.

## Receita segura

1. No `CrossbowSetup.ConfigureLods`, trocar somente o terceiro limiar por um valor positivo pequeno (por exemplo `0.01f`), reconstruir a unidade e confirmar que os três limiares continuam estritamente decrescentes.
2. Se for desejada textura para a pluma, criar uma textura própria em `Assets/CharacterRigging/Crossbow/Textures/`, remapeá-la apenas em `Crossbow_Feather.mat` e repetir o audit visual.
3. Não alterar `FOV`, contratos de gameplay, `AdvancedBridge` ou qualquer script compartilhado.

Nenhuma alteração foi aplicada nesta rodada porque a troca do LOD exige reconstrução do prefab e a textura da pluma exige uma decisão artística; ambos permanecem pendentes e não bloqueiam a ausência de magenta.
