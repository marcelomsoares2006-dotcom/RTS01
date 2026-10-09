# Briefing dos agentes — integração visual das unidades avançadas

Projeto: `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido` (Unity 6000.6.3f1, Built-in RP, DX11).
Etapa anterior (já commitada, `19d5e16`): contratos de dados, progressão por era, regras de combate e 58 testes.
Leia antes: `Docs/AdvancedUnits/advanced-units-audit.md` e `D:\AI\CONTEXTO-COMPARTILHADO.md`.

## Regras absolutas
- **Não apagar arquivos. Não fazer push. Não fazer commit nem `git add`** (o coordenador comita por unidade).
- Nunca alterar: Worker, Soldier, Spearman, HeraldicKnight, DarkKnight, Archer (nem os trabalhos do Arqueiro ainda não commitados:
  `Assets/Scripts/Units/Archer/*`, `Assets/CharacterRigging/Archer/*`, `Docs/CharacterMeshAudit/Archer*`, `UnitManager.cs`).
- Não editar arquivos de **outra unidade** nem os **arquivos compartilhados** (lista abaixo). Se precisar de uma mudança neles,
  descreva-a no relatório final; não a faça.
- Não adicionar nada ao Build Settings. Não registrar a unidade em `Resources` a menos que o contrato esteja válido **e** o visual 100 % real.
- **Sem placeholders tratados como finais.** Se a qualidade não passar na sua própria revisão visual, declare "bloqueado/parcial" com o motivo.
- Todo asset de terceiros precisa de origem e licença registradas em `Docs/CharacterMeshAudit/<Unidade>/LICENSE.md`
  (modelo: `Docs/CharacterMeshAudit/Archer/LICENSE.md`). Nada de segredo/chave em arquivo algum.
- Português nos textos e comentários, como o restante do projeto.

