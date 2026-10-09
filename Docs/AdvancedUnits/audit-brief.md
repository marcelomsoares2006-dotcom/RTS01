# Briefing dos auditores independentes

Projeto: `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido` (Unity 6000.6.3f1). Leia `Docs/AdvancedUnits/agent-brief.md`
(regras, fila do Unity, componentes) e o `RESULTADO.md` / `LICENSE.md` da unidade em `Docs/CharacterMeshAudit/<U>/`.

## Seu papel
Você **não** é quem construiu a unidade. Desconfie do relatório do construtor: **verifique por conta própria**, escrevendo suas **próprias** verificações
(não apenas reexecute `<U>Audit`/`<U>PlayModeCheck` dele; pode usá-los como evidência adicional, mas a conclusão tem de vir das suas).

## Regras
- **Somente leitura nos arquivos da unidade e nos arquivos compartilhados.** Não corrija nada. Defeitos vão para o relatório.
- Você pode criar apenas: `Assets/Editor/AdvancedAudit/<U>IndependentAudit.cs` (+ `.meta` gerado pelo Unity), e arquivos em
  `Docs/AdvancedUnits/audit/<codigo>/` (relatório `audit-<codigo>.md`, imagens, saídas) e `TestResults/`.
- Unity somente pela fila: `powershell -NoProfile -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name <log> -UnityArgs '...'`. Não comite, não faça push,
  não apague nada, não adicione nada ao Build Settings. Nunca exiba/grave chaves.
- Se os scripts do auditor tiverem erro de compilação, corrija os **seus** (e só os seus).

## Checklist obrigatório (cada item: PASS/FAIL/N-A com evidência)
1. Malha existe; origem e licença documentadas e coerentes com os arquivos de entrada (`Tools/CharacterRigging/Inputs/<U>`), sem segredo gravado.
2. Rig humanoide válido (humanóides) / esqueleto genérico coerente (máquinas); Avatar válido (`avatar.isValid && isHuman`) e clipes importados no tipo certo.
3. Pesos normalizados (soma ≈ 1, ≤ 4 influências, sem vértices sem peso) em todos os LODs.
4. UVs presentes e válidas; materiais com shader suportado, texturas atribuídas e nenhum rosa/magenta (cheque shaders `isSupported`, nome `Hidden/InternalErrorShader`
   e amostragem de pixels em renders próprios).
5. LODs presentes, contagem de triângulos **estritamente decrescente**, `LODGroup` com limiares coerentes.
6. Prefab (wrapper) com os componentes corretos: `CharacterManager`, `NavMeshAgent`, `BoxCollider` (tamanho coerente com o modelo), `AdvancedUnitEraBinding`
   (contrato correto), `AdvancedUnitAnimatorDriver`, controlador de ataque/cura correspondente, `AdvancedUnitDeathEffect` com prefab de morte; sem scripts ausentes (`Missing`).
7. Sockets existem nos caminhos do contrato e estão posicionados de forma plausível (ex.: origem do projétil à frente da arma/braço; centro de cura no tronco).
8. Animações importadas e usadas pelo Animator: todas as exigidas pelo contrato, com `AnimationClip` reais, não vazios, duração > 0, e **estados/transições** do controller
   coerentes com os parâmetros (`Speed`, `Fire`/`Hit`, `Damaged`, `Heal`, `Running`, `Attacking`).
9. Projétil separado (quando aplicável): prefab real, contrato de projétil ligado, efeito de impacto real, `AdvancedProjectile` funcional.
10. Contrato aponta para os assets corretos (prefab, malhas, efeitos, clipes, sockets); `Validate().IsValid`; `IsVisualReady`; `CharacterData` em Resources aponta para o mesmo prefab,
    com valores da era Ancient; nenhum outro `CharacterData` duplicado com o mesmo código.
11. Evolução nas quatro eras: troque `AdvancedEraState` e confirme vida máxima, dano, alcance (e cura/raio, dano radial, alcance mínimo, penetração, conforme a unidade) crescendo
    como no contrato, e o **comportamento** respeitando-os (ex.: o paladino cura mais e mais longe; a catapulta atinge mais longe).
12. Comportamento em jogo (suas próprias asserções em Play Mode, preferencialmente no Map1 e no Map2 reais, pelo menu/Core como `Assets/Editor/AdvancedUnitMapCheck.cs`):
    movimento, ataque/cura, recarga/cooldown, dano em inimigos, nenhum dano/cura indevidos, ausência de erros de console.
13. Renders **próprios** de frente, costas e lado (e do efeito/ataque), com contagem de pixels magenta; **abra e examine as imagens** e descreva defeitos visuais reais.
14. Compilação: nenhum erro/aviso do compilador atribuível à unidade nos logs; sem referências quebradas (GUIDs ausentes nos assets/prefabs/controllers da unidade);
    nomes de tipo/arquivo duplicados; `git diff --check` limpo nos arquivos da unidade.
15. Escopo: liste com `git status` quais arquivos a unidade criou/alterou e **aponte qualquer arquivo fora do escopo dela** (Worker, Soldier, Spearman, HeraldicKnight, DarkKnight, Archer,
    arquivos compartilhados, Build Settings).

## Entrega
`Docs/AdvancedUnits/audit/<codigo>/audit-<codigo>.md` com: tabela do checklist (PASS/FAIL), comandos executados, resultados numéricos (nº de verificações aprovadas/falhas),
caminhos das imagens, defeitos encontrados (severidade: bloqueante / importante / menor), e o **veredito**: `PRONTA PARA O JOGO`, `PRONTA COM RESSALVAS` (liste) ou `NÃO PRONTA` (liste).
Devolva um resumo curto com o veredito e os defeitos.
