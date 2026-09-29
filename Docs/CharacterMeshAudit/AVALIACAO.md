# Avaliação das malhas para o Era Imperial

Inspeção de 28/09/2026, realizada no Blender 5.2.1. Os originais em
`Tools/CharacterRigging/Inputs` foram somente lidos, sem salvar alterações.

## Resultado e seleção inicial

Há material aproveitável para soldados, trabalhadores, cavalaria e cervos.
O rig pronto não será suficiente para a maioria dos arquivos: primeiro será
necessário preparar versões mais leves e ajustar a malha nas articulações.
Todos os 30 arquivos BLEND estão sem armature e sem pesos de skinning.
Os dois ZIPs são uma exceção importante: já contêm personagens com rig e animações.

| Prioridade | Arquivo/modelo | Uso proposto | Triângulos da malha | Preparação necessária |
|---|---|---|---:|---|
| 1 | Heraldic_Knight_biped.zip | Primeiro soldado animado e referência de integração | 19.609 | Aproveitar rig existente; revisar capa/escudo em movimento, exportar e validar Avatar/Animator no Unity. |
| 2 | Straw_Hat_Farmhand | Camponês/trabalhador | 185.021 | Reduzir/reconstruir topologia, preservar texturas e adaptar ao rig humanoide; revisar saia/avental. |
| 3 | Spearman | Lanceiro | 467.594 | Produzir malha leve; lança e escudo estão visualmente separados e possuem componentes desconectados, mas ainda no mesmo objeto. Separar acessórios e fixá-los às mãos. |
| 4 | knight_horse | Cavalo de guerra | 1.001.521 | É um cavalo separado. Preparar versão leve, ajustar pose inicial das pernas e conferir rédeas, crina e arreios antes do rig equino. |
| 5 | Red_Deer_Stag | Cervo/fauna | 190.339 | Melhor ponto de partida que o outro cervo pelo menor peso. Adaptar proporções, pescoço e cascos; manter galhadas rígidas ligadas à cabeça. |
| 6 | Armored_Knight / _villain_Dark_Knigh | Soldado base/variante de armadura | 767.296 / 631.114 | Poses abertas e silhuetas legíveis facilitam o preparo. Exigem otimização e revisão de ombros, quadris e joelhos. |
| 7 | medieval_knight_biped.zip | Fonte adicional de animações e soldado pesado | 115.527 | Rig existente aproveitável, mas a malha é bem mais pesada que Heraldic; criar versão otimizada e testar transferência das animações. |

Nomes abreviados nesta tabela correspondem aos nomes completos em `inventory.json`.
As prioridades são uma avaliação dos arquivos locais; não representam uma aprovação
de desempenho ou importação no Unity.

## Modelos que exigem mais trabalho

- **Azure_Knight_on_a_Ste e Crimson_Knight_on_Hor:** cavalo e cavaleiro estão
  no mesmo objeto, e praticamente todos os vértices pertencem a um único componente
  conectado: 290.525 de 290.550 e 377.813 de 377.921, respectivamente. Não basta
  usar “separar partes soltas”. Será necessário cortar/reconstruir as regiões de
  contato para cavalos e cavaleiros independentes. São boas referências de visual,
  ou candidatos a um personagem montado permanente com um rig composto.
- **Azure_Knight, Frontlines_Knight e The_Silver_Knight:** as poses de combate e
  os acessórios junto ao corpo aumentam o trabalho de preparação para um rig comum.
- **Azure_Sentinel, Templar_Knight_Armor, Warrior_in_Armor e demais capas/tabardos:**
  prever pesos ou ossos auxiliares para tecidos; ligar todo o tecido às pernas
  automaticamente pode causar interseções e deformação ruim.
- **Dark_Knight, Medieval_Warrior, Iron_Sentinel e Warrior_in_Armor:** próximos
  ou acima de um milhão de triângulos. Servem como fontes detalhadas para uma nova
  malha mais leve e transferência das texturas/detalhes.

Uma malha triangulada não é automaticamente inutilizável. O problema é a densidade
e a distribuição dos polígonos nas articulações. Redução automática pode ajudar em
uma primeira versão, mas ombros, cotovelos, quadris e joelhos exigem teste de dobra.

## O que já foi validado nos ZIPs

Os quatro GLBs do Heraldic Knight e os nove do medieval knight foram importados
individualmente. Todos apresentam skinning com esqueleto de 23 ossos, com nomes
`mixamorig:*` e `headfront`, sem ossos articulados de dedos.

- **13/13 clipes passaram** na verificação estrutural e de deformação amostrada.
- Nenhum vértice da malha de personagem ficou sem peso; até quatro influências por vértice.
- Somas dos pesos normalizadas dentro da tolerância de 1%.
- Posições avaliadas finitas e mudança de posição entre quatro amostras de cada clipe.
- Heraldic inclui ataque, corrida, ataque triplo e caminhada.
- Medieval inclui nove clipes: os quatro tipos de locomoção/combate do pacote,
  além de posturas e ataques específicos; nomes exatos estão em `animation-checks.json`.

Esses testes demonstram que as animações deformam a malha no Blender. Não comprovam
ausência de todas as interseções, qualidade artística de cada movimento, compatibilidade
automática com Rigify/Kimodo, nem validação Humanoid no Unity. Para reaproveitar os
clipes em outro esqueleto será necessário mapear ossos e conferir pose de referência,
escala, apoio dos pés e movimento da raiz.

