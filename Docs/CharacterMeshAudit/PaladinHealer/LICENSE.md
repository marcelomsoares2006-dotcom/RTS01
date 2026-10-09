# Licenças do PaladinHealer (Era Imperial)

Verificado em 2026-10-06. Este arquivo registra a origem e a base de licenciamento de cada componente. Não é aconselhamento jurídico.
Mesmo regime do Arqueiro (`Docs/CharacterMeshAudit/Archer/LICENSE.md`).

| Componente | Origem | Situação |
|---|---|---|
| Malha, textura PBR (base, normal, metálico/rugosidade) e rig do corpo | Meshy (texto→imagem `nano-banana`, imagem→3D, auto-rig) gerados com a **chave de API do usuário** | A API da Meshy exige plano pago (Pro ou superior). Nos planos pagos o usuário é dono dos ativos gerados; no plano gratuito seria CC BY 4.0 (uso comercial com atribuição). Como a chave de API funcionou, a geração ocorreu sob plano pago. **Limite da verificação:** o plano da conta não é legível pela API; se quiser prova documental, confira em meshy.ai (conta/assinatura). |
| Mapa metálico/suavidade `PaladinHealer_MetallicSmooth.png` | Derivado do mapa metal/rugosidade do glTF da Meshy (reempacotado e escalado em `BuildPaladinHealer.py`) | Mesmo regime do corpo. |
| Clipes `Walk` e `Run` | Animações básicas do auto-rig Meshy (mesmo esqueleto) | Mesmo regime do corpo. |
| Clipes `Idle`, `Attack`, `Hit`, `Death` | Animações Mixamo (Adobe) — *Sword And Shield Idle*, *Sword And Shield Slash*, *Standing React Large From Left*, *Dying* — reaplicadas ao esqueleto Meshy por retarget (`BuildPaladinHealer.py`) | O Mixamo permite uso gratuito, comercial ou não, em jogos. **Restrição:** não é permitido redistribuir os arquivos de animação como pacote/asset de loja ou template. Distribua o jogo, não os clipes soltos. As cópias de trabalho estão em `Tools/CharacterRigging/Inputs/PaladinHealer/Mixamo/` (os originais continuam em `Downloads`). |
| Clipe `Heal` | Criado neste projeto, em Blender, sobre o esqueleto Meshy (`build_heal` em `BuildPaladinHealer.py`) | Sem terceiros; pertence ao projeto. |
| Espada longa e escudo | Modelados proceduralmente neste projeto (`Tools/CharacterRigging/BuildPaladinHealerAccessories.py`) | Sem terceiros; pertencem ao projeto. |
| Efeito de cura (texturas, malha, partículas) | Texturas geradas proceduralmente (`BuildPaladinHealerEffect.py`); prefab e script do próprio projeto | Sem terceiros; pertence ao projeto. |
| Código (Unity/Blender/PowerShell) | Este projeto | Mesma licença do repositório. |

## Fontes consultadas
- Termos e planos da Meshy: https://docs.meshy.ai/faq e https://meshy.ai/pricing
- Mixamo, licenciamento: https://helpx.adobe.com/en/creative-cloud/faq/mixamo-faq.html

## Rastreabilidade
- Tarefas Meshy (sem URLs assinadas nem chaves): `Tools/CharacterRigging/Inputs/PaladinHealer/img1_task.json`, `i3d_task.json`, `rig_task.json`.
- Imagem de referência gerada pela própria Meshy: `Tools/CharacterRigging/Inputs/PaladinHealer/ref1.png`.
- A chave de API **não** foi gravada em nenhum arquivo do projeto.
