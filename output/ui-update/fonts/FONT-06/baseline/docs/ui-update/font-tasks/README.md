# Substituição da tipografia para aproximar os concepts

Data: 17/10/2026. Status: **FONT-01 a FONT-05 concluídos; FONT-06 a FONT-10 pendentes**.

Este lote substitui a Inter Tight usada na interface por Roboto e Roboto Condensed, distribuídas por função visual. Inclui localizar ou baixar as fontes, criar os assets TextMeshPro necessários e aplicar a mudança nas interfaces reais. Os registros de conclusão de UI-01 a UI-20 continuam sendo evidências da entrega anterior; não comprovam esta substituição.

## Referência e decisão visual

Os cinco concepts estão em [art-source/references/concepts](../../../art-source/references/concepts). A [análise comparativa](../../../output/ui-update/font-identification/index.html) encontrou semelhança com Roboto e Roboto Condensed. Os concepts são imagens geradas, sem uma família tipográfica identificada no prompt. O objetivo é reproduzir sua aparência com fontes reais, sem afirmar uma identificação exata.

A matriz abaixo é a decisão inicial deste lote. Aferir altura aparente, peso, largura e espaçamento nas telas, em vez de considerar concluída uma simples troca de arquivo. Barlow e Source Sans 3 são alternativas da pesquisa, não famílias obrigatórias a importar.

| Função | Família e peso inicial | Tamanho inicial no quadro 1920×1080 |
| --- | --- | --- |
| Marca “Jogos de Empresa” | Roboto Bold 700 | 36 |
| Título principal de tela | Roboto Bold 700 | 44 |
| Seções e títulos compactos de cards | Roboto Condensed Bold 700 | 32 |
| Corpo e campos editáveis | Roboto Regular 400 | 24 |
| Tabelas | Roboto Regular 400; totais em Bold 700 | 22 |
| KPIs e valores de destaque | Roboto Bold 700 | 42 |
| Botões principais e secundários | Roboto Condensed SemiBold 600 | 24 |
| Navegação, filtros e rótulos compactos | Roboto Condensed Medium 500 | 22–24 |
| Legendas e informação secundária curta | Roboto Condensed Regular 400 | 20 |
| Marcadores de mapa | Roboto Condensed SemiBold 600 | Partir do tamanho vigente por marcador |

Os tamanhos são os tokens atuais como ponto de partida, não novos valores já homologados. Nomes de funções não devem virar heurísticas baseadas apenas no nome do GameObject. Registrar o papel de cada texto e as exceções justificadas por comparação visual.

## Estado inventariado na FONT-01

- [GameTypography.cs](../../../Assets/Scripts/Game/Adapter/In/UI/Theme/GameTypography.cs) aponta para `InterTight-Variable.asset` e recebe um único font asset com um booleano de negrito.
- Há Inter Tight, Inter e Liberation Sans em `Assets/TextMesh Pro/Fonts`. `Roboto-Bold.ttf` existe em `Examples & Extras`; sua existência não prova integração no tema.
- Há downloads de pesquisa em `output/ui-update/font-identification`. Validar origem, versão e licença antes de reaproveitar. O build não pode depender de `output` nem de arquivos instalados no computador.
- Há referências diretas à Inter Tight nos builders de identificação/localização, no `BackButtonStyler`, em skins e em testes. Textos também são criados ou reconfigurados em runtime.
- A cena de produção habilitada é `Assets/Scenes/GameScene.unity`, no Unity 6000.3.20f1. Conferir novamente o estado antes de implementar.

## Tarefas e ordem de execução

O inventário de FONT-01 está concluído; a substituição das fontes ainda está planejada. Cada arquivo contém escopo, dependências, implementação e critérios de aceite.

| Ordem | Tarefa | Depende de | Status |
| --- | --- | --- | --- |
| 1 | [FONT-01 — Inventário e matriz por texto](FONT-01-inventario-e-matriz.md) | — | Concluído — [QA](../qa/FONT-01.json) |
| 2 | [FONT-02 — Download, fontes de produção e licenças](FONT-02-download-e-licencas.md) | FONT-01 | Concluído — [QA](../qa/FONT-02.json) |
| 3 | [FONT-03 — Assets TextMeshPro e glifos](FONT-03-assets-tmpro.md) | FONT-02 | Concluído — [QA](../qa/FONT-03.json) |
| 4 | [FONT-04 — Tema, builders e geração em runtime](FONT-04-tema-e-geracao-runtime.md) | FONT-03 | Concluído — [QA](../qa/FONT-04.json) |
| 5 | [FONT-05 — Componentes, HUD, hub e entrada](FONT-05-componentes-hud-e-entrada.md) | FONT-04 | Concluído — [QA](../qa/FONT-05.json) |
| 6 | [FONT-06 — Restaurante, cardápio e revisão](FONT-06-restaurante-cardapio-e-revisao.md) | FONT-05 | Pendente |
| 7 | [FONT-07 — Banco, equipamentos e equipe](FONT-07-banco-equipamentos-e-equipe.md) | FONT-06 | Pendente |
| 8 | [FONT-08 — DRE, relatórios e falência](FONT-08-dre-relatorios-e-falencia.md) | FONT-07 | Pendente |
| 9 | [FONT-09 — Eventos, diálogos, feedback e ajuda](FONT-09-eventos-dialogos-e-ajuda.md) | FONT-08 | Pendente |
| 10 | [FONT-10 — Validação final e atualização do contrato](FONT-10-validacao-final.md) | FONT-09 | Pendente |

