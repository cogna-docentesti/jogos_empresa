# Calendário mensal de 30 dias

Contrato funcional da FB-21 · 19/11/2026 · base de código `6c576e28`. Aplica-se aos três meses da campanha. O jogador vê as 30 casas, avança até o próximo compromisso e decide os eventos quando chega ao dia. O caixa acompanha os pagamentos durante o percurso. A DRE apresenta o mês inteiro.

## Entrada, revisão e retorno ao hub

O botão **Calendário** do hub abre a grade inteira de 1 a 30, legível no mesmo quadro, inclusive em 1280×720. Cada casa mantém seu número mesmo quando vazia. Na primeira entrada há uma revisão das **Decisões do mês** sobre a grade, antes de iniciar o percurso. O jogador pode voltar ao hub para ajustar suas escolhas. A confirmação exige a quantidade mínima de cada função em `RestaurantData.requiredRoles`, conforme FB-14. Uma tentativa inválida informa as faltas e permite corrigi-las.

Confirmar a revisão fixa a abertura do mês em uma transação. Usar a sessão persistida depois das contratações, compras e escolhas de crédito e cardápio. Guardar caixa, dívida, reputação, equipe, equipamento, preços, local, restaurante e os parâmetros econômicos aplicáveis àquele mês. Reservar os eventos com esse mesmo contexto e guardar as contas com seus valores e datas. A grade passa a mostrar as contas e os eventos anunciados antes do primeiro **Avançar**. Na preparação, os dados públicos disponíveis são a grade e as escolhas em revisão. Uma consulta ou renderização nunca dispara o sorteio.

Até a confirmação, as escolhas são editáveis. Durante o percurso, o retorno ao hub entre paradas permite consultas e conserva a posição. As entradas de edição de equipe, preços, compra e crédito só voltam a aceitar mudanças para a rodada seguinte após a DRE. Aplicar essa regra também nos comandos de domínio para que uma revisita ou tela antiga não altere a base de um mês já em andamento. Um modal obrigatório ou movimento em curso mantém o foco no calendário. Sair e carregar novamente recupera o estado salvo.

## Avanço e estados persistentes

| Estado lógico | Comando e transição | Retomada |
| --- | --- | --- |
| Em preparação | Confirmar a revisão abre o plano mensal. O cursor lógico começa em 0, antes da casa 1. | Reabrir a revisão se ainda não confirmada. O dia 0 nunca ocupa uma casa na grade. |
| Aguardando avanço | **Avançar** inicia um movimento. O destino público é o próximo compromisso conhecido ou o marco 30. Internamente, a primeira parada é o menor dia pendente à frente, incluindo surpresas, ou 30 se a agenda se esgotou. | Recuperar a última casa concluída e o destino público. Uma conta ou evento no dia 1 participa do primeiro avanço. |
| Em movimento | Guardar origem, destino público, parada privada e identidade do comando antes de iniciar a animação. Percorrer as casas em ordem. A chegada autorizada pelo domínio processa apenas aquela data. | Se a chegada ainda não foi gravada, retomar o movimento desde a origem persistida com o mesmo comando. Repetir quadros é permitido. A chegada já gravada retoma diretamente seu resultado. |
| Chegada ao dia | Numa só transação, posicionar o cursor, quitar as contas do dia, registrar os recibos e, se houver evento, revelar a primeira ocorrência ainda sem decisão e marcá-la pendente. | Mostrar o caixa já atualizado, os comprovantes e, se couber, o popup pendente. |
| Aguardando decisão | O popup mostra título e **todas as opções reais** do `EventData`, com seus efeitos. Confirmar grava a opção e o histórico. Se houver outro evento no mesmo dia, abrir o próximo. Voltar, X, clique no fundo ou segunda chamada não pulam uma escolha. | Antes da confirmação, reabrir a mesma escolha. Depois dela, retomar a próxima pendência ou devolver o foco a **Avançar**, no mesmo dia. |
| Dia concluído | Permanecer na casa alcançada. O próximo clique prossegue em direção ao próximo compromisso, inclusive ao destino público que ficou além de uma interrupção. | Voltar do hub mantém dia, pagamentos e escolhas. Casas anteriores são consultáveis. |
| Fim do dia 30 | Primeiro resolver contas e escolhas do dia. Com revisão confirmada, todas as contas quitadas e nenhuma ocorrência pendente, o marco final aciona o fechamento desse mês. | Falha de fechamento deixa o dia 30 pronto para tentar novamente. Um resultado já gravado abre a DRE correspondente. |
| DRE aguardando Continuar | Ler o resultado persistido. **Continuar** reconhece seu ID e segue ao hub, relatório final ou falência. | O resultado pendente prevalece sobre um calendário ativo de outro mês. |

