# ART-13 — Marcadores, realces e rótulos vazios do mapa

Treze PNGs separados com transparência e treze SVGs editáveis. O lote contém seis realces compactos derivados dos polígonos canônicos, três estados de pino, três estados de rótulo vazio e uma ponteira tintável independente.

| Realce | PNG | Export (px) | X,Y no master | KB (decimais) |
| --- | --- | --- | --- | --- |
| Área Financeira | [selection_bank.png](../../Assets/Art/new/location/overlays/selection_bank.png) | 293×149 | 945,240 | 5,8 |
| Área Residencial | [selection_condominium.png](../../Assets/Art/new/location/overlays/selection_condominium.png) | 619×364 | 305,645 | 15,8 |
| Área Corporativa | [selection_marketing.png](../../Assets/Art/new/location/overlays/selection_marketing.png) | 507×322 | 1330,673 | 16,5 |
| Área Comercial | [selection_store.png](../../Assets/Art/new/location/overlays/selection_store.png) | 472×248 | 1368,247 | 11,8 |
| Área Educacional | [selection_university.png](../../Assets/Art/new/location/overlays/selection_university.png) | 529×265 | 123,125 | 9,3 |
| Centro / restaurante | [selection_center.png](../../Assets/Art/new/location/overlays/selection_center.png) | 412×219 | 961,494 | 11,2 |

As posições são o **canto superior esquerdo** de cada PNG no master 1920×1080, com origem no alto à esquerda e Y crescente para baixo. A margem transparente de 2 px está incluída. Usar pivot `[0,1]`, escala `1`, bordas zero e a transformação uniforme compartilhada do mapa. Os polígonos não foram redimensionados.

Os cinco realces de localidade são mutuamente exclusivos na ordem 10; o centro na ordem 11 pode ser ativado independentemente. Eles ficam entre o terreno e as construções. Não há sexto ID de Location.

| Componente | PNG | Export / display nominal | Bordas l,b,r,t |
| --- | --- | --- | --- |
| UI_PIN_NORMAL | [pin_normal.png](../../Assets/Art/new/location/markers/pin_normal.png) | 256×256 / 80×80 | [0, 0, 0, 0] |
| UI_MAP_LABEL_NORMAL | [map_label_normal.png](../../Assets/Art/new/location/markers/map_label_normal.png) | 256×128 / 260×76 | [24, 24, 24, 24] |
| UI_PIN_SELECTED | [pin_selected.png](../../Assets/Art/new/location/markers/pin_selected.png) | 256×256 / 80×80 | [0, 0, 0, 0] |
| UI_MAP_LABEL_SELECTED | [map_label_selected.png](../../Assets/Art/new/location/markers/map_label_selected.png) | 256×128 / 260×76 | [24, 24, 24, 24] |
| UI_PIN_DISABLED | [pin_disabled.png](../../Assets/Art/new/location/markers/pin_disabled.png) | 256×256 / 80×80 | [0, 0, 0, 0] |
| UI_MAP_LABEL_DISABLED | [map_label_disabled.png](../../Assets/Art/new/location/markers/map_label_disabled.png) | 256×128 / 260×76 | [24, 24, 24, 24] |
| UI_MAP_LABEL_TIP | [map_label_tip.png](../../Assets/Art/new/location/markers/map_label_tip.png) | 64×64 / 20×20 | [0, 0, 0, 0] |

Pinos, rótulos e ponteira usam pivot `[0.5,0.5]`. Aplicar `contain` com escala uniforme aos pinos e à ponteira; aplicar `Sliced` aos rótulos. Estados normal/selecionado/desabilitado têm **máscaras alfa idênticas** por família, sem deslocamento ou mudança de bounds.

O centro dos pinos fica vazio para receber ícones ART-06 em camada independente. O slot nativo é `[80,58,96,96]`. Conteúdo dos rótulos usa texto nativo; área segura no display nominal 260×76: `[24,24,212,28]`. Os PNGs finais não têm ícones ou nomes incorporados.

A ponteira é branca tintável: aplicar a mesma cor do preenchimento do rótulo (marfim, violeta ou neutro) e desenhar sob a base, com sobreposição na junção. Reutilizar esse único sprite nos três estados.

[Contrato de seleção, componentes e encaixe](layouts/map-selection-compact.json) · [Contrato completo do mapa](layouts/map-layout.json)

