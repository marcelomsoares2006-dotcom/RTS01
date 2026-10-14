# RTS01 — roteiro de avaliação e conclusão

Este é o caminho oficial para avaliar o estado do projeto sempre que uma nova máquina assumir o trabalho. Execute as etapas na ordem e só avance quando o critério da etapa anterior estiver aprovado.

## 1. Atualizar o código

```powershell
git clone https://github.com/marcelomsoares2006-dotcom/RTS01.git
cd RTS01
git checkout master
git pull --ff-only origin master
git status --short
git log -1 --oneline
```

O `git status` deve ficar limpo antes da avaliação. Se houver alterações locais, salve-as em uma branch antes de continuar.

## 2. Confirmar o ambiente

O projeto usa Unity `6000.6.3f1`. Confirme que o executável existe:

```powershell
Test-Path 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe'
node --version
```

Sem o Unity, execute as validações editor-free das etapas seguintes e registre o bloqueio; não promova construções manualmente.

## 3. Rodar os gates editor-free

Na raiz do projeto:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Validate-EditorFree.ps1
powershell -ExecutionPolicy Bypass -File .\Tools\Validate-Release.ps1
node .\Tools\ValidateFoundationObj.mjs
node .\Tools\ValidateFoundationGlb.mjs
node .\Tools\ValidateMilitaryUnitAssets.mjs
node .\Tools\ValidateMilitaryUnitVisualAssets.mjs
powershell -ExecutionPolicy Bypass -File .\Tools\Validate-FoundationAssets.ps1 -ReportOnly
```

Critérios editor-free atuais:

- `Validate-EditorFree.ps1` aprovado.
- `Validate-Release.ps1` aprovado.
- Nove OBJ/GLB fundacionais válidos.
- Archer, Horseman e Battering Ram com assets, prefabs e bindings válidos.
- O relatório de fundações pode continuar listando 27 pendências até a etapa Unity.

## 4. Abrir e preparar no Unity

Abra `D:\RTS01` no Unity `6000.6.3f1` e aguarde a importação terminar. Em seguida:

1. Abra `Tools > Era Imperial > Buildings > Foundation Authoring`.
2. Importe/revise os nove modelos do manifesto `Docs/FOUNDATION_IMPORT_MANIFEST.yml`.
3. Gere ou revise os drafts de Town Center, Resource Dropoff, Lumber Camp, Mining Camp, Mill, Barracks, Archery Range, Stable e Siege Workshop.
4. Use o fluxo de promoção para criar os prefabs finais em `Assets/Resources/Prefabs/Units/Buildings/`.
5. Confirme BuildingManager, BuildingBT, MeshFilter/MeshRenderer, collider, Rigidbody, NavMeshObstacle, camada `Unit`, tag `Unit` e footprint de colocação.
6. Confirme os GUIDs de BuildingData, `prefab` e `constructionMeshes` no catálogo.

## 5. Zerar o gate de fundações

Execute novamente:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Validate-FoundationAssets.ps1
```

O critério de aprovação é saída sem issues. Cada entrada deve estar em `Playable`, possuir BuildingData resolvível e possuir prefab final com `.meta`.

## 6. Testes Unity

No Unity Test Runner, execute todos os testes EditMode e PlayMode. Depois valide manualmente:

- colocar, girar, bloquear, cancelar, reparar, demolir e destruir construções;
- custos, população, dependências e produção militar;
- save/load e replay determinístico;
- recursos, pontos de coleta, NavMesh e seleção;
- combate, cerco, saúde, minimapa, áudio e acessibilidade;
- deserialização das cinco cenas de build.

## 7. Gerar e verificar o build

Use a configuração de build do projeto, gere o player para uma pasta fora de `Assets` e faça um teste limpo de inicialização, nova partida, save/load e encerramento. Registre plataforma, versão do Unity e resultado dos testes.

## 8. Registrar e enviar o marco

Antes de enviar:

```powershell
git diff --check
git status --short
git add <arquivos-revisados>
git commit -m "<marco concluído>"
git push origin master
```

Atualize `D:\Cerebro\Cerebro\20-Projetos\RTS01.md` com: commit, gates executados, contagem de contratos/assets, testes Unity, build gerado e pendências restantes.

## Formato para futuras avaliações

Toda avaliação do Git deve responder nesta ordem:

1. commit/branch e estado do worktree;
2. gates editor-free e validadores de assets;
3. situação das 9 fundações;
4. resultado Unity Test Runner e build;
5. pendências objetivas;
6. próximo marco e commit esperado.

Nunca marque o projeto como concluído enquanto o gate de fundações, os testes Unity e o build final não estiverem aprovados.