Em empates, processar as contas em ordem **aluguel → custos operacionais fixos → folha → empréstimo**. Depois, processar eventos em ordem ordinal de `eventId`, um popup por vez. No dia 30, o fechamento sempre vem por último. Um dia com duas contas tem uma chegada, dois recibos e um novo saldo. Uma conta gera feedback de valor debitado e saldo, sem pedir uma decisão de pagamento.

O modelo público só entrega a casa atual, contas, eventos anunciados ou já revelados, estados e destino **público**. Para a animação, o domínio autoriza o próximo passo por casa e entrega uma interrupção ao alcançar sua data. Antes disso, dois planos que diferem apenas por uma surpresa futura têm a mesma grade, navegação, detalhes, contadores, anúncio de acessibilidade e destino visível. Isso também vale para foco, tooltip, legenda, cores, badges, duração anunciada do movimento, histórico e numeração do tipo “evento 1 de N”. Na casa que já tenha conta ou show, o futuro imprevisto tampouco altera a marcação. O histórico acessível pelo hub contém decisões confirmadas.

## Contas e lançamentos econômicos

As datas abaixo são padrões configuráveis para novos planos. A configuração aceita dias inteiros de 1 a 30 e permite coincidências. A abertura valida e fixa as datas antes de qualquer débito. Alterar a configuração só afeta planos futuros. Cada linha gera no máximo uma ocorrência por mês com chave única **(sessionId, mês, tipo, ordinal 1)**. Dia, valor e nome apresentado são atributos, fora da chave. Usar o ID de tipo da tabela tanto na obrigação quanto no recibo.

| Tipo e vencimento padrão | Valor fixado na abertura | Lançamento no dia e na DRE |
| --- | --- | --- |
| `rent` · dia 5 | `LocationData.rent` da zona da sessão. | Debitar o aluguel integral. Linha `rent`. |
| `utilities` · dia 10 | `RestaurantData.baseMonthlyCost` do restaurante. | Debitar o custo fixo operacional integral. Linha `utilities`. |
| `salaries` · dia 25 | Soma de `RoleData.salary × quantity` por `roleId` em `teamJson`. O custo já inclui os encargos do modelo. | Uma folha pelo total. Linha `salaries`. |
| `loanPayment` · dia 28 | Havendo dívida na abertura, usar a linha `creditLineId`. Principal = saldo dividido por `4 − mês`, arredondado conforme o motor. No mês 3, usar todo o saldo restante. Parcela = principal + juros à taxa mensal sobre o saldo de abertura, com arredondamento da parcela pelo motor. | Debitar a parcela e amortizar o principal no mesmo commit. Guardar ambos no recibo. `loanPayment` inclui os juros e o principal. `loanPrincipalPayment` detalha o principal. |

Uma rubrica de valor zero continua valendo zero na DRE e dispensa compromisso. Dívida zero dispensa parcela. Uma dívida positiva exige a linha de crédito correspondente. Manter o trimestre sem 13º (`thirteenthSalary = 0`) e a restrição de novos empréstimos no terceiro mês. Compra de equipamento e entrada de crédito anteriores à abertura já compõem o caixa inicial. O valor de aquisição não vira despesa mensal novamente.

