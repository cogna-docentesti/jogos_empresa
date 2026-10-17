# FONT-05 — Componentes compartilhados, HUD, hub e entrada

Status: **pendente**. Prioridade: P1. Dependência: FONT-04.

## Escopo

UI-01, UI-03, UI-04, UI-05 e navegação compartilhada de UI-19: identificação, localização, hub/mapa, cabeçalho, navbar, voltar e componentes de `Assets/Prefabs/SharedUI`.

## Referências

[Índice e matriz](README.md), concepts `01-hub.png` e `02-abertura.png`, `GameScene.unity`, `ConceptHudView`, componentes de identificação/localização, `SharedControlAppearance` e consumidores inventariados de navegação.

## Implementação

- [ ] Aplicar Roboto Bold à marca/títulos e valores do HUD; Roboto Regular ao corpo e aos campos; Roboto Condensed aos marcadores, navegação, legendas e ações, conforme papel.
- [ ] Atualizar textos e placeholders de inputs, dropdowns usados e seus templates, toggles, tabs, filtros, botões e estados. Conferir overrides de instâncias na cena depois de atualizar o prefab.
- [ ] Revisar rótulos do mapa e painel lateral, sem alterar a transformação uniforme do cenário. Diferenciar nome, valor e informação secundária pela matriz.
- [ ] Ajustar altura útil, baseline, padding e quebras onde a nova métrica exigir. Usar altura aparente comparável ao concept; não reduzir globalmente para caber.
- [ ] Conferir normal, hover, pressed, selecionado, desabilitado, foco e erro. Atualizações de mês/caixa/score não podem alterar o papel tipográfico.
- [ ] Testar nome longo, acentos e RA, incluindo caret, seleção, placeholder e scroll horizontal. Manter a navegação e a confirmação da identificação.

## Aceite e validação

- [ ] Comparação lado a lado de identificação, localização e hub com os concepts em 1920×1080 e conferência em 1280×720 e 1600×1000.
- [ ] Sem clipping de acentos/descendentes, rótulos sobre controles, perda de máscaras ou novos bloqueios de raycast.
- [ ] Sair/entrar nos painéis e atualizar HUD preserva família/peso. Prefabs compartilhados e instâncias da cena usam a mesma regra.

## Entregáveis

Cena, prefabs, views e builders do escopo atualizados; capturas reais, inventário por consumidor e QA. Registrar componentes sem consumidor ativo, sem inventar telas para demonstrá-los.

## Comando para execução

> Execute FONT-05 no projeto D:/Cogna/jogos_empresa após FONT-04. Aplique e confira a tipografia nos componentes compartilhados, identificação, localização, hub, HUD e navegação com evidências reais nas três resoluções.
