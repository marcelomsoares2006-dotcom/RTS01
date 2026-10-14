# Contrato de defesa de construções

O contrato de defesa transforma a torre existente em uma unidade de combate
determinística sem exigir um prefab, modelo ou asset adicional. O perfil base
continua vindo de `UnitData.attackRange`, `attackDamage` e `attackRate`; torres
legadas com esses valores zerados recebem os defaults de compatibilidade de
`CombatAttackProfile` (alcance 12, dano 12 e intervalo 1,5 s).

`UnitManager.Attack` usa `CombatAttackDamage`, valida dono, vida, alcance e
cooldown e aplica o mesmo fluxo de impacto e armadura utilizado pelas demais
unidades. `BuildingDefenseRules.CanTarget` mantém a regra de alvo hostil,
vivo e dentro do alcance em uma função pura, disponível para simulações e
testes editor-free.

Construções que aceitam guarnição podem declarar
`garrisonAttackDamageBonus` no seu `BuildingContentSpec`. Cada unidade
aceita pela regra de guarnição soma esse valor ao dano de combate da
construção. A saída de unidades não reduz o valor declarado, evitando que o
perfil da torre dependa da ordem de eventos da UI; a contagem atual da
guarnição continua determinando o dano efetivo.

O contrato não inventa assets: a entrada atual da torre permanece compatível
com o catálogo existente e pode receber os valores de defesa e guarnição
quando o conteúdo for promovido de `Prototype` para `Playable`.
