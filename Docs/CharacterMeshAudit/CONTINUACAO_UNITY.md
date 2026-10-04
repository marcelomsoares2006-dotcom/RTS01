# Estado atual e próximos passos no Unity

Em 29/09/2026 o usuário autorizou explicitamente a execução elevada. O Unity
6000.6.3f1 importou e validou a etapa 2, preparou a cena das etapas 3–5, os
quatro prefabs LOD e o protótipo montado. `Stage2/unity-playmode.txt` registra
Play Mode aprovado; `Stage3/unity-runtime.txt` registra quatro Avatars válidos,
deformação amostrada, LODGroups mapeados, protótipo montado estruturalmente
válido e zero erros. `Stage3/unity-review.png` é a captura real, sem rosa.
`TestResults/unit-tests.xml` registra 3/3 testes aprovados e
`TestResults/playmode.log`, três cenários de smoke test aprovados.

Os controllers individuais das etapas 3–5 foram confirmados em Play Mode:
os quatro avançam a PoseCheck a 0,333 do clipe e deformam as malhas. O teste
também desativou temporariamente os Animators para amostrar os clipes diretamente
e comparar as poses. O protótipo montado agora usa um rider Generic com
`MountedIdle` ativo, ligado ao Spine do cavalo; a pose foi validada em Play Mode
com dez ângulos e quadris sobre a sela (`Stage5/unity-mounted.txt`). O Animator
do cavalo segue desativado nesse protótipo estático.
Ainda faltam locomoção montada, IK, LOD aceitável do cavalo,
teste de troca dos LODs em distância real e FPS. Os quatro prefabs LOD tiveram
bind poses corrigidos e 24 comparações frontal/lateral renderizadas no Unity
(`Stage4/unity-lod-visual.txt`); o LOD2 do Spearman precisou de redução menos
agressiva. A cena de revisão é
`Assets/CharacterRigging/Stage345Review.unity`.

## Histórico anterior à autorização elevada

Estado em 29/09/2026: Blender/FBX concluídos até o ponto descrito nos relatórios
das etapas 2–5. A execução do Editor dentro do ambiente restrito falhou ao
conectar ao `LicenseClient-User`. A tentativa de executar Unity fora dele foi
rejeitada pela revisão automática por possível acesso de scripts do projeto
a arquivos/configurações do sistema com privilégios administrativos. Não foi
repetida nem contornada.

Em sessão Unity 6000.6.3f1 aberta como usuário padrão, sem outra instância do
projeto, executar nesta ordem e conferir resultado real após cada passo:

1. `Tools > Character Rigging > Stage 2 > Build and Validate`. Exigir Avatar
   Humanoid válido, prefabs/cena, capturas `Stage2/unity-{rest,bend}.png` e
   `STAGE2_PLAYMODE_PASSED`, Console sem erros. Corrigir falhas antes de prosseguir.
2. `Tools > Character Rigging > Stages 3-5 > Build Review Assets`. Conferir
   Avatar Generic válido para cavalo/cervo e Humanoid para as variantes;
   texturas, materiais, Prefabs e `Stage345Review.unity`. Revisar frente/lado/
   traseira e pose dos quatro novos modelos; comparar escala com os anteriores.
3. `Tools > Character Rigging > Stages 3-5 > Build LOD Prefabs` somente após
   prefabs da etapa 2. Verificar níveis 0/1/2, transferência de ossos,
   troca visual e ausência de rosa ou partes deslocadas. O cavalo não tem LOD
   aprovado e é omitido desse setup.
4. `Tools > Character Rigging > Stages 3-5 > Build Mounted Fit Prototype`.
   Conferir posição do cavaleiro e attachment ao osso Spine do cavalo. Este
   prefab é apenas para inspeção: os animators estão desativados e falta
   integração de MountedIdle, movimento, rédeas e ataques.
5. Executar novamente os testes unitários e os smoke tests do jogo, comparar
   Console, capturas e FPS com o cenário base. Registrar evidências novas nos
   relatórios das etapas. Não tratar compilação isolada C# como execução Unity.

O cavalo continua precisando de LODs visualmente aceitáveis. Duas tentativas
de remesh passaram nos testes estruturais, mas foram rejeitadas nas imagens:
`Stage4/Horse-LOD{1,2}.png`. Os FBXs rejeitados estão em
`Tools/CharacterRigging/Working/Stage4/Rejected/`, fora da pasta Assets.
