# ART-12 — Restaurantes centrais e camada frontal

Quatro PNGs compactos com transparência, produzidos seguindo os concepts aprovados. Os três restaurantes usam a mesma construção, câmera, escala e entrada; a decoração e o interior diferenciam lanches, francês e japonês. A vegetação com banco é uma camada separada de primeiro plano.

| Asset | PNG em Assets/Art/new | Export (px) | X,Y no master | KB (decimais) |
| --- | --- | --- | --- | --- |
| Lanches | [map_rest_lanches.png](../../Assets/Art/new/location/restaurants/map_rest_lanches.png) | 320×259 | 1036,418 | 111,5 |
| Francês | [map_rest_frances.png](../../Assets/Art/new/location/restaurants/map_rest_frances.png) | 316×247 | 1040,418 | 114,5 |
| Japonês | [map_rest_japones.png](../../Assets/Art/new/location/restaurants/map_rest_japones.png) | 314×271 | 1042,418 | 113,7 |
| Vegetação e banco | [map_foreground.png](../../Assets/Art/new/location/map/map_foreground.png) | 135×95 | 1088,588 | 15,6 |

As posições são o **canto superior esquerdo do PNG**, em pixels do master 1920×1080, com origem no alto à esquerda e Y crescente para baixo. Todos usam pivot `[0,1]`, escala `1`, bordas zero e margem transparente de 2 px. A posição inclui a margem.

Mostrar somente um restaurante por vez na ordem de camada 70. A vegetação usa ordem 90. Porta comum `[1280,642]`; referência comum de chão `[1172,657]`. A âncora de chão é uma referência de montagem do master, não o canto do sprite. Como os recortes têm dimensões distintas, **cada variante usa sua própria posição top-left**, registrada na tabela e no JSON.

[Contrato de encaixe dos quatro recortes](layouts/map-restaurants-compact.json) · [Contrato completo do mapa](layouts/map-layout.json)

No parent master, usar RectTransform com âncoras no alto à esquerda, pivot `[0,1]`, `anchoredPosition = [x,-y]` e `sizeDelta` igual ao PNG. O JSON também registra as posições para pivot central e para a viewport. Aplicar ao parent a transformação uniforme comum de `2/3` com offset absoluto de canvas `[24,176]`. A viewport 1280×848 contém o mapa 1280×720 com 64 px de margem vertical local.

Os quatro PNGs somam **355,3 KB**. Os restaurantes têm cerca de 112–115 KB cada; o foreground tem cerca de 16 KB. A área total das texturas é **258851 pixels**, 96,9% menor que quatro canvases 1920×1080. A capacidade RGBA8 sem compressão/mipmaps é cerca de **0,99 MiB**, ante 31,64 MiB dos quatro canvases. O tamanho do aplicativo e a memória real dependem do import, compressão, plataforma e atlas.

As fontes maiores ficam em `art-source/references/generations/ART-12`, fora dos assets importáveis. Somente os quatro recortes compactos deste lote foram adicionados em `Assets/Art/new`. Import sugerido para integração: Sprite Single, sRGB, alfa como transparência, sem mipmaps, PPU 100, filtro bilinear e bordas zero; max texture size >= dimensão real (512 para os restaurantes, 256 para o foreground). Nenhum import ou cena foi aplicado nesta entrega.

[Prévia sobre fundo claro](../../art-source/previews/ART-12/ART-12-contact-light.png) · [Prévia sobre fundo escuro](../../art-source/previews/ART-12/ART-12-contact-dark.png) · [Montagem de lanches](../../art-source/previews/ART-12/MAP_REST_LANCHES-display-light.png) · [Montagem francesa](../../art-source/previews/ART-12/MAP_REST_FRANCES-display-dark.png) · [Montagem japonesa](../../art-source/previews/ART-12/MAP_REST_JAPONES-display-light.png)

Ilustrações geradas por ImageGen integrado, um asset por chamada; francês e japonês são edições da construção de lanches. O modelo não foi exposto pela ferramenta e está registrado como `null`. O export técnico usa sharp com uma única redução uniforme, preservação do alfa original e recorte lossless. Não houve pintura, deformação, máscaras ou limpeza de alfa por código.

[Prompts exatos, fontes, medidas e SHA256](prompts/ART-12.json) · [Exportador técnico](../../art-source/editable/ART-12/export-compact.cjs) · [QA](qa/ART-12.json) · [Medições](qa/ART-12-measurements.json) · [Auditoria de escopo](qa/ART-12-scope-audit.json)

Validação técnica dos quatro PNGs e do plano aprovada. Reconstrução do alfa e dos canais de todos os pixels com alfa não zero sem diferença. Implantação central, envelope visível, porta livre, alinhamento das variantes e montagem conjunta conferidos. Aproximadamente 1054–1136 pixels visíveis da base são ocluídos pelo foreground, conforme a variante.

Os recortes incluem resíduos de alfa muito baixo herdados da geração, sem limpeza destrutiva; por isso o retângulo total pode variar e ultrapassar levemente o envelope que limita a silhueta visível. Esses pixels também foram preservados e contabilizados. O arquivo japonês contém uma margem inferior pouco visível maior que a francesa, sem alterar a implantação.

Pendências de produção: nenhuma. A aplicação às cenas fica para a etapa de integração; geografia, câmera, hit-areas, IDs e outros lotes foram preservados.
