# Architecture showcase — próxima composição procedural

Data: 2026-10-17

## Escopo executado

Esta rodada alterou somente:

- `Assets/Editor/CityArchitecture/CityArchitectureShowcaseBuilder.cs`
- `Docs/AAA/VisualAudit/2026-10-17-architecture-showcase-next.md`

O Unity Editor estava aberto no projeto. Por isso o builder não foi executado,
a cena `Assets/Scenes/CityArchitectureShowcase.unity` não foi regenerada e não
foram produzidas capturas novas. Nenhum arquivo foi apagado.

## Melhoria de composição

O lote `CityDensity_VisualVolumes` continua determinístico com 100 volumes numa
grade 10×10, mas deixou de clonar os quatro prefabs arquiteturais para a
composição visual. Cada volume agora é montado no próprio builder, sem vínculo
de prefab, usando:

- plinto de pedra, corpo contínuo e faixa de beiral;
- telhado procedural de duas águas com cumeeira explícita;
- porta frontal com painel e moldura;
- janelas frontais com vidro escuro, molduras e, nos pontos hero, travessas;
- escala RTS consistente e rotação determinística para os volumes de fundo.

Quatro células hero foram reservadas ao redor do Town Center, com escala maior
e frente orientada para a câmera:

| Arquétipo | Leitura adicional | Props urbanos |
| --- | --- | --- |
| House | janela de sótão e chaminé | barril e pote |
| Barracks | viga frontal, seteiras e banner | lança, estante e barril |
| Market | toldo, postes e balcão contínuo | caixas e cesto |
| Temple | colunas, entablamento e frontão | dois braseiros e remate |

Os outros 96 volumes usam a mesma linguagem de massa contínua, porta, duas
janelas e telhado, mas com menos detalhes para manter a densidade visual
controlável. O builder continua removendo componentes que não sejam visuais
dos clones do kit de muralhas e os primitivos novos já nascem sem collider por
meio de `AddShowcaseBlock`/`AddShowcaseCylinder`.

## Segurança de produção

- Não foram alterados prefabs, FBX, materiais de produção ou scripts de gameplay.
- O builder não chama `SaveAsPrefabAsset`, `ApplyPrefabInstance` ou
  `SavePrefabAsset`.
- A cena permanece um showcase visual; não há promoção dos quatro edifícios
  para gameplay, placement, NavMesh ou colisão.
- LODs de produção não foram integrados. A geometria é procedural da cena de
  apresentação e não corrige os pivôs/fragmentos dos LODs auditados.

## Verificações desta rodada

- `git diff --check` no builder: **PASS**.
- Chaves balanceadas no builder após a edição: **76/76**.
- Revisão estática confirmou quatro células hero, 100 volumes procedurais,
  portas, janelas, telhados e nomes de props urbanos.
- A validação Unity, regeneração da cena e captura multi-view ficam pendentes
  até o Editor estar fechado. Não há alegação de compilação ou inspeção visual
  runtime nesta rodada.

## Próximo gate seguro

Com o Editor fechado, executar o builder em uma janela controlada, conferir a
captura Overview/Mid/Close e revisar o diff. O gate deve reprovar qualquer
alteração em `Assets/**/*.prefab`, `Assets/**/*.fbx`, `Assets/Resources/Prefabs`
ou `Assets/CityArchitecture/Prefabs`.
