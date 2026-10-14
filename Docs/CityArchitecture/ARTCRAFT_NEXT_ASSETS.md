# Próximos assets visuais — ArtCraft + Blender

Plano de produção para substituir os blocos provisórios da cidade por construções modulares, legíveis em câmera RTS e prontas para integração no Unity 6.

## Regras comuns

- **CADCraft:** definir implantação, medidas, vãos, espessuras, módulos e pontos de encaixe; exportar DXF/PNG de referência.
- **Blender:** modelar o asset final, criar UVs, materiais PBR, variações por era, LODs, colisões simples e exportar FBX.
- **Unity:** importar com escala 1 unidade = 1 metro, eixo Y para cima, origem no centro da base e pivôs alinhados aos módulos.
- **Escala:** portas com 2,2–2,6 m de altura; personagem humano de referência com 1,8 m; módulo de parede padrão de 4 m; ruas principais com 8 m de largura.
- **Materiais:** albedo/base color, normal, roughness/smoothness, metallic quando necessário e ambient occlusion. Texturas em potência de dois, preferencialmente 2048 para LOD0 hero e 1024 para peças repetidas.
- **AAA:** silhueta clara à distância, microdetalhes controlados, bordas com bevel, variação de pedra/madeira, desgaste coerente, ausência de superfícies perfeitamente lisas e atlas para reduzir draw calls.
- **LOD:** LOD0 detalhado, LOD1 aproximadamente 50–60% dos triângulos, LOD2 aproximadamente 15–25%; testar transições na câmera ortográfica RTS entre 8 m e 80 m. Usar impostor apenas em estruturas vistas muito longe.
- **Performance:** evitar materiais únicos por peça; usar atlas e instancing. Colisores separados e simples, sem collider em detalhes decorativos. Não substituir prefabs existentes até a auditoria visual e de gameplay.
- **Saídas esperadas por asset:** `.blend`, `.fbx`, texturas PBR, prefab Unity, relatório curto de escala/LOD e captura de inspeção em quatro ângulos.

## Ordem de produção

### 1. Casa modular

**Objetivo:** criar uma família residencial que forme quarteirões sem repetição evidente.

- CADCraft: planta de casa pequena, média e grande; módulos de parede, telhado, porta, janela e varanda.
- Blender: kit modular com paredes, esquinas, telhados, chaminés, vigas, calhas, portas e janelas intercambiáveis.
- Variações: camponesa, urbana e nobre; materiais de pedra, madeira, reboco e telha.
- LOD0: 25–40 mil triângulos por casa hero; LOD1: 12–20 mil; LOD2: 3–6 mil.
- Critérios AAA: telhado com espessura real, beirais legíveis, interiores falsos apenas quando visíveis, atlas compartilhado e três combinações sem alterar o footprint.

### 2. Quartel

**Objetivo:** edifício militar reconhecível e adequado para treinar infantaria, arqueiros e tropas avançadas.

- CADCraft: nave central, pátio de treino, entrada larga e área de armazenamento.
- Blender: estrutura robusta, estandartes removíveis, armas decorativas, sacos, madeira estrutural e plataforma de treino.
- Variações por era: paliçada e madeira; pedra clássica; quartel medieval; fortificação imperial com metal e estandartes.
- LOD0: 45–70 mil triângulos; LOD1: 22–35 mil; LOD2: 6–10 mil.
- Critérios AAA: leitura imediata da função militar, pontos de spawn sinalizados, sem ornamentos que bloqueiem unidades e collider externo simples.

### 3. Templo

**Objetivo:** criar um marco visual para pesquisa, cura, fé ou tecnologias da civilização.

- CADCraft: eixo de entrada, nave, degraus, pátio e posição do altar.
- Blender: colunas, frontão, estátuas, vitrais ou painéis, altar e iluminação emissiva discreta.
- Variações por era: santuário simples, templo colunado, basílica medieval e complexo imperial.
- LOD0: 60–90 mil triângulos; LOD1: 30–45 mil; LOD2: 8–14 mil.
- Critérios AAA: composição simétrica, silhueta forte, materiais diferenciados entre pedra estrutural e decoração, interior falso opcional e sem transparência excessiva.

