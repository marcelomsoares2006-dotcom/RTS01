# Ciclo 3 — protótipo de avanço de eras

Data: 2026-10-17
Escopo: auditoria isolada das regras existentes de progressão, replay e save/load.

## Arquivo e execução

- Harness: `Assets/Editor/FinalIntegration/EraProgressionPlayModeHarness.cs`
- Método Unity: `EraProgressionPlayModeHarness.Run`
- Execução esperada: Unity Editor fora do PlayMode, com a cena vazia criada pelo próprio harness.
- Código de saída:
  - `0`: todos os gates exercitados passaram e não houve bloqueios;
  - `2`: bloqueio conhecido, incluindo infraestrutura/licenciamento ou UI não exercitada;
  - `1`: falha inesperada/assert ou exceção.

O harness não salva cenas, prefabs, assets ou arquivos de gameplay. O arquivo temporário de save é criado em `Application.persistentDataPath` e removido no `finally`.

## Cobertura implementada

1. **Sequência completa**
   - Ancient → Classical (`BuildingEra.Dark` → `Feudal`);
   - Classical → Medieval (`Feudal` → `Castle`);
   - Medieval → Imperial (`Castle` → `Imperial`);
   - verificação de `AdvancedEraState` em cada transição e no final.

2. **Rejeições**
   - salto direto Ancient → Medieval rejeitado por `EraProgressionRules` e `ConstructionReplayRuntime`;
   - avanço sem tecnologia requerida rejeitado por `EraProgressionRules.TryAdvance`;
   - `AgeUpTechnologyPlanningRules` deve encaminhar o requisito ausente para uma intenção de pesquisa, sem declarar o age-up pronto.

3. **Desbloqueio por era**
   - tecnologia de cada transição começa disponível na era atual;
   - cada tecnologia só é liberada por `TechnologyProgressionRules` no momento correto;
   - a intenção muda de pesquisa para avanço somente depois de `TryUnlock`.

4. **Save/load**
   - gravação pelo `EraImperial.SaveMigrationV2.SaveMigrationV2Store`;
   - fonte confirmada como V2;
   - preservação dos três comandos, das tecnologias e do snapshot Imperial;
   - reset para Ancient seguido de `ConstructionReplayRuntime.Restore` a partir do save;
   - confirmação de republicação de `AdvancedEraState.Imperial`.

## Bloqueio explícito de UI

Este ciclo não inventa uma interface. O harness não afirma que o botão/comando de avanço está disponível para o jogador, nem que existe feedback visual, sonoro, barra de progresso ou alteração de câmera. Esses itens permanecem `BLOCKED` até uma execução PlayMode manual/visual da partida real.

## Limitações

- A execução ainda depende de o Unity inicializar e compilar o projeto; a falha conhecida `LicenseClient-User` deve resultar em `Exit(2)`, não em falso `PASS`.
- A cobertura é de contratos, replay e persistência; não substitui teste de produção de unidades, construção de prédios, combate, cerco ou avaliação artística LOD/PBR.
- Não houve alteração em scripts de gameplay, prefabs, cenas, assets ou Git.

## Critério de aceite

O marcador `ERA_PROGRESSION_CYCLE3_PASS` só pode ser emitido quando a sequência, as rejeições, os desbloqueios e o round-trip V2 passarem. Se a UI/feedback continuar sem validação real, o resultado deve permanecer `ERA_PROGRESSION_CYCLE3_BLOCKED` com código `2`, conforme o escopo solicitado.
