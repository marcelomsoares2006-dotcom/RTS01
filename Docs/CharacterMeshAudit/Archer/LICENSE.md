# Licenças do Archer (Era Imperial)

Verificado em 2026-10-05. Este arquivo registra a base de licenciamento de cada componente. Não é aconselhamento jurídico.

| Componente | Origem | Situação |
|---|---|---|
| Malha, textura PBR e rig do corpo | Meshy (texto→imagem, imagem→3D, auto-rig) gerados com a **chave de API do usuário** | A API da Meshy exige plano pago (Pro ou superior). Nos planos pagos o usuário é dono dos ativos gerados; no plano gratuito seria CC BY 4.0 (uso comercial com atribuição). Como a chave de API funcionou, a geração ocorreu sob plano pago. **Limite da verificação:** o plano da conta não é legível pela API; se quiser prova documental, confira em meshy.ai (conta/assinatura). |
| Clipes `Walk` e `Run` | Animações básicas do auto-rig Meshy (mesmo esqueleto) | Mesmo regime do corpo. |
| Clipes `Idle`, `Aim`, `DrawString`, `Fire` | Animações Mixamo (Adobe) reaplicadas ao esqueleto Meshy por retarget (`BuildArcher.py`) | O Mixamo permite uso gratuito, comercial ou não, em jogos. **Restrição:** não é permitido redistribuir os arquivos de animação como pacote/asset de loja ou template. Distribua o jogo, não os clipes soltos. |
| Arco, aljava e flecha | Modelados proceduralmente neste projeto (`Tools/CharacterRigging/BuildArcherAccessories.py`) | Sem terceiros; pertencem ao projeto. |
| Código (Unity/Blender/PowerShell) | Este projeto | Mesma licença do repositório. |

## Fontes consultadas
- Termos e planos da Meshy: https://docs.meshy.ai/faq e https://meshy.ai/pricing
- Mixamo, licenciamento: https://helpx.adobe.com/en/creative-cloud/faq/mixamo-faq.html

## Rastreabilidade
- Tarefas Meshy (sem URLs assinadas nem chaves): `Tools/CharacterRigging/Inputs/Archer/img1_task.json`, `i3d_task.json`, `rig_task.json`.
- Imagem de referência gerada pela própria Meshy: `Tools/CharacterRigging/Inputs/Archer/ref1.png`.
- A chave de API **não** foi gravada em nenhum arquivo do projeto.
