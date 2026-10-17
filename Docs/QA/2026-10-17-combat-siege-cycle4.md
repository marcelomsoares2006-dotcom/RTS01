# Ciclo 4 — combate e cerco

Data: 2026-10-17
Projeto: `RTS01-Corrigido`
Harness: `Assets/Editor/FinalIntegration/CombatSiegePlayModeHarness.cs`

## Escopo

Este ciclo foi implementado em arquivos novos e isolados. Ele usa somente as APIs determinísticas já existentes para verificar contratos de combate e cerco:

- `SiegeAttackProfile` para catapulta;
- `CrossbowAttackProfile` para balestra;
- `MageAttackProfile` para mago;
- `PaladinHealing` e `PaladinHealController` para paladino;
- `SaveMigrationV2Store` para round-trip de persistência.

Não foram alterados scripts de gameplay, prefabs, cenas, assets de produção ou Git.

## Verificações implementadas

### Catapulta

- quatro eras presentes;
- alcance mínimo inclusivo;
- alcance máximo inclusivo;
- rejeição abaixo do mínimo e acima do máximo;
- cooldown bloqueando disparo precoce;
- impacto direto com dano integral;
- dano radial no centro;
- falloff linear no meio do raio;
- exclusão da borda;
- proteção de aliados;
- exclusão de unidades mortas;
- `friendlyFire` explícito;
- progressão de dano, alcance, raio e recarga;
- validação indireta do contrato e do projétil balístico de cerco.

### Balestra

- alcance e cooldown;
- dano perfurante com armadura;
- mínimo de dano;
- progressão de penetração por era;
- contrato de projétil separado e não balístico.

### Mago

- alcance e cooldown;
- dano mágico;
- resistência total anulando o dano;
- quatro eras e contratos válidos.

### Paladino

- cura apenas de aliados vivos e feridos;
- raio horizontal;
- exclusão de inimigos, mortos e alvos com vida cheia;
- quantidade de cura;
- cooldown entre pulsos;
- progressão de cura e raio por era.

### Save/load V2

- gravação usando `SaveMigrationV2Store.Save`;
- leitura com `SaveMigrationV2Store.Load`;
- confirmação da fonte `V2`;
- preservação de um desbloqueio de cerco;
- limpeza de `.data`, `.bak` e `.tmp` de teste.

## Status esperado

O harness deve emitir `COMBAT_SIEGE_CYCLE4_BLOCKED` e sair com código `2`, mesmo quando todas as verificações determinísticas passam. Isso é intencional: o ciclo não inventa runtime visual e não promove um protótipo de contrato a jogo jogável.

Bloqueios explícitos:

1. produção real da catapulta via prefab;
2. collider, camada `UNIT_MASK` e consulta física;
3. projétil balístico como `GameObject`;
4. alvo `BuildingManager` e destruição estrutural;
5. produção em prefab de balestra, mago e paladino;
6. UI, animações e feedback audiovisual;
7. FPS e orçamento de performance.

## Critério de interpretação

- **Código 0:** somente se não houver bloqueios.
- **Código 2:** bloqueios conhecidos, como os gates acima.
- **Código 1:** falha inesperada, exceção ou contrato inválido.

O marcador `COMBAT_SIEGE_CYCLE4_PASS` só seria emitido sem bloqueios. O harness não declara a produção de uma unidade, o disparo de um projétil ou a destruição de uma construção sem uma execução PlayMode real desses objetos.

## Próximo ciclo

Para fechar este gate será necessário, em etapa separada e com o Unity/licenciamento funcional:

1. produzir a catapulta pelo `Siege Workshop`;
2. instanciar o prefab e verificar sockets, colliders e camada;
3. disparar contra um `BuildingManager` inimigo;
4. confirmar impacto, dano radial, destruição e save/load;
5. repetir produção para balestra, mago e paladino;
6. medir FPS em carga crescente e registrar a execução PlayMode.
