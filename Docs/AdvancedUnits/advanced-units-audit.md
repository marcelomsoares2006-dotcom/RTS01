# Unidades avançadas por era — auditoria

Data da auditoria de integração: 14/10/2026. Projeto: `RTS01-Corrigido` (Unity 6000.6.3f1).

Este documento preserva o histórico da etapa de contratos de 05/10/2026, mas o estado atual foi revalidado diretamente no
projeto. Os quatro ativos avançados possuem agora malha, rig, materiais, animações, prefabs jogáveis, `CharacterData`,
contrato visual, sockets, projéteis/efeitos quando aplicável e controladores de runtime. A produção foi ligada ao catálogo
real nesta auditoria: Barracks produz `combat_mage` e `paladin_healer`, Archery Range produz `crossbow` e Siege Workshop
produz `catapult`. Nenhuma unidade avançada é uma cena separada do Build Settings; elas são carregadas por `Resources` e
instanciadas pela produção, portanto não devem ser adicionadas como cenas.

## Auditoria de integração atual

| Frente | Estado verificado | Evidência |
|---|---|---|
| `combat_mage` | **OK** | `CombatMage.prefab`, `CharacterData`, `CombatMage.asset`, `ProjectileAttackController`, contrato `CombatMageBolt`, socket `ProjectileSpawn`, efeitos e clipes reais. |
| `paladin_healer` | **OK** | `PaladinHealer.prefab`, `CharacterData`, `PaladinHealer.asset`, `PaladinHealBehaviour`, aura de cura, clipes Heal/Attack e efeito de morte. Não usa projétil. |
| `catapult` | **OK** | `Catapult.prefab`, `CharacterData`, `Catapult.asset`, `CatapultCombatController`, `ProjectileSpawn` no osso do braço, pedra balística e efeitos de disparo/impacto. |
| `crossbow` | **OK** | `Crossbow.prefab`, `CharacterData`, `Crossbow.asset`, `ProjectileAttackController`, socket `BoltSpawn`, virote separado e efeitos de disparo/impacto. |
| Produção | **OK após esta rodada** | Catálogo `Building Content Catalog.asset` e teste `EssentialRosterDefinesEraCostProduction` cobrem os quatro códigos. |
| Build Settings | **OK por arquitetura** | As cenas válidas permanecem em `ProjectSettings/EditorBuildSettings.asset`; unidades são prefabs/Resources, não cenas. `buildSettingsApproved` dos contratos permanece falso por não haver requisito de cena separada. |
| Runtime | **OK com ressalvas** | Controladores avançados são componentes dos prefabs e substituem o dano instantâneo padrão; validações de Play Mode existentes comprovam disparo, cura, projétil, LOD e morte. |

**Correção executada nesta rodada:** quatro entradas de produção e uma verificação automatizada que exige `CharacterData`,
prefab e código coerentes. Não foram alterados arquivos de outras frentes nem foi feito push.

### Testes desta rodada

- Checagem estática do catálogo: **4/4 códigos de produção encontrados** e **4/4 `CharacterData` + prefabs resolvidos**.
- `Tools/ValidateMilitaryUnitAssets.mjs`: **passou** (arqueiro, cavaleiro e aríete; validador de produção existente).
- `Tools/ValidateMilitaryUnitVisualAssets.mjs`: **passou** (3/3 ativos militares verificados).
- `Tools/Validate-FoundationAssets.ps1 -ReportOnly`: **passou**.
- `Tools/Validate-Release.ps1` e `Tools/Validate-EditorFree.ps1`: **passaram**.
- `EraImperial.AdvancedUnits.Tests` no Unity: **não reexecutado nesta rodada**, pois a fila detectou o projeto aberto em instâncias do Unity; nenhum processo foi encerrado. As evidências Play Mode específicas já existentes continuam registrando aprovação para as quatro unidades.

## Decisões de arquitetura

