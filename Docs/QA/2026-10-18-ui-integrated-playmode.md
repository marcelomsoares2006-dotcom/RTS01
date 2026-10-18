# Auditoria de UI integrada à partida

Data: 2026-10-18

Status: **PASS**

O harness carregou `Core.unity`, `Map1.unity` e `GameScene.unity` somente em memória. Nenhuma cena, prefab ou asset de produção foi salvo ou alterado.

## Evidências

- composição Core + Map1 + GameScene: carregamento iniciado
- Canvas real ativo com GraphicRaycaster
- EventSystem real ativo
- UIManager real com painéis e prefabs ligados
- GameManager real inicializado
- widgets de recursos materializados: 3
- evento UpdatedPlacedBuildingPosition populou a UI real
- eventos reais PlaceBuildingOn/Off consumidos sem exceção
- captura visual solicitada pelo harness, mas não materializada no modo batch sem gráficos (`-nographics`); isso não altera o PASS funcional da integração runtime.

## Conclusão

**PASS funcional:** a UI real está integrada à composição jogável e respondeu aos eventos reais testados.
 A revisão visual manual da captura continua separada, porque o modo batch não produz pixels.

## Arquivos alterados nesta auditoria

- `Assets/Editor/FinalIntegration/UIIntegratedPlayModeAudit.cs`
- `Assets/Editor/FinalIntegration/UIIntegratedPlayModeAudit.cs.meta`
- este relatório em `Docs/QA`

Nenhuma cena, prefab, ScriptableObject, material, textura, malha ou asset de produção foi alterado.