No parent master, usar `anchoredPosition = [x,-y]` para os realces com âncoras no alto à esquerda e `sizeDelta` igual ao PNG. O parent mantém escala comum de `2/3` e offset absoluto de canvas `[24,176]`. A viewport 1280×848 contém o mapa 1280×720 com margem vertical local de 64 px.

A prova de montagem dos marcadores usa pinos 80×80 e rótulos 260×76 **em pixels do master**; na viewport eles ocupam 53,33×53,33 e 173,33×50,67. A revisão isolada também mostra os tamanhos nominais de UI 80×80 e 260×76. A proposta de instâncias é documentada no JSON, preservando as âncoras canônicas. Esses valores servem à futura integração.

Os treze PNGs somam **106,2 KB**. Os seis realces usam 779696 pixels, **93,7% menos** que seis canvases 1920×1080. A capacidade total RGBA8 sem compressão/mipmaps é cerca de **4,11 MiB**. Build e memória real dependem de import, plataforma e atlas.

[Componentes sobre fundos claro/escuro](../../art-source/previews/ART-13/ART-13-components-display.png) · [Geometria nativa dos marcadores](../../art-source/previews/ART-13/ART-13-markers-native.png) · [Realces sobre fundo claro](../../art-source/previews/ART-13/ART-13-overlays-native-light.png) · [Realces sobre fundo escuro](../../art-source/previews/ART-13/ART-13-overlays-native-dark.png)

[Prova das seis seleções](../../art-source/previews/ART-13/ART-13-map-selections.png) · [Exibição da área financeira](../../art-source/previews/ART-13/OVERLAY_BANK-map-display-light.png) · [Realce central sobre fundo escuro](../../art-source/previews/ART-13/OVERLAY_CENTER-map-display-dark.png)

Produção vetorial determinística, conforme a skill para componentes geométricos. Fontes em `art-source/editable/location/markers` e `art-source/editable/location/overlays`; PNGs rasterizados diretamente na resolução registrada com sharp/libvips e perfil sRGB. Não foi necessário gerar novas ilustrações; modelo registrado como `null`.

[Prompts/specs exatos, fontes e SHA256](prompts/ART-13.json) · [Gerador dos SVGs e PNGs](../../art-source/editable/ART-13/build-art13.cjs) · [QA](qa/ART-13.json) · [Medições](qa/ART-13-measurements.json) · [Auditoria](qa/ART-13-scope-audit.json)

Validação técnica e do plano aprovadas; fontes SVG e hashes conferidos. Geometria alfa dos estados idêntica, cantos do 9-slice preservados, sem emendas no centro. SVGs dos realces usam os pontos exatos do contrato e não têm efeitos, textos ou imagens incorporados. A cobertura antialias fica limitada à borda geométrica subpixel do polígono.

Pendências de produção: nenhuma. Integração às cenas fica para etapa própria; geografia, IDs, economia, scripts e outros lotes foram preservados.

Fontes SVG separadas:

- [OVERLAY_BANK](../../art-source/editable/location/overlays/selection_bank.svg)
- [OVERLAY_CONDOMINIUM](../../art-source/editable/location/overlays/selection_condominium.svg)
- [OVERLAY_MARKETING](../../art-source/editable/location/overlays/selection_marketing.svg)
- [OVERLAY_STORE](../../art-source/editable/location/overlays/selection_store.svg)
- [OVERLAY_UNIVERSITY](../../art-source/editable/location/overlays/selection_university.svg)
- [UI_PIN_NORMAL](../../art-source/editable/location/markers/pin_normal.svg)
- [UI_MAP_LABEL_NORMAL](../../art-source/editable/location/markers/map_label_normal.svg)
- [UI_PIN_SELECTED](../../art-source/editable/location/markers/pin_selected.svg)
- [UI_MAP_LABEL_SELECTED](../../art-source/editable/location/markers/map_label_selected.svg)
- [UI_PIN_DISABLED](../../art-source/editable/location/markers/pin_disabled.svg)
- [UI_MAP_LABEL_DISABLED](../../art-source/editable/location/markers/map_label_disabled.svg)
- [UI_MAP_LABEL_TIP](../../art-source/editable/location/markers/map_label_tip.svg)
- [OVERLAY_CENTER](../../art-source/editable/location/overlays/selection_center.svg)