Executar nessa ordem para manter uma referência coerente durante as alterações da mesma cena. Um pedido de execução do lote autoriza percorrer a sequência; os arquivos permitem também solicitar uma tarefa individual.

## Regras comuns de implementação

1. Ler este índice, a tarefa e o [contrato vigente](../visual-contract.json). Antes de modificar, conferir a hierarquia, os prefabs e as referências atuais. Trabalhar com as skills Unity UI/uGUI, TextMeshPro e Unity CLI quando aplicáveis.
2. Usar arquivos reais dos pesos necessários. Se a distribuição oficial só fornecer uma fonte variável, gerar instâncias estáticas reproduzíveis antes de criar os assets TMP. Não presumir que o importador aplicou os eixos desejados.
3. Criar assets TMP, atlas e materiais pelo Editor/API compatível com a versão instalada. Não fabricar YAML de atlas nem abrir importadores modais de TMP Essentials para este lote.
4. Reutilizar fontes e assets adequados que já existam, conferindo família, peso, licença, cobertura e qualidade. Download e criação de TMP são obrigatórios quando faltar um desses itens; copiar apenas o TTF não conclui a integração.
5. Preservar o quadro de referência, a paleta, as artes, os callbacks e os dados do jogo. Ajustar margens, alturas e quebras quando a nova métrica exigir. Não comprimir RectTransforms nem aplicar escala de fonte diferente de 1 para imitar condensação.
6. Resolver família, peso e material por papel tipográfico. Não manter negrito sintético cumulativo, `Face Dilate` ou overrides que engrossem novamente uma face já Bold. Verificar também `<b>`, `<i>` e pesos definidos por código quando existirem.
7. Cobrir textos serializados e criados em runtime, incluindo objetos inativos, templates, estados de erro e reentrada. Corrigir as fábricas e migrations para não restaurarem a Inter Tight depois.
8. Manter a família nova como primária em toda UI de produção. Preservar arquivos antigos que tenham consumidores legítimos; registrar exceções e fallbacks. Não substituir ícones ou palavras pintadas dentro das ilustrações por esta migração.
9. Usar os tamanhos vigentes inicialmente, revisar visualmente e registrar os finais. Não usar redução global, autosize de contadores ou truncamento para ocultar problemas de layout.
10. Guardar QA por tarefa em `docs/ui-update/qa/FONT-XX.json` e evidências em `output/ui-update/fonts/FONT-XX/`. Arquivos apenas planejados não são evidências. Atualizar o status do arquivo e deste índice somente após o aceite.

## Cobertura das tarefas anteriores

| Escopo anterior | Tarefa deste lote |
| --- | --- |
| UI-01 identificação; UI-04 hub/HUD; UI-05 localização | FONT-05 |
| UI-02 tema e UI-03 componentes | FONT-01 a FONT-05 |
| UI-06 restaurante; UI-07 cardápio; UI-08 estabelecimento/revisão | FONT-06 |
| UI-09 banco; UI-10 equipamentos; UI-11 equipe | FONT-07 |
| UI-13 DRE mensal; UI-14 trimestral; UI-15 final; UI-16 falência | FONT-08 |
| UI-12 eventos; UI-17 ajuda/feedback; UI-18 diálogos | FONT-09 |
| UI-19 navegação e reentrada | FONT-04, FONT-05 e FONT-10 |
| UI-20 QA e build | FONT-10 |

## Fontes de consulta

- [Roboto no Google Fonts](https://fonts.google.com/specimen/Roboto) e [arquivos oficiais](https://github.com/google/fonts/tree/main/ofl/roboto).
- [Roboto Condensed no Google Fonts](https://fonts.google.com/specimen/Roboto+Condensed) e [arquivos oficiais](https://github.com/google/fonts/tree/main/ofl/robotocondensed).
- Código TMP da versão instalada em `Library/PackageCache/com.unity.ugui@*/Runtime/TMP/` e ferramentas de criação em `Editor/TMP/`.
- [Revisão de composição atual](../concept-layout.json) e [QA anterior](../qa/CONCEPT-REVISION.json), para comparar sem atribuir os resultados antigos a esta migração.

## Como solicitar a execução

> Execute FONT-01 conforme docs/ui-update/font-tasks/FONT-01-inventario-e-matriz.md, seguindo o índice do lote e registrando os resultados reais.

> Execute o lote FONT-01 a FONT-10 de docs/ui-update/font-tasks/README.md, incluindo downloads necessários, geração dos assets TMP, integração nas interfaces e validação final.
