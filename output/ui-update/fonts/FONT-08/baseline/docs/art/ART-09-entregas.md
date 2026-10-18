# ART-09

18 PNGs RGBA transparentes e seus SVGs editáveis, separados conforme o manifesto. Indicadores do HUD e componentes da DRE sem texto, ícones ou dados incorporados.

[Prévia completa](../../art-source/previews/ART-09/ART-09-contact-sheet.png) | [QA](qa/ART-09.json) | [Produção e especificações](prompts/ART-09.json) | [Montagem de progresso em 1:1](../../art-source/previews/ART-09/progress-assembly-1x.png)

| Componente | Export / display em px | PNG final | SVG editável |
| --- | --- | --- | --- |
| Pílula do HUD (UI_HUD_PILL) | [256, 128] / [280, 60] | [hud_pill.png](../../Assets/Art/new/hud/indicators/hud_pill.png) | [hud_pill.svg](../../art-source/editable/hud/indicators/hud_pill.svg) |
| Base de KPI (UI_KPI_TILE) | [256, 128] / [400, 112] | [kpi_tile.png](../../Assets/Art/new/hud/indicators/kpi_tile.png) | [kpi_tile.svg](../../art-source/editable/hud/indicators/kpi_tile.svg) |
| Trilho de progresso (UI_PROGRESS_TRACK) | [128, 64] / [480, 16] | [progress_track.png](../../Assets/Art/new/hud/progress/progress_track.png) | [progress_track.svg](../../art-source/editable/hud/progress/progress_track.svg) |
| Preenchimento de progresso (UI_PROGRESS_FILL) | [128, 64] / [480, 16] | [progress_fill.png](../../Assets/Art/new/hud/progress/progress_fill.png) | [progress_fill.svg](../../art-source/editable/hud/progress/progress_fill.svg) |
| Sublinhado da navegação (UI_NAV_UNDERLINE) | [128, 16] / [240, 4] | [nav_underline.png](../../Assets/Art/new/hud/progress/nav_underline.png) | [nav_underline.svg](../../art-source/editable/hud/progress/nav_underline.svg) |
| Pílula normal (UI_PILL_NORMAL) | [256, 128] / [180, 52] | [pill_normal.png](../../Assets/Art/new/shared/components/badges/pill_normal.png) | [pill_normal.svg](../../art-source/editable/shared/components/badges/pill_normal.svg) |
| Pílula selecionada (UI_PILL_SELECTED) | [256, 128] / [180, 52] | [pill_selected.png](../../Assets/Art/new/shared/components/badges/pill_selected.png) | [pill_selected.svg](../../art-source/editable/shared/components/badges/pill_selected.svg) |
| Pílula desabilitada (UI_PILL_DISABLED) | [256, 128] / [180, 52] | [pill_disabled.png](../../Assets/Art/new/shared/components/badges/pill_disabled.png) | [pill_disabled.svg](../../art-source/editable/shared/components/badges/pill_disabled.svg) |
| Aviso neutro (UI_TOAST_NEUTRAL) | [256, 128] / [680, 88] | [toast_neutral.png](../../Assets/Art/new/shared/components/feedback/toast_neutral.png) | [toast_neutral.svg](../../art-source/editable/shared/components/feedback/toast_neutral.svg) |
| Aviso de sucesso (UI_TOAST_SUCCESS) | [256, 128] / [680, 88] | [toast_success.png](../../Assets/Art/new/shared/components/feedback/toast_success.png) | [toast_success.svg](../../art-source/editable/shared/components/feedback/toast_success.svg) |
| Aviso de atenção (UI_TOAST_WARNING) | [256, 128] / [680, 88] | [toast_warning.png](../../Assets/Art/new/shared/components/feedback/toast_warning.png) | [toast_warning.svg](../../art-source/editable/shared/components/feedback/toast_warning.svg) |
| Cabeçalho da DRE (UI_TABLE_HEADER) | [256, 128] / [1240, 52] | [table_header.png](../../Assets/Art/new/dre/components/table_header.png) | [table_header.svg](../../art-source/editable/dre/components/table_header.svg) |
| Linha neutra da DRE (UI_TABLE_ROW_NEUTRAL) | [256, 128] / [1240, 48] | [table_row_neutral.png](../../Assets/Art/new/dre/components/table_row_neutral.png) | [table_row_neutral.svg](../../art-source/editable/dre/components/table_row_neutral.svg) |
| Subtotal da DRE (UI_TABLE_ROW_SUBTOTAL) | [256, 128] / [1240, 48] | [table_row_subtotal.png](../../Assets/Art/new/dre/components/table_row_subtotal.png) | [table_row_subtotal.svg](../../art-source/editable/dre/components/table_row_subtotal.svg) |
| Resultado positivo da DRE (UI_TABLE_ROW_POSITIVE) | [256, 128] / [1240, 48] | [table_row_positive.png](../../Assets/Art/new/dre/components/table_row_positive.png) | [table_row_positive.svg](../../art-source/editable/dre/components/table_row_positive.svg) |
| Resultado negativo da DRE (UI_TABLE_ROW_NEGATIVE) | [256, 128] / [1240, 48] | [table_row_negative.png](../../Assets/Art/new/dre/components/table_row_negative.png) | [table_row_negative.svg](../../art-source/editable/dre/components/table_row_negative.svg) |
| Coluna de total da DRE (UI_TABLE_TOTAL_COLUMN) | [128, 256] / [180, 600] | [table_total_column.png](../../Assets/Art/new/dre/components/table_total_column.png) | [table_total_column.svg](../../art-source/editable/dre/components/table_total_column.svg) |
| Superfície do gráfico (UI_CHART_PANEL) | [256, 256] / [560, 300] | [chart_panel.png](../../Assets/Art/new/dre/components/chart_panel.png) | [chart_panel.svg](../../art-source/editable/dre/components/chart_panel.svg) |

Bordas 9-slice e pivot registrados no manifesto. Estados de pílulas, avisos e linhas da DRE preservam alfa, bounds e pivot. Trilho e preenchimento de progresso permanecem independentes.

Pendências: nenhuma.