| Decisão | Motivo |
|---|---|
| Lógica em asmdefs próprios (`EraImperial.Units.Advanced/Mage/Paladin/Siege/Crossbow`), `autoReferenced`, sem depender do `Assembly-CSharp` | O asmdef de testes existente (`EraImperial.EditorTests`) só referencia `EraImperial.Compatibility` e **não enxerga o `Assembly-CSharp`**. Em asmdefs próprios a lógica é testável com tipos reais, e o jogo ainda pode usá-la (o `Assembly-CSharp` referencia asmdefs `autoReferenced`). |
| Ponte `Assets/Scripts/Units/AdvancedBridge/AdvancedUnitDataBridge.cs` no `Assembly-CSharp` | Única parte que toca `UnitData`/`CharacterData`/`BuildingData`. Pasta extra além das cinco pedidas, porque um asmdef não pode referenciar o `Assembly-CSharp`. |
| Contratos produzem `UnitDataFields` com os mesmos nomes de `UnitData` (`code`, `unitName`, `healthpoints`, `attackRange`, `attackDamage`, `attackRate`, `cost`, `fieldOfView`) | Reutiliza as abstrações atuais. `attackRate` = período do `Timer` em `CharacterBT`/`BuildingBT`, ou seja, o cooldown em segundos. |
| `dataKind` (Character/Building) em cada contrato | Catapulta e balestra podem ser unidade (padrão) ou estrutura (`BuildingData`). |
| Projétil como `ProjectileContract` separado, um asset por unidade à distância | Requisito "projétil separado". O paladino não tem projétil. |
| Contratos em `Assets/Data/AdvancedUnits`, fora de `Resources` | `Globals` carrega `CharacterData` de `Resources`; esses assets não são `UnitData`, mas ficam fora por segurança. |
| A ponte recusa preencher um `UnitData` sem dados válidos **e** visual real | Evita unidade sem malha no catálogo (`Unit` instancia `data.prefab`). |

## Arquivos criados

Scripts (cada `.cs`/`.asmdef`/pasta tem `.meta`):

- `Assets/Scripts/Units/Advanced/`: `EraImperial.Units.Advanced.asmdef`, `ImperialEra.cs`, `AdvancedUnitCodes.cs`,
  `AdvancedUnitRules.cs` (`CombatGates`), `DamageType.cs`, `ValidationReport.cs`, `EraProgression.cs`, `ResourceCost.cs`,
  `AdvancedVisualContract.cs`, `ProjectileContract.cs`, `AdvancedUnitContract.cs`
- `Assets/Scripts/Units/Advanced/Editor/`: `EraImperial.Units.Advanced.Editor.asmdef`, `AdvancedUnitAssetGenerator.cs`
  (menu *Era Imperial > Advanced Units > Criar assets de contrato*; nunca sobrescreve asset existente)
- `Assets/Scripts/Units/Mage/`: `.asmdef`, `CombatMageContract.cs` (+ `MageAttackProfile`), `CombatMageDefaults.cs`
- `Assets/Scripts/Units/Paladin/`: `.asmdef`, `PaladinHealerContract.cs`, `PaladinHealing.cs`
  (`PaladinHealing`, `PaladinHealController`, `TargetHealLedger`), `PaladinHealerDefaults.cs`
- `Assets/Scripts/Units/Siege/`: `.asmdef`, `CatapultContract.cs`, `SiegeAttackProfile.cs`, `CatapultDefaults.cs`
- `Assets/Scripts/Units/Crossbow/`: `.asmdef`, `CrossbowContract.cs` (+ `CrossbowAttackProfile`), `CrossbowDefaults.cs`
- `Assets/Scripts/Units/AdvancedBridge/AdvancedUnitDataBridge.cs`

Assets de dados (`Assets/Data/AdvancedUnits/`, 12,7 KB no total): `CombatMage.asset`, `PaladinHealer.asset`,
`Catapult.asset`, `Crossbow.asset`, `Projectiles/CombatMageBolt.asset`, `Projectiles/CatapultBoulder.asset`,
`Projectiles/CrossbowBolt.asset`.

Testes (`Assets/Tests/Editor/AdvancedUnits/`, asmdef próprio `EraImperial.AdvancedUnits.Tests`): `ContractSet.cs`,
`UnitCodesAndProgressionTests.cs`, `PaladinHealingTests.cs`, `RangedAndSiegeCombatTests.cs`, `VisualReadinessTests.cs`,
`BridgeAndCatalogTests.cs`, `GeneratedContractAssetsTests.cs`.

Documentação: este relatório e `Docs/AdvancedUnits/evidence/` (XMLs e saídas). `Docs/AdvancedUnits/previous-draft/` guarda
dois rascunhos não rastreados de uma tentativa anterior (ver "Itens encontrados").

## Dados de balanceamento iniciais

Referência do jogo: Soldier 30 PV / dano 20 / alcance 1 / período 2; Spearman 45 PV / dano 24 / alcance 1,6 / período 1,8.
Valores são ponto de partida, editáveis no Inspector.