## Texturas, escala e cenário

Os 30 BLENDs abriram, possuem UV e imagens incorporadas; não houve imagem externa
ausente entre as referências inspecionadas. Foram encontrados mapas de 2K, e alguns
modelos também têm mapas de 4K. Preservar os mapas ao criar a versão leve e revisar
materiais na exportação; os previews são do Blender, não uma prova de shader no Unity.

As dimensões parecem normalizadas por arquivo: os dois conjuntos montados têm cerca
de 1,9 unidade de altura, semelhante aos humanos isolados, enquanto o cavalo tem
cerca de 1,27. Precisam de escala comum antes de montar a cavalaria.

As seis árvores e três formações rochosas são úteis para cenário, não para o rig
humanoide. Rock_Formation (5.512 triângulos), Rock_Pile (5.220) e a árvore
`0928223909` (6.307) são os mais leves. As árvores maiores chegam a 3.283.835
triângulos e precisam de versões simplificadas e LODs antes de povoar o terreno.
A árvore de 6.307 triângulos tem visual mais estilizado que as outras: avaliar a
coerência visual antes de misturá-las.

Os três `Human/Horse/Wolf_SkinningFixture.glb` já existentes são protótipos técnicos
do pipeline. Não foram contados como arte final nova nesta análise. A pasta contém
dois cervos novos, mas não contém um lobo final equivalente às novas malhas.

## Catálogo completo

| ID | Modelo | Triângulos | Uso/observação |
|---:|---|---:|---|
| 01 | _villain_Dark_Knigh | 631.114 | Humanoide em pose aberta; boa base após otimização. |
| 02 | a_tree | 3.283.835 | Árvore detalhada; otimização pesada. |
| 03 | Armored_Knight | 767.296 | Humanoide em pose aberta. |
| 04 | Azure_Crest_Knight | 516.007 | Soldado com espada; preparar pose e acessórios. |
| 05 | Azure_Knight | 687.107 | Espada/escudo e pose de combate. |
| 06 | Azure_Knight_on_a_Ste | 581.122 | Montado, geometria predominantemente fundida. |
| 07 | Azure_Sentinel | 631.293 | Soldado com capa e tecido. |
| 08 | Crimson_Knight_on_Hor | 755.873 | Montado, geometria predominantemente fundida. |
| 09 | Dark_Knight | 1.297.067 | Humanoide em pose aberta; muito pesado. |
| 10 | Frontlines_Knight | 780.826 | Espada/escudo e capa. |
| 11 | Iron_Sentinel | 1.051.041 | Armadura; muito pesado. |
| 12 | knight_horse | 1.001.521 | Cavalo independente; preparar pose e rig. |
| 13 | Medieval_Standby | 757.493 | Civil em pose aberta; alternativa ao trabalhador. |
| 14 | Medieval_Warrior | 1.363.156 | Soldado em pose aberta; muito pesado. |
| 15 | Midnight_Knight | 873.317 | Variante de armadura em pose aberta. |
| 16 | Monolithic_Fragment | 519.898 | Rocha/cenário; reduzir bastante. |
| 17 | Oak_Tree | 1.797.450 | Árvore; precisa de LODs e redução. |
| 18 | Red_Deer_Stag | 190.339 | Cervo prioritário. |
| 19 | Rock_Formation | 5.512 | Cenário; candidato mais leve. |
| 20 | Rock_Pile | 5.220 | Cenário; candidato mais leve. |
| 21 | Silver_Birch_Majesty | 2.331.631 | Árvore; precisa de LODs e redução. |
| 22 | Spearman | 467.594 | Lanceiro; boa disposição inicial dos acessórios. |
| 23 | Stag | 315.186 | Segundo cervo; variante visual. |
| 24 | Straw_Hat_Farmhand | 185.021 | Trabalhador prioritário. |
| 25 | Templar_Knight_Armor | 950.753 | Variante de tropa; tabardo e articulações. |
| 26 | The_Silver_Knight | 597.086 | Espada/escudo em pose de combate. |
| 27 | tree_0928223909 | 6.307 | Árvore leve e estilizada. |
| 28 | tree_0928223914 | 1.001.201 | Árvore detalhada. |
| 29 | tree_0928223917 | 273.866 | Árvore; reduzir para uso repetido. |
| 30 | Warrior_in_Armor | 1.020.143 | Soldado de capa; muito pesado. |
| 31 | Heraldic_Knight_biped | 19.609 | Rig existente; quatro animações. |
| 32 | medieval_knight_biped | 115.527 | Rig existente; nove animações. |

Contagens são de triângulos da malha-base, sem LODs; objetos auxiliares do importador
de rig foram excluídos dos dois GLBs de amostra. Não foi feito benchmark de FPS.

Previews: [catálogo 1](catalogo-1.jpg), [catálogo 2](catalogo-2.jpg).
Dados: [inventário](inventory.json), [testes dos clipes](animation-checks.json).

## Ordem de preparação sugerida

1. Integrar uma cópia do Heraldic Knight para validar a rota FBX → Unity e animação.
2. Preparar uma malha leve do trabalhador e do lanceiro, preservando os originais.
3. Ajustar o rig equino ao cavalo separado e um rig quadrúpede às proporções do cervo.
4. Conferir deformação, acessórios, materiais e escala; produzir LODs e validar em cena.
5. Reaproveitar o processo nas variantes de armadura e, depois, nos conjuntos montados.
