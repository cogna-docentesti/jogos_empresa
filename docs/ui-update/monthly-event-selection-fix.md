# Seleção mensal de eventos — 25/11/2026

## Regra e causa

A orientação recebida da programadora confirmou o contrato de uma campanha de três meses: reservar quatro ou cinco eventos na abertura de cada mês, sem reposição durante a campanha. Eventos surpresa devem existir no plano privado, mas só aparecer no calendário quando ocorrerem.

Após a integração do motor sistêmico, o calendário passou a executar sorteios diários por funcionário, equipamento e evento global. A ausência de funcionário podia se repetir várias vezes. Os registros distintos encontrados no banco não eram duplicatas de apresentação; ainda assim, esse comportamento contrariava o contrato de seleção mensal. O catálogo também continha o mesmo incidente de ausência sob `insufficient_team` e `staff_absent`.

## Alteração

- `MonthlyEventPlanner` seleciona eventos elegíveis por peso, sem reposição, define os dias antes de começar e exclui os IDs canônicos já ocorridos.
- O plano é persistido junto aos dados congelados do calendário. Reabrir, avançar ou retomar uma decisão não refaz o sorteio.
- A simulação revela e aplica apenas o incidente reservado para o dia. As fórmulas de vendas, capacidade, custos e contabilidade continuam no motor sistêmico.
- As duas representações de funcionário ausente compartilham uma exclusão canônica, inclusive quando o histórico vem do motor antigo.
- O calendário projeta antecipadamente apenas eventos anunciados; surpresas permanecem privadas até a ocorrência.
- O fechamento grava o plano no relatório para permitir conferência e reprodução exatas. A previsão usa a mesma seleção mensal para meses futuros.
- Pagamentos e decisões financeiras reais de insuficiência de caixa foram preservados. Quando há dívida, o sorteio reserva quatro incidentes para deixar espaço para a decisão da parcela, se necessária, mantendo o limite mensal de cinco apresentações.

## Compatibilidade

Jornais anteriores à correção continuam a ser reproduzidos com a regra original até o fechamento do mês já aberto. Não há exclusão de partidas, recálculo de escolhas já confirmadas ou alteração retroativa do caixa. Meses novos usam o plano mensal corrigido.

## Evidências

- Compilação dos assemblies do jogo e do Editor: aprovada, com um aviso preexistente em `SharedUiImportRules.Manifest.assets`.
- 45 métodos de teste NUnit de domínio/persistência executados fora do Editor, sem falhas: seleção de três meses, ausência com probabilidade diária extrema, aliases, plano estável, eventos anunciados, surpresas ocultas, replay antigo, falha de gravação, confirmação repetida, caixa e fechamento.
- 60 simulações mensais com o catálogo real congelado de uma partida, sem escrever no banco: todas reservaram quatro ou cinco eventos e respeitaram a exclusão entre os três meses de cada campanha.
- Relatórios: `output/merge-motor-novo/monthly-event-plan-validation.json` e `output/merge-motor-novo/monthly-event-plan-real-catalog.json`.
- Play Mode não foi validado nesta execução; a validação acima usa os assemblies compilados, o domínio e SQLite local.

O áudio e a transcrição ficaram fora do repositório.
