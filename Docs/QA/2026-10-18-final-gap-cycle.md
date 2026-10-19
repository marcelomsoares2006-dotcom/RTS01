# Ciclo de fechamento das pendências — 2026-10-18

## Resultado desta rodada

| Frente | Evidência | Estado |
|---|---|---|
| Unity/licença | `TestResults/siege-visual-cycle6-final.log` conectou a `LicenseClient-User` e atualizou a licença | PASS |
| Cerco visual isolado | `SIEGE_VISUAL_CYCLE6_PASS`: GameObjects reais, `Rigidbody`, `SphereCollider`, `BoxCollider`, dano direto/radial, destruição e marcador de impacto | PASS técnico / integração de partida PENDENTE |
| Magenta | Capturas `CityArchitectureShowcase_Overview/Mid/Close` e `TownCenter_Showcase_D3D11` sem áreas magenta | PASS nas capturas |
| PBR/UV/seams | Capturas mostram materiais válidos, mas o piloto PBR ainda não foi promovido; 20 ocorrências UV permanecem classificadas para revisão | REVIEW |
| LOD | Auditoria arquitetônica mantém 23 fragmentos deslocados acima de 1 cm e nenhum `LODGroup` promovido | BLOCKED |
| FPS | O probe anterior mostra média ~59,84 FPS, mas mínimo 14,37 FPS por um pico de 69,6 ms após o warm-up | BLOCKED (<30 mínimo) |
| Benchmark contínuo | Launcher corrigido para argumentos separados; o executável atual encerra antes de criar `Player.log`/JSON (smoke e soak inconclusivos) | BLOCKED |

## Interpretação

O cerco agora tem uma prova de GameObjects físicos e feedback visual, porém a aceitação de produção ainda requer uma sessão real que produza a catapulta pelo `SiegeWorkshop`, dispare contra um `BuildingManager` e registre dano/destruição em cena. O resultado não deve ser confundido com o harness isolado.

As capturas de arquitetura confirmam ausência de magenta no showcase atual, mas não confirmam qualidade AAA ou superioridade ao Age of Empires IV. O LOD continua bloqueado por desalinhamento de hierarquia/pivôs; os candidatos PBR continuam fora de produção até revisão no Unity.

## Próximos gates

1. Regenerar/validar o player Windows em uma sessão gráfica que produza `Player.log`; então coletar e arquivar o JSON/CSV do benchmark de 600 s.
2. Reexecutar PlayMode integrado com produção real de catapulta e alvo estrutural.
3. Corrigir os LODs em cópia isolada, revisar pivôs e só então testar `LODGroup` no Unity.
4. Promover o piloto PBR apenas após captura Unity no Inspector/Game View.
5. Repetir build Windows e playtest final; manter o projeto não concluído enquanto o FPS mínimo ficar abaixo de 30.