Insumos (`supplyCost`) dependem das vendas e dos efeitos dos eventos. Apurá-los no fechamento com a receita (`grossRevenue`). O popup registra a escolha, inclusive o `cashDelta` de um conserto. Seus efeitos financeiros e de reputação entram no fechamento a partir do histórico. Assim, apenas as quatro rubricas prefixadas movimentam o caixa durante o percurso. Saldo insuficiente permite o débito integral e caixa negativo. A falência é avaliada no fechamento pelos resultados mensais.

## Reserva, datas e visibilidade dos eventos

Para cada novo plano, chamar uma vez `MonthlyEventSelector.SelectEventsForMonth` pelo fluxo de `MonthlyEventDecisionService.OpenMonth`. Usar sessão e mês 1–3, restaurante, catálogos de eventos/equipamentos, histórico da campanha, IDs reservados, grupos usados e quantidade já selecionada no mês. A revisão confirmada oferece o contexto econômico que hoje está disponível ao abrir decisões após o planejamento. Manter o contrato do seletor inclusive para catálogos parciais e dados inválidos.

O padrão é alvo 4, mínimo 3, máximo 5, contando as reservas existentes. O seletor valida limites, IDs, pesos e definições. Respeita compatibilidade de restaurante, intervalo de rodadas, condições cumulativas do `EventConditionEvaluator`, `canRepeat` e os grupos de exclusão. Ordena candidatos por ID ordinal, sorteia com os `baseWeight` e retira o ID e os pares de seu grupo diferente de `NONE`. Pode retornar menos que o mínimo, inclusive zero, quando faltam candidatos elegíveis. Conservar os diagnósticos. Por exemplo, a quebra depende de qualidade agregada de equipamento ≤ 1, sem exigir a posse de um item chamado freezer. A regra autorizada de mínimo da equipe pode afetar o contexto de entrada. O limiar da condição de ausência de funcionário continua o do catálogo.

Após a seleção, para cada ID novo na ordem devolvida, sortear um dia uniforme em **1–30 inclusive, com reposição**, usando gerador independente do gerador de incidência. Persistir ID, dia e classe de anúncio junto com o plano, inclusive o plano vazio. Colisões mantêm ambos os compromissos. Um plano salvo prevalece sobre alterações no catálogo de datas e novas consultas. A mesma entrada e sequência aleatória do seletor deve produzir os mesmos IDs na mesma ordem, quaisquer que sejam os dias sorteados.

O catálogo chama o show do exemplo de **Festival na região**. Cada linha abaixo define o metadado da ocorrência selecionada, com o título do asset. O critério de anúncio é a existência de uma data que o restaurante poderia conhecer ao planejar o mês.

