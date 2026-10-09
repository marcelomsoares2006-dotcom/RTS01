# Licenças do CombatMage (Mago de combate, Era Imperial)

Verificado em 2026-10-05. Este arquivo registra a base de licenciamento de cada componente. Não é aconselhamento jurídico.

| Componente | Origem | Situação |
|---|---|---|
| Malha, textura PBR e rig do corpo | Meshy (texto→imagem, imagem→3D, auto-rig) gerados com a **chave de API do usuário** | A API da Meshy exige plano pago (Pro ou superior). Nos planos pagos o usuário é dono dos ativos gerados; no plano gratuito seria CC BY 4.0 (uso comercial com atribuição). Como a chave de API funcionou, a geração ocorreu sob plano pago. **Limite da verificação:** o plano da conta não é legível pela API; se quiser prova documental, confira em meshy.ai (conta/assinatura). |
| Clipes `Walk` e `Run` | Animações básicas do auto-rig Meshy (mesmo esqueleto) | Mesmo regime do corpo. |
| Clipes `Idle`, `Hit`, `Death` | Animações Mixamo (Adobe: `Idle`, `Standing React Large From Left`, `Dying`) reaplicadas ao esqueleto Meshy por retarget (`BuildCombatMage.py`) | O Mixamo permite uso gratuito, comercial ou não, em jogos. **Restrição:** não é permitido redistribuir os arquivos de animação como pacote/asset de loja ou template. Distribua o jogo, não os clipes soltos. |
| Clipe `Cast` | Base Mixamo `Idle` (quadros 1–54) com os braços reescritos **proceduralmente** neste projeto (`BuildCombatMage.py`, `CAST_KEYS`) | Obra derivada de clipe Mixamo: mesma restrição de não redistribuição dos clipes soltos. |
| Cajado (haste, couro, bronze) e orbe | Modelados proceduralmente neste projeto (`Tools/CharacterRigging/BuildCombatMageAccessories.py`) | Sem terceiros; pertencem ao projeto. |
| Efeitos mágicos (projétil, impacto, conjuração) e texturas suaves | Gerados por script de editor (`CombatMageEffectsBuilder.cs`), texturas calculadas matematicamente | Sem terceiros; pertencem ao projeto. Sombreadores do Unity (Built-in RP). |
| Código (Unity/Blender/PowerShell) | Este projeto | Mesma licença do repositório. |

## Fontes consultadas
- Termos e planos da Meshy: https://docs.meshy.ai/faq e https://meshy.ai/pricing
- Mixamo, licenciamento: https://helpx.adobe.com/en/creative-cloud/faq/mixamo-faq.html

## Rastreabilidade
- Tarefas Meshy (sem chaves): `Tools/CharacterRigging/Inputs/CombatMage/img1_task.json` (texto→imagem), `i3d_task.json` (imagem→3D), `rig_task.json` (auto-rig).
  Esses JSONs contêm URLs de saída da Meshy (nenhuma credencial).
- Imagem de referência gerada pela própria Meshy: `Tools/CharacterRigging/Inputs/CombatMage/ref1.png`.
- Clipes Mixamo usados (cópias em `Tools/CharacterRigging/Inputs/CombatMage/Mixamo/`; originais não movidos de `Downloads`): `Idle.fbx`, `Standing-React-Large-From-Left.fbx`, `Dying.fbx`.
  `Goalie-Throw.fbx` e `Standing-Aim-Idle-01.fbx` foram copiados e avaliados, mas **não** entram no jogo.
- A chave de API **não** foi gravada em nenhum arquivo do projeto.
