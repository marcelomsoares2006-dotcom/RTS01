# Gate visual comparativo — Era Imperial x Age of Empires IV

Data: 2026-10-16

Este documento define o primeiro gate visual do roteiro. A comparação deve usar a mesma resolução, campo de visão aproximado, distância de câmera e categoria de cena. Não é permitido declarar superioridade usando apenas testes automatizados.

## Referências

- [Galeria oficial de wallpapers do Age of Empires](https://www.ageofempires.com/downloadable-wallpapers/)
- [Requisitos oficiais do Age of Empires IV](https://support.ageofempires.com/hc/en-us/related/click?data=BAh7Cjo%3D)
- [Opções oficiais de qualidade gráfica](https://support.ageofempires.com/hc/en-us/articles/7473728312724-Tips-for-Optimizing-Age-of-Empires-IV)

## Categorias do gate

| Categoria | Evidência exigida | Estado inicial |
|---|---|---|
| Silhueta arquitetônica | captura próxima e média de cada prédio | PENDENTE |
| PBR | albedo, normal, roughness, metallic/AO quando aplicável | PENDENTE |
| UV/topologia | auditoria automática + revisão manual dos casos críticos | PENDENTE |
| LOD | LOD0/LOD1/LOD2 e transição sem popping | PENDENTE |
| Cidade densa | 100+ construções, ruas, props, vegetação e FPS | PENDENTE |
| Iluminação | dia, sombra, contraste, atmosfera e leitura estratégica | PENDENTE |
| Unidades/cerco | batalha com efeitos, projéteis e feedback de dano | PENDENTE |
| Interface | HUD, minimapa, produção, eras e feedback | PENDENTE |
| Estabilidade | EditMode, PlayMode, build, standalone e save/load | PASS |

## Primeiro piloto

O Town Center será a primeira construção a receber acabamento em cópia isolada. A promoção para produção só ocorrerá se passar por modelagem, UV, PBR, LOD, collider, NavMesh, captura visual e validação Unity.

## Regra de promoção

Uma melhoria visual não pode substituir o prefab jogável diretamente. O fluxo obrigatório é: cópia isolada → captura A/B → auditoria fiscal → teste Unity → promoção explícita.
