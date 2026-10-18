# FONT-07 — Banco, equipamentos e equipe

Status: **concluído**. Prioridade: P1. Dependência: FONT-06.

## Escopo e arquivos

UI-09 a UI-11: banco, loja/carrinho e equipe/RH. Ler o [índice](README.md), o concept `03-gestao.png`, `GameScene.unity`, `Card_loan.prefab`, `Card_equipment.prefab`, `TeamRoleCard.prefab`, `EquipmentCartLine.prefab`, painéis e views/factories correspondentes.

## Implementação

- [x] Aplicar o papel correto a título de produto/cargo/contrato, descrição, preço, juros, prazo, quantidade, salário, capacidade, total e ação.
- [x] Usar Roboto Regular para explicações e valores de corpo, Roboto Bold para totais e Roboto Condensed para títulos compactos, rótulos e botões, conforme a matriz.
- [x] Atualizar todos os cards e linhas gerados por código, inclusive o carrinho e mensagens de saldo/capacidade. Não deixar uma fonte distinta no último item ou num card de estado especial.
- [x] Aferir as quatro opções de crédito, os 16 equipamentos e as cinco funções reais de equipe. Não criar conteúdo de catálogo a partir das ilustrações.
- [x] Rever largura de preços e quantidades, baseline dos controles +/− e altura de descrições. Manter o mínimo de altura dos cards e os filtros/scroll operantes.
- [x] Testar normal, selecionado, adquirido, obrigatório, indisponível e carrinho vazio. Confirmar que bind, filtro, incremento e retorno ao painel preservam os papéis.

## Aceite e validação

- [x] Banco, equipamentos e equipe comparados ao concept em 1920×1080, 1280×720 e 1600×1000; incluir o final dos catálogos e resumo do carrinho.
- [x] Contratos, preços e custos de equipe não cortam nem saltam de família/peso quando o valor muda.
- [x] Filtros, compra, contratação, confirmação, retorno e reentrada funcionam; economia e persistência mantêm o comportamento anterior.

## Entregáveis

Prefabs/cena/views/fábricas atualizados, registros no inventário e QA com capturas de estados do catálogo e textos longos.

## Comando para execução

> Execute FONT-07 no projeto D:/Cogna/jogos_empresa após FONT-06. Migre a tipografia do banco, loja/carrinho e equipe, cubra todos os templates e estados e valide visualmente contra o concept.
