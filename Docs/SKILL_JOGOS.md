# Desenvolvimento do Era Imperial com a skill de jogos

Skill adotada: `thrixel:build-world`, com as referências específicas de Unity.
Projeto existente: Unity 6000.6.3f1, Windows, Built-in Render Pipeline.
Não criar outro projeto nem trocar o pipeline apenas por usar a skill.

## Integração

- Unity CLI encontrado em `C:\Program Files\Unity Hub\resources\cli\unity.exe`.
- Integração oficial adicionada pelo comando `pipeline install`: `com.unity.pipeline` 0.8.0-exp.1. É uma versão experimental; validar antes de depender dela.
- Ferramentas remotas de geração Thrixel indisponíveis nesta sessão. Nenhum modelo gerado, crédito consumido ou publicação solicitada.
- A licença de estudante do Unity não comprova assinatura ou saldo no Thrixel.

Verificação após a instalação em 28/09/2026: três testes NUnit aprovados, três cenários de partida aprovados e zero erros de execução capturados. As seis imagens de jogo/minimapa apresentaram zero pixels magenta segundo o teste. Evidências: `TestResults/unit-tests.xml` e `TestResults/playmode-smoke.txt`. A integração compilou nesses testes; o controle de uma sessão interativa pelo CLI ainda não foi confirmado (`status` não encontrou uma instância conectada durante a verificação).

## Ordem de trabalho

1. Preservar os terrenos, referências, saves e correções existentes; repetir os testes de regressão após alterações.
2. Corrigir o acabamento das árvores e o aviso de billboard antes de ampliar a quantidade de assets.
3. Inspecionar câmera, terreno, água, construções, unidades e minimapa em Scene e Play; cumprir a inspeção da skill por pelo menos dez ângulos. Essa etapa ampliada ainda está pendente.
4. Usar o roteiro automatizado de partida existente como base e acrescentar verificações de construção, coleta e combate. Registrar pelo menos cinco imagens ao longo do roteiro e revisar os resultados visuais.
5. Medir desempenho em uma execução sem testes concorrentes. Não declarar FPS ou qualidade visual sem medição e inspeção.

## Quando a geração de assets estiver conectada

Antes de gerar, consultar saldo/plano e preços reais; apresentar a escolha exigida pela skill caso a conta seja gratuita. Não comprar créditos automaticamente.

Prioridade visual: árvores coerentes com o terreno, construção principal, construções econômicas e adereços. Aproveitar os assets atuais até existir uma necessidade concreta de substituição. Manter estilo low-poly coerente, proporções verificadas ao lado das unidades existentes e orçamento de geometria definido antes da geração.

Importar FBX, agrupar peças estáticas antes da importação e preservar pivôs de peças móveis. Verificar orientação, escala, materiais, colisões e geometria tanto no asset isolado quanto na partida. Não substituir referências válidas sem mapear os GUIDs e preservar os dados existentes.

## Limites

- Os três testes unitários e os três cenários de integração não cobrem o jogo inteiro.
- A inspeção visual ampliada da skill não está concluída apenas porque esses testes passam.
- Publicação pública, troca de engine e alterações na segurança do Windows não fazem parte desta configuração.
- O problema de execução como administrador é tratado separadamente; nenhuma alteração de UAC foi autorizada ou realizada aqui.

Resultados e procedimento de regressão: `REVISAO_UNITY6.md` e `Tools/Validate-Project.ps1`.