| ID | Título do catálogo | Visibilidade | Motivo |
| --- | --- | --- | --- |
| `bulk_purchase_opportunity` | Oportunidade de compra em volume | Surpresa | A proposta comercial surge no contato com o fornecedor. |
| `competitor_temporary_closure` | Fechamento temporário de concorrente | Surpresa | O restaurante percebe o fechamento no dia, sem aviso prévio no catálogo. |
| `corporate_order` | Pedido corporativo | Surpresa | A ocorrência é a chegada da solicitação da empresa. |
| `excessive_food_waste` | Desperdício acima do esperado | Surpresa | O excesso é identificado na operação. |
| `extra_local_demand` | Aumento inesperado da demanda local | Surpresa | O movimento adicional é inesperado. |
| `freezer_breakdown` | Quebra do freezer | Surpresa | A falha é descoberta quando o equipamento para. |
| `health_inspection` | Fiscalização sanitária | Surpresa | A ocorrência começa com a chegada da fiscalização. |
| `heavy_rain` | Chuva forte | Surpresa | O catálogo descreve o impacto da chuva e não fornece um alerta meteorológico datado. |
| `influencer_mention` | Menção de influenciador local | Surpresa | A publicação é espontânea. |
| `ingredient_price_increase` | Aumento no preço dos ingredientes | Surpresa | O reajuste é conhecido quando o fornecedor apresenta os novos preços. |
| `insufficient_team` | Funcionário ausente | Surpresa | A falta se manifesta no turno. |
| `local_festival` | Festival na região | Anunciado | A programação pública do festival permite marcar o dia com antecedência. |
| `lunch_demand_peak` | Pico de demanda no horário de almoço | Surpresa | O horário do almoço é conhecido, mas o excesso de clientes só se constata no dia. |
| `negative_review` | Avaliação negativa nas redes | Surpresa | A avaliação só existe após a publicação. |
| `new_competitor` | Novo concorrente na região | Anunciado | Para o calendário, a inauguração comercial tem data divulgada no bairro. |
| `payment_system_failure` | Falha no sistema de pagamento | Surpresa | A instabilidade surge durante as vendas. |
| `positive_online_review` | Avaliação positiva nas redes | Surpresa | A repercussão da avaliação é conhecida após a publicação. |
| `supplier_delay` | Atraso do fornecedor | Surpresa | O atraso se revela quando a entrega esperada falha. |
| `supplier_discount` | Desconto do fornecedor | Surpresa | A oferta é conhecida quando o fornecedor a faz. |
| `team_productivity_boost` | Aumento de produtividade da equipe | Surpresa | O ganho de ritmo é observado durante o atendimento. |

Ao chegar, eventos das duas classes abrem o mesmo fluxo de decisão. Validar sessão, mês, ocorrência reservada, dia alcançado e opção pertencente ao asset no comando de confirmação. O histórico existente identifica a ocorrência por sessão, mês, evento e dia e guarda `selectedChoiceId`, textos, deltas e `effectsJson`. A confirmação repetida da mesma opção retorna o registro original. Uma opção conflitante não substitui a primeira.

O motor continua usando o dia registrado. Multiplicadores `IMMEDIATE` e `CURRENT_DAY` afetam o próprio dia e `CURRENT_MONTH` afeta desse dia ao 30 inclusive. Efeitos simultâneos compõem-se como hoje. Os deltas diretos de caixa e reputação entram uma vez por ocorrência, e `clientsChange` pertence ao dia. A distribuição temporal pode alterar vendas e insumos, sobretudo para um efeito até o fim do mês. A previsão usa a mesma abertura e os mesmos dias das escolhas já confirmadas. Quando todas estiverem registradas, previsão e fechamento devem coincidir. Uma previsão parcial jamais usa o plano secreto para antecipar impactos.

## Persistência e reconciliação

Guardar por sessão/mês a versão do plano, revisão, abertura econômica, contas, agenda privada, cursor, comando de movimento, estado de revelação e ocorrência pendente. Conservar os vínculos com `MonthlyEventPlanEntity`, `SessionEventHistoryEntity` e `RoundResultEntity`. Cada recibo traz chave da obrigação, dia, valor em centavos, saldo anterior e posterior e, para crédito, principal e juros. O dia simultâneo usa a ordem definida acima, o que torna os saldos intermediários auditáveis.

Cada comando valida o mês e a revisão esperados na própria transação SQLite. A chegada grava cursor, todos os recibos do dia, caixa, dívida e primeira pendência de decisão atomicamente. A confirmação grava histórico, conclusão da ocorrência e próxima pendência atomicamente. Uma repetição do comando já confirmado lê o resultado gravado. Uma requisição obsoleta não avança outro dia ou mês. Duplo toque, concorrência e perda da resposta após commit têm o mesmo efeito de uma única confirmação. Publicar a sessão em memória e liberar o input apenas depois do commit. Em rollback, reler o estado durável e repetir o mesmo comando. Falta de persistência impede iniciar ou continuar o percurso econômico.

