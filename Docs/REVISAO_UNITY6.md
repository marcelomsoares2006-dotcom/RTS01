# Revisão técnica — Era Imperial / Unity 6

Data: 28/09/2026. Editor validado: **6000.6.3f1**, DirectX 11, Radeon RX 580.

## Correções implementadas

- Referências originais dos componentes NavMesh restauradas pelos GUIDs usados nas cenas.
- Terreno do Map2 recolocado na camada Terrain, usada tanto pela navegação quanto pelos cliques.
- NavMesh dos dois mapas reconstruído: 4.171 vértices no Map1 e 18.580 no Map2.
- Material explícito `Nature/Terrain/Standard` salvo e ligado aos terrenos. O reparo de execução não apaga materiais válidos nem reescreve TerrainData.
- Shader de névoa de guerra corrigido para DirectX 11; névoa reativada no padrão do projeto.
- Recuperadas 5.916 posições de árvores do Map1 a partir do histórico, sem substituir alturas ou pintura atuais. Referências de prefab ausentes usam `Tree Type7 01` como substituto; não se afirma que essa espécie era a original. Map2 preserva suas 19.030 instâncias.
- Removido o comportamento que apagava todas as árvores quando apenas uma referência estava quebrada.
- Console persistente isolado: ele não conserva mais os gerenciadores de uma partida encerrada.
- Instância do GameManager disponível em Awake; estado de seleção, unidades e multiplicadores reinicializado entre partidas.
- CoreBooter remove corretamente seus eventos e permite retornar ao menu mesmo com o jogo pausado.
- Minimapa não é mais desativado automaticamente.
- Nome do arquivo RuntimeBehaviorTree alinhado à classe, preservando seu GUID.
- Ferramenta de metadados isolada do código de player; menu usa APIs públicas do editor.
- Save dos parâmetros de jogadores gravado na mesma pasta de onde é carregado. Streams de GameData são fechados mesmo em exceções.
- Ausência de GameData em uma partida nova tratada como estado esperado, não como falha.
- Segundo bônus de ataque aplica a proporção correta, sem multiplicar cumulativamente por oito.
- Play no editor inicia pela cena Core mesmo se um mapa estiver selecionado.

## Testes reais

Três testes NUnit/EditMode em `Assets/Tests/Editor/TerrainCompatibilityTests.cs`:

1. Preservação das camadas pintadas e reposição somente das referências ausentes.
2. Preservação da ordem e dos prefabs válidos das árvores.
3. Reposição de material ausente/com shader de erro sem substituir material válido.

Resultado: **3 aprovados, 0 falhas**. Evidência: `TestResults/unit-tests.xml`.

Também foram executados três cenários de integração em Play Mode:

1. Nova partida no Map1, movimento de trabalhador, salvar e voltar ao menu com o jogo pausado.
2. Nova partida no Map2, movimento de trabalhador e retorno ao menu.
3. Recarregar o save do Map1, incluindo o trabalhador salvo, e retornar ao menu.

Resultado: **3 cenários aprovados, 0 erros de execução capturados**. A rodada final inclui névoa de guerra ativa. As seis imagens das câmeras do jogo/minimapa apresentaram 0% de pixels magenta pelo critério registrado no teste e foram inspecionadas. São renders reais do Unity, não capturas da interface do editor.

Evidências: `TestResults/playmode-smoke.txt`, `TestResults/playmode.log`, `TestResults/scenario-*-game.png` e `TestResults/scenario-*-minimap.png`.

Auditoria: cinco cenas de build, zero scripts ausentes e zero materiais incompatíveis detectados. Evidência: `TestResults/migration-audit.txt`.

## Build Windows

Compilação concluída: **Succeeded, 0 erros e 25 avisos**. Executável: `Builds/Windows/EraImperial.exe`. Evidências: `TestResults/windows-build.txt` e `TestResults/windows-build.log`.

Os avisos incluem APIs antigas, BinaryFormatter e shader de billboard das árvores. Não foram ocultados por supressão de mensagens. A interface do editor foi inspecionada pela habilidade de controle do computador. A tentativa de abrir o executável por essa habilidade teve o prazo de aprovação expirado; a compilação bem-sucedida não deve ser confundida com teste visual concluído no player Windows.

Verificação visual adicional: posteriormente, a janela real do editor mostrou Map1 em Play, terreno/construção/HUD renderizados sem rosa, Console com zero erros e um aviso de shader de árvores. Foi detectada interação do usuário, então a partida foi deixada aberta sem novos comandos. Isso confirma a execução visual no editor, não substitui o teste do executável standalone.

## Repetir a validação

Feche o projeto no editor e execute `Tools/Validate-Project.ps1`. Não é necessário pedir elevação ao Windows. O script recusa iniciar se o projeto já estiver aberto e só pode encerrar processos de teste que ele mesmo criou, em caso de timeout.

- `-Migrate`: reaplica a migração explícita e audita as cenas; faça backup antes, pois salva materiais, TerrainData, NavMesh e cenas.
- `-Build`: acrescenta a compilação Windows em `Builds/Windows/EraImperial.exe`.
- Sem opções: executa os três testes unitários e os três cenários.

Os testes de partida guardam seus saves em uma pasta isolada dentro de `TestResults`; não substituem os saves pessoais.

## Pendências e limites

- O Windows foi encontrado com `EnableLUA=0`, e a janela real do Unity foi observada com o prefixo Administrator. O UAC está desativado; isso é separado dos erros do projeto. Nenhuma configuração de segurança nem reinicialização do computador foi feita. A correção do ambiente exige decisão do usuário. Referência: [configuração de UAC — Microsoft](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/settings-and-configuration).
- O projeto ainda usa BinaryFormatter para compatibilidade com os saves antigos, e há avisos de APIs legadas. A migração de persistência precisa preservar/converter os saves; não é correto declarar o projeto livre de toda dívida técnica.
- O prefab substituto de árvores gera aviso sobre shader de billboard/iluminação. A renderização dos cenários passou, mas o acabamento das árvores e dos modelos ainda precisa de trabalho.
- Os testes não cobrem todos os combates, tecnologias, construções, combinações de configurações nem outros sistemas operacionais. O projeto continua sendo uma base de RTS, não um jogo completo acabado.

## Preservação de dados

Backup de Assets, Packages e ProjectSettings: `.review-backups/20260927-review` (feito antes desta revisão).

Terreno original recuperado: `.review-backups/original-map1`. A cópia temporária importada em Assets/Editor/MigrationSource foi removida após a recuperação; o backup e o terreno em uso foram preservados.

Alterações preexistentes, arquivos de tentativas anteriores e histórico Git foram mantidos. Não foi feito commit, push nem publicação.
