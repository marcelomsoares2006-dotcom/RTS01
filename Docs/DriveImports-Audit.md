# Auditoria dos assets próprios do Google Drive

Data da triagem: 2026-10-07.

Os arquivos foram copiados para `Tools/CharacterRigging/DriveImports/` sem substituir assets existentes. A validação foi executada no Blender 5.2 com `Tools/CharacterRigging/ValidateGameAsset.py`.

| Asset | Resultado | Uso recomendado |
|---|---|---|
| `ArcherElite.glb` | **Reprovado para importação direta**: corpo sem UV; objeto auxiliar sem material; rig presente | Corrigir/remover objeto auxiliar, gerar UV e só então criar variante elite |
| `MagicShield.zip` | **Aprovado**: 9.998 triângulos, UV e material válidos | Acessório do paladino ou herói mágico |
| `OrcAxe.zip` | **Aprovado**: 10.000 triângulos, UV e material válidos | Acessório de guerreiro/orc ou variante pesada |
| `WoodSword.zip` | **Origem reprovada; cópia reparada aprovada**: `WoodSword_Repaired.fbx` com 9.936 triângulos, UV/material válidos e 0 arestas não-manifold | Acessório estático em teste; não integrado à produção |
| `Silver Sentinel` | **Pendente**: pacote de 151 MB; requer extração, escala, cor e teste de rig | Unidade de infantaria pesada após triagem própria |

## Arquivos de origem

- [Pasta Era Imperial - Personagens](https://drive.google.com/drive/folders/1g5T-WphW57BpPEQcSUI7MWcIfFom1eJK)
- [Arqueiro elite](https://drive.google.com/drive/folders/1JXktGJCQNI-dXnAxKVHvcu-LgeLLKS2K)
- [Sentinela Silver](https://drive.google.com/file/d/1cLu4ZJI_w2EdhLv7N2t1yIj95SRCga7L/view)
- [Escudo mágico](https://drive.google.com/file/d/1paCDN_BEK7LsYt0hTDOZJxBv6DBEvzup/view)
- [Machado de orc](https://drive.google.com/file/d/1kDlTx8asBXROOk-y9hTkjvCYnfuYGFNw/view)
- [Espada de madeira](https://drive.google.com/file/d/1kcSNNQBMbzQ245lphHD0N7TwE-fXKNTS/view)

## Próximas etapas

1. Integrar escudo e machado como acessórios em prefabs de teste, sem alterar os prefabs de produção.
2. Reparar a espada e retestar com limite de triângulos compatível com RTS.
3. Corrigir UV/objeto auxiliar do arqueiro elite e comparar com o arqueiro já validado.
4. Extrair e auditar a sentinela antes de criar uma nova unidade no catálogo.