## Unity: sempre pela fila
Nunca chame `Unity.exe` direto e nunca encerre um Unity. Use:
```
powershell -NoProfile -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name <nome-do-log> -UnityArgs '-quit -executeMethod Classe.Metodo'
```
Só um Unity roda por vez (mutex global); as chamadas esperam. Log em `TestResults\<nome>.log`. Se o log tiver erros de compilação em arquivo
de **outra unidade**, ela está no meio de uma edição: espere ~3 min e repita. Mantenha **seus** scripts compilando; releia o log.
Testes EditMode: `-runTests -testPlatform EditMode -assemblyNames <asmdef> -testResults TestResults\<nome>.xml`.
Blender: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python-exit-code 1 --python <script>` (pode rodar em paralelo).

## Modelo a seguir: o Arqueiro (somente leitura)
`Tools/CharacterRigging/BuildArcher.py` (Meshy rigged glb + clipes → retarget → LODs → FBX/GLB), `BuildArcherAccessories.py` (peças separadas
procedurais), `Build-Archer.ps1`, `Assets/CharacterRigging/Archer/Editor/{ArcherSetup,ArcherAudit,ArcherPlayModeCheck}.cs`,
`Docs/CharacterMeshAudit/Archer/` (evidências). Copie a estrutura e **adapte**, em arquivos próprios da sua unidade.
Convenções do Blender→Unity: metros, `axis_forward=-Z / axis_up=Y`, personagem olhando −Y no Blender (+Z no Unity), 3 LODs com triângulos
estritamente decrescentes (LOD0 ≤ 31 k humanóide; props ≤ 8 k / 3 k / 1 k), pesos normalizados (≤ 4 influências), materiais Standard
(Built-in RP) sem magenta, texturas em `Textures/`. Clipes como `Nome@Clip.fbx`.

## Estrutura de pastas por unidade `<U>` (Mage → `CombatMage`, Paladin → `PaladinHealer`, `Catapult`, `Crossbow`)
- `Tools/CharacterRigging/Build<U>.py` (+ extras) e `Tools/CharacterRigging/Build-<U>.ps1` (reconstrução ponta a ponta)
- fonte bruta fora de Assets: `Tools/CharacterRigging/Inputs/<U>/` e `Working/<U>/`
- `Assets/CharacterRigging/<U>/{Models,Accessories,Textures,Materials,Animation,Prefabs,Data,Editor}`
- Editor: `<U>Setup.cs` (importação, controller, prefabs, vínculo ao contrato, registro condicionado), `<U>Audit.cs`, `<U>PlayModeCheck.cs`
  (métodos estáticos `-executeMethod`, saem com código ≠ 0 em falha)
- wrapper jogável: `Assets/Resources/Prefabs/Units/Characters/<U>.prefab`; catálogo: `Assets/Resources/ScriptableObjects/Units/Characters/<U>.asset`
  (somente via `AdvancedUnitBuilders.RegisterCharacterData`)
- evidências: `Docs/CharacterMeshAudit/<U>/` (renders frente/costas/lado, `unity-audit.txt`, `playmode.txt`, `LICENSE.md`, `RESULTADO.md`)
- testes NUnit EditMode da unidade: `Assets/Tests/Editor/AdvancedUnits/Visual/<U>VisualTests.cs` (asmdef `EraImperial.AdvancedUnits.Tests`; esse
  asmdef **não enxerga o Assembly-CSharp**: acesse componentes por nome, `GetComponent("CharacterManager")`, e `AssetDatabase`)

## Arquivos compartilhados (NÃO editar)
`Assets/Scripts/Units/Unit.cs`, `Assets/Scripts/Units/AdvancedBridge/**` (Runtime e Editor), `Assets/Scripts/Units/{Advanced,Mage,Paladin,Siege,Crossbow}/**`
(contratos e regras), `Assets/Tests/Editor/AdvancedUnits/*.cs` (raiz), `Tools/AdvancedUnits/*`, `Assets/Data/AdvancedUnits/*` (exceto via `AdvancedUnitBuilders.Bind*`).

## Contratos e dados (já existem)
`Assets/Data/AdvancedUnits/{CombatMage,PaladinHealer,Catapult,Crossbow}.asset` e `Projectiles/{CombatMageBolt,CatapultBoulder,CrossbowBolt}.asset`.
Cada contrato lista o que **falta** de visual: `visual.prefab`, `meshes`, `effects`, `sockets` (nome + caminho de Transform), `animations`
(estado + parâmetro do Animator). O contrato só vira "visual pronto" (`IsVisualReady`) quando tudo isso aponta para assets **reais**:
- prefab = o **wrapper** `Resources/Prefabs/Units/Characters/<U>.prefab`; sockets verificados por `Transform.Find` a partir da raiz do wrapper;
- meshes/effects = assets reais por `role`; animations = `AnimationClip` reais por `state`;
- o projétil tem contrato separado (prefab do projétil + efeito de impacto).
Use `AdvancedUnitBuilders` (Editor, Assembly-CSharp-Editor): `BindPrefab`, `BindVisualReference(contract,"meshes"|"effects",role,asset)`,
`BindSocket`, `BindAnimation`, `BindProjectile`, `BuildWrapperRoot`, `SaveWrapper`, `AddStandardParameters`, `ConfigureAgent`, `FindDeep`,
`RelativePath`, `RegisterCharacterData`. **Pode** ajustar nomes de role/state/socket/caminhos esperados no contrato via SerializedObject se
o seu desenho real diferir do proposto, desde que continue cobrindo: malha, efeitos, sockets e animações exigidos pela especificação.
(`Debug`: a lista proposta está em `*Defaults.cs`; edite o **asset**, não o `.cs`.)

## Runtime pronto (Assembly-CSharp, `Assets/Scripts/Units/AdvancedBridge/Runtime`)
- `AdvancedUnitEraBinding` (contract): aplica vida/dano/alcance da era (`Unit.SetEraStats`). `AdvancedEraState.Set(era)`; console: `set_advanced_era 0..3`.
- `AdvancedUnitAnimatorDriver`: parâmetros `Speed` (float, do NavMeshAgent) e `Damaged` (trigger, queda de vida).
- `AdvancedUnitDeathEffect (deathPrefab)`: o jogo destrói a unidade ao zerar a vida; este componente instancia um prefab de morte/destruição
  (visual com animação Death/Destroy tocando sozinha) no lugar.
- `CatapultCombatController`, `ProjectileAttackController` (mago/balestra): `IAttackHandler`; campos: `contract`, `animator`, `projectileSpawn`
  (socket), `releaseDelay` (s do início da animação `Fire` até a saída do projétil), `sourceHeight`. Trigger do Animator: `Fire`.
- `PaladinHealBehaviour (contract, animator, effectOrigin, healEffectPrefab)`: pulso de cura; trigger `Heal`.
- `AdvancedProjectile`: voo reto ou balístico, vindo do `ProjectileContract` (prefab do projétil + efeito de impacto reais).
- **Parâmetros que o behavior tree escreve e o Animator DEVE declarar** (sem transições): `Running`(bool), `Attacking`(bool), `Hit`(trigger).
  `Hit` do BT é o gatilho de ataque corpo a corpo do jogo (Soldier/Paladino usam como ataque). Nas unidades à distância use `Fire`, não `Hit`.
  `Damaged` = reação a dano. `AddStandardParameters` cria todos.
- Wrapper: clone do Soldier (`BuildWrapperRoot`): `CharacterManager`, `NavMeshAgent`, `BoxCollider`, FOV, barra de vida, minimapa; o renderer
  do `UnitManager` é um **anel de time** (define `MeshSize`; escolha o raio pela pegada da unidade) — os componentes novos vão na raiz.
- Estatísticas de jogo por era vêm do contrato (`Assets/Scripts/Units/*/Defaults`); o `CharacterData` recebe os valores Ancient.
- Debug em Play Mode: `instantiate_characters <code> <n>` (console de debug) cria a unidade por código se estiver em `Resources`.

## Relatório final de cada agente (mensagem de retorno)
1. Estado: **COMPLETA** / **PARCIAL** / **BLOQUEADA**, com motivo.
2. Arquivos criados/alterados (caminhos), assets de terceiros usados e licença/origem.
3. Comandos para reconstruir e para validar; resultados (contagem de PASS/FAIL, caminhos das evidências, imagens).
4. Limitações e qualquer mudança que você precisaria nos arquivos compartilhados.