Para o fechamento, sejam **A** o caixa de abertura, **P** a soma positiva dos recibos e **N** o `netResult` integral da DRE. Os valores usam os centavos do motor, com arredondamento monetário a duas casas, afastando do zero nos empates. Conservar as colunas e a leitura histórica de floats, reconciliando valores monetários em centavos.

1. Calcular a DRE com a cópia da abertura, os dados econômicos do plano e todos os snapshots confirmados. **N = receita − insumos − aluguel − folha − custos fixos operacionais − parcela − 13º + impacto assinado dos eventos**. Manter cada custo e os detalhes da parcela nas linhas existentes.
2. Verificar o conjunto de chaves, valores e componentes de **cada** conta contra a DRE, sem aceitar um total coincidente com recibos trocados. Exigir que `currentCash = A − P` e que a dívida corrente seja a de abertura menos o principal já amortizado. Uma inconsistência bloqueia o commit e preserva o mês para diagnóstico.
3. O ajuste restante de caixa é **N + P**. O saldo final precisa satisfazer **(A − P) + (N + P) = A + N**. Persistir esse saldo absoluto e a dívida final calculada a partir da abertura. Alimentar `MonthlySessionSettlement.Apply` com a base de abertura ao calcular a amortização. Aplicá-lo à dívida corrente já reduzida amortizaria o principal duas vezes.
4. Usar o mês explícito em `RoundService.ProcessRound(expectedMonth)`. Em uma transação, validar o dia 30 completo, inserir o único resultado da sessão/mês, registrar o calendário fechado, atualizar sessão, reputação, dívida e `pendingSummaryResultId`. Retry após commit retorna a mesma DRE. Todas as entradas de fechamento, inclusive atalhos do hub e caminhos antigos de `CloseMonth` e `ProcessCurrentRound`, respeitam essa barreira no domínio.
5. O resultado negativo soma um mês à sequência de prejuízos. Resultado zero ou positivo zera o contador. Três resultados negativos consecutivos dão precedência a `BANKRUPT` no mês 3. Nos demais términos do terceiro mês, segue `COMPLETED`. A sessão pode já conter o número do mês seguinte enquanto aguarda a DRE. Só `ContinueAfterSummary(resultId)` libera o hub do próximo mês ou a tela final correspondente. Reabrir resumo, relatório ou histórico é consulta.

### Leitura de saves anteriores

| Dados encontrados | Ação na primeira retomada |
| --- | --- |
| Rascunho antes de D3, resumo pendente, resultado já salvo ou campanha encerrada | Preservar o fluxo correspondente. Um mês que já tem resultado não ganha contas nem sofre recálculo. Manter reconhecimento do resumo, arquivamento e destino final existentes. |
| Mês ativo sem calendário nem plano | Entrar em preparação. O fluxo antigo cobrava as contas somente ao fechar. Na confirmação, usar os saldos persistidos de abertura, ledger vazio e cursor 0. |
| Plano anterior em `entriesJson`, inclusive vazio ou parcialmente decidido | Importar IDs e dias originais. Unir com o histórico do mês, casar por evento/dia e manter os snapshots já confirmados. Iniciar o percurso de contas no cursor 0 para alcançar todos os vencimentos. Escolhas legadas já conhecidas continuam no histórico, contam para a simulação e dispensam nova parada de decisão. Uma reserva ainda sem escolha segue as regras de anúncio e revelação. |
| Histórico ativo sem plano | Preservar as ocorrências, seus dias e escolhas, inclusive deltas antigos sem `effectsJson`. Ao abrir, passar ao fluxo existente todos os IDs ocorridos, reservas do mês, grupos usados e quantidade. Sortear datas apenas para os novos IDs retornados. |
| Calendário versionado já existente | Retomar exatamente a última transação. Reexecutar a migração nunca infere pagamentos do saldo, reseta o cursor ou refaz a reserva. |

