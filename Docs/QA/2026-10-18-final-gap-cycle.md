# Ciclo de fechamento das pendências — 2026-10-18

## Resultado desta rodada

| Frente | Evidência | Estado |
|---|---|---|
| Unity/licença | `TestResults/siege-visual-cycle6-final.log` conectou a `LicenseClient-User` e atualizou a licença | PASS |
| Cerco integrado | `LIVE_PRODUCTION_SIEGE_PASS`: SiegeWorkshop produziu catapulta real; projétil balístico causou 4 impactos (360→260→160→60) e destruiu `BuildingManager` inimigo | PASS |
| Magenta | Capturas `CityArchitectureShowcase_Overview/Mid/Close` e `TownCenter_Showcase_D3D11` sem áreas magenta | PASS nas capturas |
| PBR/UV/seams | House PBR capturado no Unity e promovido com backup; 20 ocorrências UV continuam classificadas para revisão manual | House PASS / UV REVIEW |
| LOD | Auditoria Blender Round7: produção=0 e candidato=0 fragmentos deslocados; revisão visual Unity de popping ainda pendente | STATIC PASS / VISUAL REVIEW |
| FPS | Smoke otimizado de 30,01 s: média 60,00 FPS, mínimo 50,28 FPS. Soak anterior de 600,02 s: mínimo 1,29 FPS; novo soak otimizado encerrou antes do JSON | SMOKE PASS / 600 s PENDING |
| Benchmark contínuo | Player Windows concluiu 600,02 s com `EXIT=0`, JSON/CSV válidos e sem padrões de crash no log | PASS de duração / BLOCKED de FPS |

## Interpretação

O cerco agora tem uma prova de GameObjects físicos e feedback visual, porém a aceitação de produção ainda requer uma sessão real que produza a catapulta pelo `SiegeWorkshop`, dispare contra um `BuildingManager` e registre dano/destruição em cena. O resultado não deve ser confundido com o harness isolado.

As capturas de arquitetura confirmam ausência de magenta no showcase atual, mas não confirmam qualidade AAA ou superioridade ao Age of Empires IV. O LOD continua bloqueado por desalinhamento de hierarquia/pivôs; os candidatos PBR continuam fora de produção até revisão no Unity.

## Próximos gates

1. Reexecutar o soak otimizado de 600 s até gerar JSON/CSV completos e confirmar mínimo ≥30 FPS.
2. Fazer a revisão visual Unity dos LODs Round7, UVs, seams, pivôs e materiais restantes.
3. Executar playtest manual completo de UI, eras, combate, cerco, destruição, save/load e NavMesh.
4. Repetir build Windows final após fechar o soak e as revisões visuais.
