# RTS01 — Meta de conclusão e protocolo de execução

## Meta

Concluir o Era Imperial como uma experiência de estratégia em tempo real inspirada no ciclo de exploração, economia, construção, produção, combate e progressão de eras de Age of Empires IV, com identidade própria, maior profundidade de sistemas, feedback audiovisual consistente e desempenho verificável.

## Regra de avanço

Cada marco deve entregar uma unidade funcional pequena e verificável:

1. um agente implementa apenas um setor delimitado;
2. outro agente ou a fiscalização revisa contratos, riscos e compatibilidade;
3. testes determinísticos e `git diff --check` são executados;
4. o marco recebe um commit próprio e é enviado para `origin/master`;
5. o resultado, limitações e próximo passo são registrados no Cérebro;
6. a integração do marco seguinte começa somente depois desse registro.

## Frentes paralelas atuais

- **Construções:** catálogo, requisitos e ciclo de vida até integração com colocação.
- **Economia:** Food, coleta, população e ligação gradual aos trabalhadores existentes.
- **Combate:** defesa de torres, perfis de dano/armadura e projéteis compatíveis.
- **IA estratégica:** snapshots, gateway de intenções e conectores de execução.
- **Release/fiscalização:** preflight editor-free e validação licenciada Unity 6000.6.3f1.

## Sequência macro

1. Construções essenciais e ciclo completo de obra.
2. Economia e população integradas ao jogo.
3. Prédios econômicos e militares, produção e filas.
4. Combate completo, defesa territorial, muros e portões.
5. Eras, tecnologias, marcos, maravilha e objetivos.
6. IA estratégica integrada, mapas, balanceamento, modos e campanha.
7. UI, VFX, áudio, acessibilidade, desempenho e empacotamento.

O Editor Unity licenciado ainda precisa executar os testes reais e a build; até lá, os commits editor-free são aceitos como marcos parciais, nunca como conclusão do projeto.
