# Limpeza de warnings do Unity — 2026-10-18

Projeto: `RTS01-Corrigido`  
Unity: `6000.6.3f1`  
Escopo: somente scripts de editor em `Assets/Editor` e `Assets/CharacterRigging/*/Editor`.

## Auditoria

`TestResults/windows-build.log` foi revisado. O log não contém diagnósticos `CS` textuais; o único `LogWarning` identificável é emitido pelo pacote `com.unity.pipeline` porque não existe `RuntimePipelineConfig`, e não pertence ao lote de código solicitado.

Também foram revisados os avisos históricos documentados no projeto. O lote aplicável foi:

- chamadas `Object.FindObjectsByType<T>()` sem `FindObjectsSortMode`, cuja sobrecarga é obsoleta no Unity 6;
- variáveis locais/campo sem uso em auditorias editoriais (`CS0219`/`CS0414`), sem alterar a lógica observável dos testes.

## Correções

- As chamadas sem ordenação foram migradas para `FindObjectsByType<T>(FindObjectsSortMode.None)`. Isso mantém a descoberta sem ordenação e usa a API explícita do Unity 6.
- Foram removidas variáveis sem uso em `FoundationBuildingAuthoringWindow`, `CatapultIndependentAudit` e `CombatMageIndependentAudit`.
- Não foram alterados gameplay, prefabs, cenas, assets visuais, Build Settings ou Project Settings.
- `Projector` e `AssetDatabase.ExportPackage` foram apenas auditados e não alterados: não há evidência desses itens no `windows-build.log` como warnings deste lote, e substituí-los exigiria validar o comportamento visual/empacotamento fora do escopo.

## Validação

- `Tools/Validate-EditorFree.ps1 -SkipReleasePreflight`: **PASS** — Unity 6000.6.3f1, 5 cenas de build, 43 contratos de construção, 11 entradas de catálogo e 1675 assets indexados.
- `git diff --check`: **PASS**; apenas avisos de normalização LF/CRLF do Git foram emitidos.
- Busca final por `FindObjectsByType<T>()` sem argumento de ordenação nos diretórios-alvo: **nenhuma ocorrência**.
- Compilação Unity/EditMode: **não executada**. Não há `Unity.exe` instalado neste host; o Unity Hub está presente, mas não há Editor disponível para uma execução batch segura.

## Arquivos alterados nesta rodada

- `Assets/CharacterRigging/CombatMage/Editor/CombatMageAudit.cs`
- `Assets/Editor/AdvancedAudit/CatapultIndependentAudit.cs`
- `Assets/Editor/AdvancedAudit/CombatMageIndependentAudit.cs`
- `Assets/Editor/EraImperialSmokeTests.cs`
- `Assets/Editor/FoundationBuildingAuthoringWindow.cs`
- `Assets/Editor/Stage345RuntimeCheck.cs`
- `Assets/Editor/Stage5MountedLocomotionRuntimeCheck.cs`
- `Assets/Editor/Stage5MountedNavMeshRuntimeCheck.cs`
- `Assets/Editor/Stage5MountedRuntimeCheck.cs`
- `Docs/QA/2026-10-18-warning-cleanup.md`

Alterações locais preexistentes em outros arquivos foram preservadas e não fazem parte desta limpeza.
