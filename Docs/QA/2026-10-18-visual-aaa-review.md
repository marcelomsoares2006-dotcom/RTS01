# Revisão visual AAA — Unity

Data: 2026-10-18  
Escopo: revisão somente leitura do projeto `RTS01-Corrigido`. Nenhum prefab, cena de produção ou mesh de produção foi alterado ou promovido nesta revisão.

## Resultado executivo

**Status: REVISÃO PARCIAL — não declarar superioridade visual ao Age of Empires IV.**

Os gates técnicos de importação, ausência de material rosa por integridade de asset, PBR piloto, LOD geométrico e pipeline Built-in passaram. A inspeção visual das capturas Unity, porém, encontrou qualidade de apresentação abaixo da meta AAA: fachadas repetitivas e modulares, pouca variação de props, iluminação plana e composição de cidade ainda demonstrativa. O projeto continua jogável, mas o gate comparativo com AoE4 permanece aberto.

## Evidências executadas

| Frente | Evidência | Resultado |
|---|---|---|
| Integridade visual de assets | `Tools/CityArchitecture/Validate-AAAVisualGate.ps1` + Blender 5.2.1 | PASS técnico: 44 FBX, 0 linhas inseguras; 20 ocorrências UV classificadas para revisão |
| LOD arquitetônico | `Tools/CityArchitecture/AuditBuildingLODs.py` | PASS geométrico: produção 0 deslocamentos, candidato 0; 32 ocorrências do conjunto legado foram separadas como não comparáveis |
| Pipeline Unity | `AAARenderPipelineAudit.Run` em Unity 6000.6.3f1 | PASS: Built-in RP, qualidade Ultra, HDR/MSAA suportados, sem mutação global |
| PBR House | `TestResults/HousePBRPilot/candidate.png` e `capture.log` | PASS de preview: materiais Standard com albedo/normal/metallic-gloss/AO; `House.prefab` não foi salvo |
| Cidade densa | `TestResults/CityArchitectureShowcaseAudit.txt` | PASS estrutural da cópia visual: 100 volumes, 476 renderers, 34.096 triângulos estimados, 0 colliders e 0 MonoBehaviours |
| Capturas Unity | Overview/Mid/Close e cenário de jogo | PASS de captura; revisão artística ainda reprovada para o alvo AAA |

O batch Unity foi executado em modo `-nographics` para a auditoria do pipeline e registrou `AAA_RENDER_AUDIT_PASSED`. Não foi criada uma nova captura gráfica nesse processo; foram reutilizadas e inspecionadas as capturas Unity já presentes em `TestResults` e `Docs/CharacterMeshAudit`. A captura gráfica de uma sessão jogável limpa continua pendente e está listada como bloqueio, sem inventar uma aprovação visual.

## Revisão visual manual

### PBR e materiais

- A captura do piloto House mostra contraste entre madeira, telhado e elementos metálicos, sem magenta.
- O uso de mapas normal, metallic-gloss e AO foi confirmado no candidato isolado.
- A cidade showcase tem leitura de cor coerente, mas os materiais ainda têm aparência estilizada/plana em escala média; faltam variação de roughness, desgaste, sujeira, decals e maior riqueza de microdetalhe.
- Nenhum material foi promovido ao prefab de produção nesta etapa.

### LODs

- A auditoria de produção agora registra `production=0` deslocamentos e pivôs alinhados dentro do limiar.
- As capturas de LOD do cavaleiro e do cervo preservam silhueta e enquadramento entre LOD0/LOD1/LOD2, sem material rosa visível.
- Ainda falta uma revisão Unity de transição em movimento e distância de câmera real para confirmar ausência de popping; o gate geométrico sozinho não prova qualidade perceptual.

### UVs

- As 20 ocorrências permanecem classificadas, sem normalização automática.
- Repetições em peças de madeira/ferro/corda são compatíveis com tiling intencional.
- Permanecem casos que exigem inspeção manual: `Crossbow/Bolt_LOD0`, `Crossbow/Trigger_LOD0` e a ilha marginal do `HeraldicKnight_LOD2`.
- Portanto, UVs: **tecnicamente auditadas, manualmente incompletas**.

### Materiais rosas e enquadramento

- Nenhum rosa/magenta foi observado nas capturas PBR, LOD e unidades revisadas.
- A captura de jogo disponível mostra o mapa e a vegetação, mas também exibe overlays de colocação; não deve ser usada como captura final de apresentação.
- As capturas de cidade têm enquadramento válido, mas céu vazio, terreno uniforme e baixa densidade de elementos de ambientação reduzem a leitura AAA.

## Comparação com o gate AoE4

O documento `Docs/AAA/VisualAudit/COMPARISON_GATE_AOE4.md` exige comparação por categoria e mesma condição de câmera. Nesta rodada:

- Silhueta arquitetônica: **parcial** — legível, mas repetitiva e simples.
- PBR: **parcial** — piloto aprovado, cobertura de produção e variação de materiais ainda insuficientes.
- UV/topologia: **parcial** — gate automático passou, revisão manual pendente.
- LOD: **parcial positivo** — alinhamento técnico passou; popping em gameplay ainda não foi revisado.
- Cidade densa: **não aprovado** — 100 volumes são composição visual de teste, não uma cidade AAA final.
- Iluminação: **não aprovado** — falta atmosfera, sombras e variação de iluminação de apresentação.
- Unidades/cerco: **não coberto por esta revisão visual**; usar os relatórios de PlayMode e capturas específicas já existentes.
- Interface: **não coberto por captura gráfica nesta rodada**; a auditoria funcional de UI existe separadamente.

## Bloqueios e próximos passos

1. Criar uma captura Unity gráfica real da cena jogável sem overlays de colocação, com dia/noite ou iluminação de apresentação e câmera comparável.
2. Revisar no Inspector/Game View os três casos UV pendentes e confirmar as transições LOD em movimento.
3. Substituir volumes repetidos por variações de fachadas, props, portas, janelas, telhados, terreno e vegetação com maior diversidade.
4. Ampliar o piloto PBR para as construções principais somente após captura A/B e aprovação visual.
5. Repetir a comparação AoE4 por categoria; só então considerar o gate “melhor que AoE4”.

## Preservação

- Nenhum prefab, mesh ou cena de produção foi editado.
- Alterações antigas não relacionadas, backups `.blend1` e arquivos fora do escopo permanecem preservados.
- Não foi criado commit nesta etapa, conforme solicitado.

