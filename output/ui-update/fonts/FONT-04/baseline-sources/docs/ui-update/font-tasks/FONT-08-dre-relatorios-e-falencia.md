# FONT-08 — DRE, relatórios, conclusão e falência

Status: **pendente**. Prioridade: P1. Dependência: FONT-07.

## Escopo e referências

UI-13 a UI-16: DRE mensal, trimestral, reconciliação de caixa, detalhes/gráficos, relatório final/BMC/debriefing existente, conclusão e falência. Ler o [índice](README.md), concepts `04-dre-mensal.png` e `05-dre-trimestral.png`, `MonthlyResultView`, `QuarterlyResultView`, `FinalReportView`, `BankruptcyView` e migrations/consumidores encontrados no inventário.

## Implementação

- [ ] Migrar títulos, tabs de período, cabeçalhos de coluna, nomes de linha, subtotais, KPIs, saldos e ações. Usar Roboto para corpo/valores e Condensed nos títulos compactos/rótulos previstos.
- [ ] Aplicar pesos reais aos totais. Preservar alinhamento à direita, sinais e formato monetário; verificar numerais tabulares se suportados e apropriados.
- [ ] Aferir tabela trimestral e os textos menores sem reduzir todo o conjunto para compensar as novas métricas. Ajustar áreas e quebra de descrições mantendo o comparativo legível.
- [ ] Cobrir legendas, eixos, valores de gráficos, detalhes expansíveis e textos de interpretação criados em runtime.
- [ ] Aplicar o mesmo sistema aos relatórios finais e falência. Não inventar modelos/documentos ausentes nem transformar limitações anteriores em novas telas.
- [ ] Revisar números positivos, negativos, zero, valores largos, nomes extensos, período parcial e resultados ausentes. Reconsultar meses sem refazer efeitos financeiros.

## Aceite e validação

- [ ] DRE mensal e trimestral comparadas diretamente aos concepts nas três resoluções comuns; conclusão e falência conferidas com a mesma hierarquia.
- [ ] Moedas, acentos, sinais, percentuais, totais e colunas não cortam nem se sobrepõem. Cor semântica e alinhamento permanecem corretos.
- [ ] Consultar, trocar abas e expandir detalhes mantém a fonte. Saldos e resultados exibidos continuam vindos dos dados reais, sem mudar cálculo ou classificação.

## Entregáveis

Views/migrations/cena atualizados, evidências visuais e QA dos estados relevantes, com entradas do inventário concluídas.

## Comando para execução

> Execute FONT-08 no projeto D:/Cogna/jogos_empresa após FONT-07. Migre DRE mensal/trimestral, gráficos/detalhes, relatório final, conclusão e falência, preservando dados e validando a leitura contra os concepts.