Migrar atomicamente e preservar IDs, snapshots e valores numéricos dos enums serializados. Para um plano legado ainda ativo, a revisão mensal pode corrigir uma equipe incompleta antes de iniciar o percurso. As reservas que já existiam conservam sua incidência. Duplicata conflitante, dia fora de 1–30 no plano ativo, ID reservado sem catálogo/opções para decidir, versão desconhecida ou resultado incompatível produzem diagnóstico recuperável e preservam o original. Um evento histórico já confirmado pode ser lido por seu snapshot mesmo que seu asset tenha sido retirado. Continuam válidos o tratamento de save incompatível e a preservação de campanhas encerradas.

## Percurso de referência para o aceite

Mês 1, abertura de **R$ 12.000**, Lanches na universidade, dois atendentes e um chapeiro, sem dívida. Os catálogos dão aluguel de R$ 6.000, folha de R$ 6.900 e custos operacionais de R$ 1.500. **Neste cenário** o aluguel foi configurado para o dia 20. O plano de quatro eventos usa IDs compatíveis entre si e um contexto elegível para a quebra. As datas são entradas controladas para a conferência.

| Momento | Observação esperada | Caixa |
| --- | --- | ---: |
| Abertura | As 30 casas aparecem juntas. O festival do dia 8 e as contas dos dias 10, 20 e 25 são públicos. As demais ocorrências não aparecem em nenhuma projeção futura. | R$ 12.000 |
| Dia 4 | Avançar rumo ao festival revela `bulk_purchase_opportunity`. Confirmar `decline_purchase`. | R$ 12.000 |
| Dia 8 | O festival anunciado abre suas opções. Confirmar `normal_operation`, com o multiplicador real de demanda de 1,3 nesse dia. | R$ 12.000 |
| Dia 10 | Custos operacionais debitados. O próximo compromisso público é o aluguel do dia 20. | R$ 10.500 |
| Dia 16 | **Avançar** em direção ao dia 20 para na quebra do freezer. Recarregar agora reabre a quebra com as três opções originais. Confirmar `repair_now` registra −R$ 3.500 para o fechamento. Recarregar depois restaura o dia 16 com a escolha já feita. | R$ 10.500 |
| Dia 20 | Novo avanço alcança o aluguel. A chegada exibe o recibo de R$ 6.000. Retry ou reload retorna o mesmo recibo. | R$ 4.500 |
| Dia 25 | Folha debitada primeiro. Em seguida a falha do sistema de pagamento abre seu popup. Confirmar `emergency_support` registra −R$ 500 para o fechamento. O caixa negativo permite continuar. | −R$ 2.400 |
| Dia 30 | Sem outras pendências, atingir o marco final e abrir a DRE. Se a gravação falhar, repetir para este mesmo mês. Se a resposta se perder após o commit, reabrir a DRE salva. | Saldo final da DRE |

Para isolar a conciliação, tomar como entradas ilustrativas da DRE receita de R$ 30.000 e insumos de R$ 10.000, já com os efeitos operacionais. Os recibos totalizam R$ 14.400 e as duas escolhas de caixa somam −R$ 4.000. O resultado integral é **R$ 1.600**. O ajuste no fechamento é **+R$ 16.000** e o caixa final é **R$ 13.600**, igual a abertura mais resultado. Essa conferência não estima as vendas da campanha. Com o resultado salvo, a tela aguarda **Continuar** para liberar o mês 2.

Em um empate de duas contas, configurar aluguel e custos operacionais no dia 20 da mesma abertura. A chegada debita R$ 6.000 e R$ 1.500, com saldos intermediário de R$ 6.000 e final de R$ 4.500. Falha antes do commit restaura R$ 12.000 e zero recibos do dia. Em outro caso, um microcrédito de R$ 50.000 contratado no mês 2 tem parcela de R$ 26.000 e dívida de R$ 25.000 após o pagamento. No mês 3, a última parcela é R$ 25.500 e a dívida chega a zero no dia do débito. As duas DREs conservam essas parcelas por inteiro.

O [registro de QA](qa/FB-21.json) identifica as fontes, os cálculos conferidos e os limites desta validação documental.