### 4. Mercado

**Objetivo:** representar comércio e servir como peça de preenchimento urbano com alta variedade visual.

- CADCraft: praça, bancas, galpão, depósitos e corredores de circulação.
- Blender: bancas modulares, toldos, caixas, barris, carroças, balanças e sinalização de mercadores.
- Variações: mercado aberto, mercado coberto e mercado nobre; itens decorativos com pontos de troca de material.
- LOD0: 35–55 mil triângulos; LOD1: 16–28 mil; LOD2: 4–8 mil.
- Critérios AAA: modularidade sem sobreposição de colisores, props instanciáveis, cores de leitura por função e atlas de madeira/tecido/cerâmica.

### 5. Torre defensiva

**Objetivo:** produzir uma torre de vigilância e combate que conecte muralhas e funcione como marco vertical.

- CADCraft: diâmetro, altura, plataforma, escada e encaixes para parede.
- Blender: base, corpo, plataforma, ameias, seteiras, telhado opcional e sala de combate.
- Variações por era: madeira, pedra baixa, torre medieval e torre imperial reforçada.
- LOD0: 40–65 mil triângulos; LOD1: 18–32 mil; LOD2: 5–9 mil.
- Critérios AAA: espessura de ameias, seteiras funcionais visualmente, pivô na base, encaixe exato com parede de 4 m e collider cilíndrico/caixa simplificado.

### 6. Muralha modular

**Objetivo:** substituir segmentos provisórios por um kit repetível para perímetros, divisões internas e cenários de cerco.

- CADCraft: segmento reto, esquina, junção de torre, variação de altura e base.
- Blender: pedras com variação controlada, fundação, parapeito, ameias, seteiras e módulos de reparo/dano visual.
- Peças obrigatórias: reta, meia-reta, esquina interna, esquina externa, transição de altura e conector de torre.
- LOD0: 8–15 mil triângulos por módulo de 4 m; LOD1: 4–8 mil; LOD2: 1–2 mil.
- Critérios AAA: repetição mascarada por variações de pedra e acabamento, pivôs nos encaixes, atlas compartilhado e nenhuma alteração no NavMesh principal sem teste.

### 7. Portão

**Objetivo:** criar uma entrada legível, atravessável e compatível com o kit de muralhas e torres.

- CADCraft: vão livre mínimo de 5 m, torres laterais, passagem, soleira e área de aproximação.
- Blender: arco, portão de madeira, ferragens, ponte ou soleira opcional, mecanismo visual de abertura e sala superior.
- Variações por era: paliçada, portão de pedra, portão medieval e portão imperial reforçado.
- LOD0: 55–85 mil triângulos; LOD1: 28–45 mil; LOD2: 7–12 mil.
- Critérios AAA: vão realmente livre, threshold separado do portão visual, animação opcional sem bloquear unidades, collider dividido em laterais e lintel, teste de passagem no Unity.

## Portões de qualidade antes da integração

1. Conferir dimensões no Blender e no Unity com um humanoide de 1,8 m.
2. Verificar UV sem sobreposição indevida, texel density coerente e normais corretas.
3. Conferir LODGroup, sombras, light probes, occlusion e materiais sem magenta.
4. Instanciar cada peça em uma cena de teste com luz diurna e noturna.
5. Testar seleção, colocação, colisão, NavMesh e passagem de unidades.
6. Comparar quatro ângulos: próximo, gameplay, longe e vista aérea.
7. Só depois registrar o prefab no catálogo de construções e substituir o bloco provisório correspondente.

## Entregas da próxima rodada

- Casa modular com três fachadas e dois telhados.
- Primeiro segmento de muralha, esquina e conector de torre.
- Torre defensiva compatível com o conector.
- Cena de validação com escala Unity, LOD e teste de passagem.

