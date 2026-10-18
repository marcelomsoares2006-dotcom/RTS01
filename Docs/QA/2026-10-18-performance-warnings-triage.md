# Triagem independente de desempenho e warnings

Data: 2026-10-18  
Projeto: `RTS01-Corrigido`  
Unity: `6000.6.3f1`  

## Escopo

Foram lidos `TestResults/windows-build.txt`,
`TestResults/standalone-runtime.log` e os logs de compilação/auditoria com
avisos `CS` e `UAC`. A triagem separa código de produção, ferramentas de
editor, testes e compatibilidade de saves. Nenhum aviso foi silenciado.

## Resultado da evidência anterior

| Evidência | Resultado | Interpretação |
|---|---:|---|
| Build Windows | Succeeded / 0 erros / 20 warnings | O player foi gerado; os warnings precisam de redução gradual. |
| Player standalone | 307 frames / 8,01 s | Execução encerrou normalmente. |
| FPS bruto mínimo | 7,10 | Amostra de inicialização, não representa o regime sustentado. |
| FPS médio bruto | 59,69 | Série estabilizada próxima do limite de 60. |
| FPS observado após o carregamento | aproximadamente 59,2–60,0 | Não há evidência, neste log, de queda sustentada abaixo de 30 FPS. |

O log contém uma amostra inicial em `t=2,7s` com `frames=0` e depois saltos
para aproximadamente 59 FPS. O mínimo 7,10 veio de uma amostra curta de
startup/primeiro frame e deve continuar sendo preservado como dado bruto, mas
não deve ser usado sozinho como FPS sustentado.

## Correções seguras aplicadas

As APIs obsoletas de busca foram atualizadas somente no runtime:

- `GameManager.cs`: `FindObjectsByType` com exclusão explícita de objetos inativos.
- `Globals.cs`: mesma atualização para descoberta de `NavMeshSurface`.
- `EventManager.cs`: `FindAnyObjectByType<EventManager>()`.
- `CoreBooter.cs`: `FindAnyObjectByType<AudioListener>()`.
- `DataHandler.cs`: `FindAnyObjectByType<CameraManager>()`.

Essas mudanças preservam a intenção existente e não alteram ordem de gameplay,
save/load ou conteúdo de cena. Ferramentas de editor e harnesses não foram
alterados nesta triagem porque não são caminho de produção e algumas dependem
de APIs/fixtures específicas.

## Ajuste isolado de benchmark

`StandalonePerformanceProbe.cs` agora usa 2 s de aquecimento antes de coletar
a série sustentada. O player continua com o mesmo alvo de 60 FPS e a mesma
duração; não há mudança de qualidade, câmera, economia ou combate. O JSON passa
a registrar `warmupSeconds=2.00`, enquanto `fpsMin`, `fpsAverage` e `fpsMax`
continuam sendo calculados sem mascarar uma queda ocorrida depois do
aquecimento. O log bruto anterior permanece evidência histórica.

## Warnings que não foram alterados

- `BinaryFormatter`/`UAC0023`: permanece isolado no leitor legado. Não foi
  migrado nesta etapa porque isso exige teste de compatibilidade com saves
  existentes; o caminho seguro é somente leitura legada + escrita V2.
- `Projector` e `AssetDatabase.ExportPackage`: aparecem em ferramentas de
  editor/empacotamento; substituição requer confirmar a API de pacote e o
  comportamento visual da seleção. Não foram mascarados.
- `CS0219`/`CS0414` em auditorias e checks: são código de validação, fora do
  runtime de produção; permanecem visíveis para uma rodada específica de
  limpeza dos harnesses.

## Limitação de confirmação

No momento da triagem havia instâncias do Unity/Unity Hub abertas. Portanto a
recompilação batch não foi forçada sobre um projeto potencialmente em uso.
É necessário executar, com o Editor fechado:

```powershell
.\Tools\Validate-Project.ps1 -Build
.\Tools\RuntimeStandaloneAudit\Invoke-RuntimeStandaloneAudit.ps1
```

Depois disso, conferir `TestResults/windows-build.txt` e o novo JSON de runtime.
O critério de desempenho sustentado recomendado é `fpsMin >= 30` após o
aquecimento; o valor bruto de startup deve ser reportado separadamente.

## Arquivos alterados nesta rodada

- `Assets/Scripts/Managers/GameManager.cs`
- `Assets/Scripts/Managers/EventManager.cs`
- `Assets/Scripts/Core/CoreBooter.cs`
- `Assets/Scripts/Tools/Globals.cs`
- `Assets/Scripts/Tools/DataHandler.cs`
- `Assets/Scripts/Tools/StandalonePerformanceProbe.cs`
- `Docs/QA/2026-10-18-performance-warnings-triage.md`

Alterações preexistentes locais, incluindo auditorias AAA/LOD, backups `.blend1`
e sidecars, foram preservadas e não foram incluídas nesta triagem.
