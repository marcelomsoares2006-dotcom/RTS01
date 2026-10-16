# Auditoria multifrente e roteiro de conclusão — 2026-10-15

## Frentes executadas

### Unity/licenciamento

- Licença Student identificada como atribuída, com expiração registrada para 30/09/2027.
- O bloqueio atual é a comunicação IPC intermitente do `LicenseClient-User`, não ausência de licença.
- A execução batch mais recente excedeu 300 s antes de produzir um novo resultado de EditMode.

### Gameplay e conteúdo

- `Validate-EditorFree.ps1`: passou.
- `Validate-Release.ps1`: passou.
- Fundações: 9/9 aprovadas.
- Unidades militares: arqueiro, cavaleiro e aríete aprovados.
- Produção real no Town Center, NavMesh, combate das unidades avançadas e PlayMode smoke permanecem aprovados pelo baseline registrado.
- Baseline Unity existente: EditMode 391/391, PlayMode smoke 3/3 e build Windows com 0 erros.

### Visual/AAA

- 44 FBX analisados; sem escala inválida, dimensões nulas, normais inválidas ou material rosa por nome.
- Gate visual: 0 linhas inseguras.
- 20 classificações de UV continuam para revisão artística; `Crossbow/Bolt_LOD0`, `Crossbow/Trigger_LOD0` e `HeraldicKnight_LOD2` exigem atenção manual.
- As construções ainda não podem ser declaradas superiores ao Age of Empires IV: faltam família hero para as nove fundações, LOD/collider dentro do Unity, PBR comprovado, showcase em quatro distâncias e composição urbana densa.

## Roteiro obrigatório para o encerramento

1. Abrir o Hub normalmente, confirmar a conta Student e abrir/fechar o projeto pelo fluxo normal para estabilizar o Licensing Client.
2. Executar em sessão fechada: EditMode, PlayMode completo e build Windows; conferir `unit-tests.xml`, `playmode-smoke.txt` e `windows-build.txt` com a mesma rodada.
3. Adicionar/rodar PlayMode dedicado para avanço via árvore tecnológica real e save/load após uma era diferente de Ancient.
4. Rodar playtest standalone com cidade densa, registrar média, mínimo e frame time, e capturar as quatro distâncias visuais.
5. Revisar manualmente as UVs pendentes e confirmar os vínculos PBR/lightmap no Unity.
6. Só então decidir quais construções precisam de nova modelagem; não substituir os prefabs jogáveis por protótipos CityArchitecture sem validação de collider/NavMesh/LOD.
7. Reexecutar todos os fiscais e publicar o commit final.

## Critério de conclusão

O projeto só deve ser marcado como concluído quando os itens acima tiverem evidência nova da mesma rodada. Até lá, o estado correto é **integrado e funcional, mas pendente de validação final e acabamento AAA**.