| Unidade | Era | PV | Dano | Alcance | Cooldown (s) | Específico |
|---|---|---|---|---|---|---|
| combat_mage (mágico) | Ancient / Classical / Medieval / Imperial | 22 / 28 / 34 / 42 | 18 / 24 / 31 / 40 | 8 / 9 / 10 / 11 | 2,4 / 2,2 / 2,0 / 1,8 | custo Gold 70 + Stone 30 |
| paladin_healer | idem | 70 / 90 / 115 / 145 | 16 / 20 / 25 / 31 | 1,5 | 1,8 / 1,7 / 1,6 / 1,5 | cura 6 / 9 / 13 / 18; raio 4 / 4,5 / 5 / 6; cooldown de cura 3,0 / 2,8 / 2,5 / 2,2; limite por alvo 18 / 27 / 39 / 54 em 10 s; Gold 80 + Wood 20 |
| catapult (impacto) | idem | 60 / 80 / 105 / 135 | 45 / 60 / 78 / 100 | máx 20 / 22 / 24 / 26; mín 8 | 6,0 / 5,5 / 5,0 / 4,5 | radial 25 / 34 / 45 / 58; raio 3 / 3,5 / 4 / 4,5; Gold 120 + Wood 100 + Stone 40 |
| crossbow (perfurante) | idem | 28 / 35 / 43 / 52 | 22 / 29 / 37 / 47 | 14 / 15,5 / 17 / 18,5 | 3,2 / 2,9 / 2,6 / 2,3 | penetração de armadura 15 / 25 / 35 / 50 %; Gold 60 + Wood 40 |

Regras impostas pela validação: quatro eras, uma vez cada, em ordem; vida e dano estritamente crescentes; alcance não
decrescente; cooldown não crescente; mago com alcance > corpo a corpo (2,0); paladino com alcance ≤ 2,0; catapulta com
mínimo < máximo e radial < impacto; balestra com alcance ≥ 12; projétil obrigatório e do tipo certo (Magic / Siege balístico /
Piercing).

Regras de combate (puras, testadas): cura do paladino só em aliados (mesmo dono), vivos, feridos, dentro do raio (distância no
plano XZ, borda incluída), limitada à vida faltante e ao limite por alvo na janela; si mesmo só com `canHealSelf`; pulso em
cooldown não cura nem reinicia a contagem. Catapulta: dentro de [mínimo, máximo] e recarregada; alvo direto leva o impacto,
demais inimigos no raio levam o radial com queda linear (zero na borda); aliados poupados salvo `friendlyFire`. Balestra:
armadura efetiva reduzida pela penetração, mínimo 1 de dano.

## Testes executados e resultados

Todos em Unity 6000.6.3f1, batch, sem erro de licenciamento (o log traz `Licensing is not yet initialized` /
`Access token is unavailable` como mensagens informativas; o processo saiu com código 0 e os testes rodaram).

| # | Execução | Resultado |
|---|---|---|
| 1 | Gerador de assets (`AdvancedUnitAssetGenerator.GenerateBatch`) | saída 0; 7 assets criados |
| 2 | EditMode `EraImperial.AdvancedUnits.Tests` | **58 aprovados, 0 falhas, 0 ignorados** (`evidence/advanced-units-tests.xml`) |
| 3 | `Tools\Validate-Project.ps1` — etapa de testes unitários (`EraImperial.EditorTests`) | Unity saiu com sucesso, **6/6 aprovados** (Terrain 3, catálogo 1, Arqueiro 2) |
| 4 | `Tools\Validate-Project.ps1` — checagem final do script | **FALHOU: "Expected all three unit tests to pass"**. O script exige exatamente 3 aprovados; a suíte já tinha 6 antes desta etapa. Interrompeu antes do Play Mode. Não alterei o script (arquivo não relacionado). |
| 5 | Play Mode (`EraImperialSmokeTests.Run`, o mesmo passo do script) | **3/3 cenários aprovados, 0 erros**, 0 % de pixels magenta (`evidence/playmode-smoke.txt`) |

Distribuição dos 58 testes: códigos únicos e progressão 13; cura do paladino 10; alcance/cooldown/dano de catapulta,
balestra e mago 17; prontidão visual 9; ponte e catálogo/Build Settings 6; assets gerados 3.

Cobertura dos cinco requisitos de teste: (1) códigos únicos nas constantes, nos contratos, nos projéteis e nos assets;
(2) progressão crescente por era, com casos negativos (regressão, era faltando, era duplicada); (3) cura em área; (4) alcance,
cooldown e dano; (5) nenhuma referência visual inexistente é pronta — caminhos declarados sem asset, objeto destruído,
asset do tipo errado, socket ausente no prefab, clipe ausente, sinalizador `buildSettingsApproved` isolado; e o portão abre
quando prefab, socket e clipe reais existem (duplo de teste em memória, destruído ao fim, não é asset do projeto).

## Auditoria estática

