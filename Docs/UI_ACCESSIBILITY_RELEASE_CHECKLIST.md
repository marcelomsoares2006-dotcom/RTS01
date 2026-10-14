# UI, acessibilidade e release

Este contrato registra o aceite da camada de apresentação do Era Imperial.
As projeções em `Assets/Scripts/UI/UiAccessibilityRules.cs` são determinísticas
e podem ser verificadas sem abrir o Unity. Os painéis reais ainda precisam
consumir essas projeções e ser validados em PlayMode.

## Contratos editor-free

- seleção expõe código, nome, estado semântico e estado de saúde;
- saúde é limitada a `0..1`, nunca produz `NaN` ou infinito e possui faixas
  Saudável, Ferido, Crítico e Destruído;
- minimapa converte posição para coordenadas normalizadas, limita posições fora
  do mapa e informa quando dimensões ou serviço estão indisponíveis;
- áudio expõe volume em dB limitado a `-80..0` e estado Silenciado;
- construção mantém rótulo, status, atalho, ícone e classe de contraste por
  meio de `BuildingConstructionUiRules`.

## Aceite Unity pendente

- conferir navegação por teclado e foco visível em construção, produção,
  tecnologia, seleção e configurações;
- conferir leitura do nome, status e saúde por leitor de tela ou adaptador de
  acessibilidade escolhido para a plataforma;
- conferir contraste, escala de texto e feedback sem depender apenas de cor;
- conferir clique e arraste no minimapa, seleção por caixa, grupos `Ctrl+1..9`
  e tecla `Esc` em resolução mínima suportada;
- conferir sliders de música e efeitos, mute, pausa e restauração após save/load;
- conferir ausência de referências quebradas em seleção, healthbar, minimapa,
  áudio, produção e menu principal;
- executar EditMode, PlayMode, deserialização das cenas e build Windows pelo
  `Tools/Validate-Release.ps1 -WithUnity -Build`.

## Critério de release

O release só passa quando os testes editor-free e Unity estiverem verdes, a
checklist acima tiver evidência em `TestResults/`, e o build puder iniciar uma
partida, selecionar unidades, construir, produzir, salvar, carregar e encerrar
sem erro de runtime.
