# FONT-09 — Eventos, diálogos, feedback e ajuda

Status: **pendente**. Prioridade: P1. Dependência: FONT-08.

## Escopo e referências

UI-12, UI-17 e UI-18: decisões de evento e histórico, confirmações de fechar mês/sair/reset/compra, tutorial, tooltip, toast, loading, vazio e erro/retry. Ler o [índice](README.md), os consumidores do inventário, `EventScreenView`, `EventOptionCardView`, `FeedbackToast`, `SharedTooltip`, `SharedLoading`, controladores de diálogos, `EventOptionCard.prefab` e `Assets/Prefabs/SharedUI`.

## Implementação

- [ ] Aplicar Roboto Regular às explicações, Roboto Bold aos destaques e Condensed aos títulos compactos, legendas e ações, conforme a matriz compartilhada.
- [ ] Atualizar opções de evento, efeitos monetários/reputação, histórico e rótulos gerados por código. Cobrir os 20 eventos e as opções reais do catálogo sem alterar seus efeitos.
- [ ] Migrar título, corpo e todas as ações dos diálogos, inclusive cancelamento e erro. Conferir instâncias criadas depois de abrir a cena.
- [ ] Migrar os textos de feedback e ajuda, incluindo cabeçalhos curtos, corpo longo e estado vazio/loading. Não incorporar texto a sprites para resolver layout.
- [ ] Ajustar altura de toast/tooltip/modal e quebra de linha quando necessário. Preservar scrim, máscaras, foco, ordem de tabulação e tamanho clicável dos botões.
- [ ] Revisar o conjunto após abrir/fechar repetidamente, disparar mensagem longa e retornar de outro painel. Evitar materiais ou atlas alocados por mensagem.

## Aceite e validação

- [ ] Conferir em 1920×1080, 1280×720 e 1600×1000 exemplos reais de evento, histórico, confirmação, tooltip, toast, ajuda e estados vazios/erro disponíveis.
- [ ] Sem overflow de corpo, botão com legenda cortada ou troca de família ao criar um clone. Mensagens de erro mantêm leitura e foco.
- [ ] Confirmar/cancelar mantém a ação anterior: não repetir fechamento de mês, compra, reset ou gravação. Testar com SQLite/PlayerPrefs isolados.

## Entregáveis

Componentes, prefabs, views e instâncias do escopo atualizados; inventário fechado para conteúdo transitório e QA com evidências por estado.

## Comando para execução

> Execute FONT-09 no projeto D:/Cogna/jogos_empresa após FONT-08. Migre eventos/histórico, diálogos, feedback, tutoriais e estados de apoio; valide textos longos, clones, foco e ações existentes.
