# Frente AAA — Renderização e captura

Data da auditoria: 2026-10-12  
Projeto: Era Imperial / RTS01-Corrigido  
Alvo: Unity 6.0.6.3f1, Built-in Render Pipeline, DirectX 11, RX 580 8 GB

## Resultado executivo

O projeto permanece no Built-in RP e não deve migrar globalmente para URP/HDRP nesta etapa. O pacote `com.unity.postprocessing` 3.5.4 está disponível e pode ser usado de forma local em uma cena ou câmera de apresentação, mas não foi encontrado um volume/perfil AAA centralizado para ativar sem risco às partidas.

O caminho seguro para imagens de alta resolução é usar a ferramenta isolada `Tools/AAA/Capture-AAA.ps1` com `Assets/Editor/AAA/AAARenderPipelineAudit.cs`. Ela configura temporariamente a câmera selecionada em uma cópia de execução, captura 2560x1440 ou 3840x2160 e restaura o estado ao fechar o modo de captura. Não altera o pipeline global.

## Auditoria atual

| Área | Estado | Observação |
|---|---|---|
| Pipeline | Aprovado com ressalva | Built-in RP; `m_CustomRenderPipeline` nulo |
| HDR de câmera | Disponível | Câmeras de `GameScene` têm HDR habilitado |
| MSAA | Disponível | Câmeras permitem MSAA; qualidade Ultra está em 2x |
| Pós-processamento | Disponível, não padronizado | Pacote 3.5.4 presente; perfil AAA dedicado ainda não existe |
| Sombras | Disponível | Ultra: 4 cascatas/150 m; Very High: 2 cascatas/70 m |
| LOD | Disponível | `lodBias` Ultra 2; cada unidade deve ser validada por distância |
| Captura 2K/4K | Preparada | Ferramenta gera PNG sem alterar resolução permanente |
| FPS | Instrumentado | Ferramenta registra média, mínimo e máximo durante a captura |
| Desempenho RX 580 | Pendente de medição real | Meta recomendada: 60 FPS em 2K; 30 FPS sustentados em 4K |

## Preset de captura AAA recomendado

- 2K: 2560x1440, PNG, HDR temporário, MSAA 4x se a cena permanecer acima de 60 FPS.
- 4K: 3840x2160, PNG, HDR temporário, MSAA 2x; preferir supersampling de captura em vez de deixar 4K permanente durante a partida.
- Luz principal: uma Directional Light com sombras em cascata; evitar várias luzes pontuais com sombras em tempo real.
- Sombras: Ultra para stills; reduzir distância/cascatas no jogo quando a contagem de unidades crescer.
- LOD: manter `lodBias` 2 apenas na captura; validar LOD0/1/2 por distância e evitar forçar LOD0 em todas as unidades.
- Pós: Bloom, Color Grading/tonemapping e Ambient Occlusion devem ficar em perfil local de apresentação. Não ativar globalmente sem teste de FPS e sem verificar que nenhum material fica magenta.

## Critérios de aceite

1. Auditoria Unity retorna `AAA_RENDER_AUDIT_PASSED`.
2. Captura 2K e 4K gera PNG com as dimensões solicitadas.
3. Média de FPS da cena alvo fica acima da meta definida no comando.
4. Não há mudança persistente em `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset` ou no pipeline global.
5. Captura é visualmente revisada para HDR estourado, sombras serrilhadas, materiais magenta, LOD pop-in e UI cortada.

## Limitações conhecidas

- Uma imagem 4K não prova desempenho 4K em tempo real; a captura pode usar resolução temporária.
- Built-in RP não oferece o mesmo conjunto de recursos de HDRP. O objetivo aqui é aparência AAA compatível e controlada, não equivalência de renderer.
- A RX 580 deve ser medida com a cena real e o número de unidades da partida; não assumir 60 FPS sem telemetria.
- A auditoria não cria materiais, luzes ou pós-processamento novos automaticamente para não contaminar as cenas de produção.

## Próxima etapa recomendada

Criar uma cena de apresentação dedicada, duplicada da cena de revisão, com perfil Post Processing local, iluminação cinematográfica, câmera ortográfica RTS e alvos de captura. Depois rodar a matriz 2K/4K com 1, 50 e 100 unidades, registrar FPS e revisar as imagens antes de integrar qualquer preset ao jogo.
