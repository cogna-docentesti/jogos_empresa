# FONT-04 — Tema tipográfico, builders e geração em runtime

Status: **concluído** — [QA FONT-04](../qa/FONT-04.json). Prioridade: P0. Dependência: FONT-03.

## Objetivo

Fazer a tipografia nova funcionar por papel visual tanto em assets serializados quanto em objetos criados por código. Trocar somente `FontAssetPath` não resolve o conjunto.

## Arquivos e consumidores

`GameTypography.cs`, `SharedUiSkin.cs`, `LocationSkinV2.cs`, `LocationMockupTokens.cs`, `UiFactory.cs`, `GlobalThemeMigration.cs`, `BackButtonStyler.cs`, builders de identificação/localização, migrations e demais pontos encontrados por FONT-01.

## Implementação

- [x] Ler o [índice](README.md) e os três registros de fontes/assets/matriz produzidos nas dependências.
- [x] Introduzir uma resolução compartilhada de papel → família/peso/material/tamanho. Manter os consumidores existentes funcionando durante a migração, com uma API clara para os novos papéis.
- [x] Garantir referências serializadas ou carregamento runtime compatível com o projeto. A solução do Player não pode chamar `AssetDatabase` nem procurar fontes em `output`.
- [x] Atualizar factories, skins e migrations para respeitarem o papel escolhido. Remover a suposição de uma única família e de que um booleano Regular/Bold descreve todos os pesos.
- [x] Corrigir referências diretas, GUID fixo do `BackButtonStyler` e overrides que repõem a Inter Tight em `OnEnable`, `Bind`, templates ou reestilização.
- [x] Conferir a interação com `<b>`, `<i>`, `fontWeight`, material e estilos. Preservar conteúdo e rich text válido, sem peso sintético cumulativo.
- [x] Manter os tamanhos atuais como início; revisar comentários/métricas que pressupõem a altura de Inter Tight. Expor ajustes por papel, sem condensar via escala de RectTransform.
- [x] Tornar a aplicação idempotente e respeitar a família/peso de cada papel ao criar clones. Não reexecutar migrations antigas completas sem avaliar efeitos na composição atual.

## Aceite e validação

- [x] Cabeçalho, corpo, botão e KPI criados pelo código usam seus assets corretos no Editor em execução.
- [x] Abrir, fechar e revisitar um painel, repopular um catálogo e reabrir a cena mantém as escolhas de família e peso.
- [x] A busca por Inter Tight/GUIDs antigos não encontra caminhos de produção não classificados no inventário.
- [x] Compilação e verificações existentes relevantes de tema passam. Atualizar expectativas obsoletas somente quando medirem a nova regra, sem enfraquecer verificações de layout ou comportamento.

## Entregáveis

Tema/factories/runtime e ferramentas de migração atualizados; contrato proposto com a matriz técnica implementada; QA e inventário atualizados. O acabamento de todas as telas continua nas tarefas seguintes.

## Comando para execução

> Execute FONT-04 no projeto D:/Cogna/jogos_empresa após FONT-03. Integre a resolução de fontes por papel no tema, factories, skins, builders e runtime; valide reentrada e persistência das referências.
