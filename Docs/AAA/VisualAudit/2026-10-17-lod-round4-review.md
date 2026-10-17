# Revisão LOD Round4 — alinhamento sem deslocar pivôs

## Resultado

**PASS técnico isolado; NÃO PROMOVIDO para produção.**

Foi corrigido o método de exportação para preservar `location`, `rotation` e
`scale` dos fragmentos. A geometria é alinhada no espaço local da malha,
comparando cada fragmento do LOD com o LOD0 correspondente. Os oito FBX foram
gerados somente em `TestResults/LOD-Generation-Round4/`.

## Evidências

- 4 edifícios × 2 níveis: 8 candidatos gerados.
- Pivôs desalinhados: **0**.
- Erro máximo de pivô: **0 m**.
- Fragmentos acima de 1 cm no centro: **0**.
- Erro máximo de centro: **0,000003 m**.
- Render offline House LOD1: `TestResults/LOD-Generation-Round4/House_Medieval_LOD-comparison.png`.
- Auditoria JSON: `Docs/AAA/VisualAudit/2026-10-17-uv-lod-audit.json`.

## Limite do gate

O Unity não executou a captura visual PBR/LOD nesta rodada porque o batch
ficou bloqueado no canal `LicenseClient-User`. Portanto os candidatos não
foram copiados para `Assets/CityArchitecture/Generated/LOD`, nenhum prefab foi
alterado e ainda não há aprovação visual final no Inspector/Scene/Game View.

## Próximo gate

Com o Licensing Client estável, importar os oito candidatos em uma cena de
revisão, alternar LOD0/LOD1/LOD2 em câmera próxima e distante, conferir
silhueta, popping, materiais e UVs, e só então preparar um pacote de promoção
revisável. A meta não é declarar superioridade ao Age of Empires IV sem essa
evidência.