| Verificação | Resultado |
|---|---|
| Erros/avisos do compilador citando arquivos novos | nenhum nos logs de compilação do Unity (todas as DLLs dos asmdefs novos foram geradas) |
| Tipos novos (52) com nome duplicado entre si ou com o resto de `Assets/**/*.cs` | nenhum |
| GUIDs duplicados nos `.meta` do projeto | nenhum |
| Arquivo/pasta novo sem `.meta` | nenhum |
| GUIDs referenciados pelos 7 assets gerados sem `.meta` correspondente | nenhum |
| Referências de asmdef quebradas / nomes de asmdef duplicados | nenhum |
| Códigos nos assets | `catapult`, `combat_mage`, `crossbow`, `paladin_healer` (únicos) |
| `ProjectSettings/` e `Packages/` | sem alteração (Build Settings intacto; teste confirma que nenhuma cena do build depende dos contratos) |
| Catálogo do jogo | nenhum `UnitData` com os quatro códigos; contratos não estão em `Resources` |
| Arquivos grandes | nenhum (maior arquivo novo: XML de evidência, 47 KB); sem necessidade de Git LFS |

## Pendências reais após a integração

1. **Produção física ainda depende do callback do runtime.** O catálogo e a fila agora aceitam os quatro códigos, e os
   `CharacterData` resolvem os prefabs. A etapa restante é ligar o callback de conclusão da fila ao `Spawn` definitivo de
   cada `CharacterData` na cena, caso o controlador de produção ainda esteja usando somente a lista legada.
2. **Era em runtime.** `AdvancedEraState` é o ponto único usado pelos controladores e pelos contratos, mas ainda precisa ser
   alimentado pela árvore tecnológica/estado de partida quando a progressão de eras for conectada ao jogo final.
3. **Catapulta e balestra mantêm limitações do BT.** O controlador recusa disparo fora do alcance (e, no caso da catapulta,
   abaixo do alcance mínimo), porém a árvore pode continuar em estado de ataque até o alvo ficar válido. A apresentação pode
   ser melhorada com uma regra de aproximação/recuo específica para cerco.
4. **Balanceamento e desempenho.** Os valores de quatro eras são contratos iniciais. Falta playtest de produção completa,
   economia, multiplayer/replay e medição de FPS no player standalone.
5. **Qualidade visual de produção.** As malhas e LODs estão integrados e sem magenta nas evidências existentes, mas a revisão
   AAA final de UV/texel density e a iluminação em todas as eras continuam sendo uma etapa visual separada.
6. **Build Settings.** Não há pendência de adicionar as unidades como cenas. A validação correta é a resolução por
   `Resources`/catálogo durante o build; inserir prefabs individuais em `EditorBuildSettings.asset` seria incorreto.

## Itens encontrados no repositório (não resolvidos aqui)

- **Trabalho do Arqueiro em andamento por outra frente**, ainda não commitado: `Assets/Scripts/Units/UnitManager.cs` e
  `Assets/Tests/Editor/ArcherInfrastructureTests.cs` modificados; `Archer.prefab`, `Archer.asset` e vários scripts em
  `Assets/Scripts/Units/Archer/` novos. Não foram alterados nem incluídos no commit desta etapa. Os 6 testes da suíte
  existente passaram **com** essas alterações presentes na árvore de trabalho.
- **Teste do Arqueiro no HEAD não compilava no asmdef**: a versão commitada de `ArcherInfrastructureTests` usava tipos do
  `Assembly-CSharp`, invisíveis para `EraImperial.EditorTests`. A versão atual, ainda não commitada, usa reflexão e passa.
- **Rascunho anterior desta mesma frente** (não rastreado, sem `.meta`): `Assets/Scripts/Units/Advanced/AdvancedUnitContracts.cs`
  e `Assets/Tests/Editor/AdvancedUnitContractsTests.cs`. O segundo não compilaria no asmdef de testes. Foram **movidos**
  (não apagados) para `Docs/AdvancedUnits/previous-draft/` com extensão `.txt`.
- Nenhum arquivo foi apagado.

## Como reexecutar

- Testes novos: Unity fechado, depois
  `Unity.exe -batchmode -projectPath <projeto> -runTests -testPlatform EditMode -assemblyNames EraImperial.AdvancedUnits.Tests -testResults <xml>`.
- Regerar assets que faltarem: menu *Era Imperial > Advanced Units > Criar assets de contrato* ou
  `-executeMethod EraImperial.Units.Advanced.Editor.AdvancedUnitAssetGenerator.GenerateBatch`.
- Antes de adicionar qualquer unidade ao Build Settings: atribuir prefab, malhas, efeitos, clipes e sockets reais, confirmar
  `IsVisualReady` e `Validate().IsValid` e só então marcar `buildSettingsApproved` (`CanBeAddedToBuildSettings`).
