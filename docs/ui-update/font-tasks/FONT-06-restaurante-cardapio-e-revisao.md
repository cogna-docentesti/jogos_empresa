# FONT-06 — Restaurante, cardápio, estabelecimento e revisão

Status: **pendente**. Prioridade: P1. Dependência: FONT-05.

## Escopo e arquivos

UI-06, UI-07 e UI-08: seleção de restaurante/público, preços, meu estabelecimento e revisão. Inspecionar `GameScene.unity`, `Card_ProductItem.prefab`, `Button_Segment.prefab`, views e migrations correspondentes e suas fábricas.

Ler o [índice](README.md), o inventário e os concepts `02-abertura.png` e `03-gestao.png`.

## Implementação

- [ ] Aplicar papéis a nomes de restaurante, público, títulos de cards, descrições, restrições, legenda, valores e botões. Usar fonte normal no corpo/preço e condensada nas funções compactas previstas.
- [ ] Cobrir os três restaurantes, as combinações de público existentes e todos os produtos. Atualizar textos criados em runtime e templates, não apenas o primeiro card visível.
- [ ] Revisar os inputs de preço, sufixos/prefixos, limites, avisos e estados inválidos. Testar números com larguras diferentes e moeda em português.
- [ ] Revisar KPIs, checklist, textos de compatibilidade e detalhes expansíveis. Distinguir totais e títulos sem engordar artificialmente o material.
- [ ] Ajustar caixas e entrelinha para nomes longos e acentos; preservar fachada/prato, proporção de imagens e composição da revisão anterior.
- [ ] Conferir seleção, bloqueio, edição inicial e consulta no hub. Rebind/reentrada não pode restaurar fonte antiga.

## Aceite e validação

- [ ] Capturas de seleção, cardápio e estabelecimento/revisão em 1920×1080, 1280×720 e 1600×1000, comparadas aos concepts correspondentes.
- [ ] Nomes, preços e valores são legíveis, sem sobreposição e com hierarquia coerente. Últimos itens continuam acessíveis por scroll.
- [ ] Preços e escolhas permanecem nos mesmos modelos; a migração não altera IDs, cálculos, quantidade de gravações SQLite nem regras de confirmação.

## Entregáveis

Cena/prefabs/views do escopo atualizados e verificados; inventário e QA por tarefa com estados e casos reais, usando armazenamento isolado para validação.

## Comando para execução

> Execute FONT-06 no projeto D:/Cogna/jogos_empresa após FONT-05. Migre restaurante/público, cardápio, estabelecimento e revisão, inclusive conteúdo dinâmico e inputs, e registre a comparação com os concepts.
