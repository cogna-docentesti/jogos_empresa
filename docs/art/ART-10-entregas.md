# ART-10 — Geografia e contrato de coordenadas do mapa

O lote entrega **um asset novo**, `MAP_BASE`, e o contrato de montagem das cinco localidades e do restaurante central. Controles, campos e seleção de formulários pertencem ao ART-05, cujos 24 PNGs existentes foram conferidos novamente e passaram no validador. Não existem controles adicionais no inventário do ART-10.

- [PNG final: map_base.png](../../Assets/Art/new/location/map/map_base.png)
- [Contrato: map-layout.json](layouts/map-layout.json)
- [Prévia geral em fundos claros/escuros e polígonos de QA](../../art-source/previews/ART-10/ART-10-contact-sheet.png)
- [Inspeção de bordas em 1:1](../../art-source/previews/ART-10/MAP_BASE-edges-1x.png)
- [Prompts exatos, origem, referências e hashes](prompts/ART-10.json)
- [QA](qa/ART-10.json) e [validação técnica](qa/ART-10-technical.json)

O PNG tem **1920×1080, RGBA8, sRGB**, pivot `[0,1]` e bordas 9-slice zero. Contém somente terreno, vias, calçadas, rio, duas pontes e vegetação baixa. As seis implantações estão vazias; labels e polígonos aparecem somente nas provas de QA.

Arte produzida com a ferramenta integrada `image_gen.imagegen`, usando o concept aprovado do hub e uma revisão de vegetação. O modelo não é exposto pela ferramenta. O arquivo nativo devolvido tem 1672×941; o export técnico utiliza escala uniforme `contain` para o master 1920×1080, com padding transparente de arredondamento, sem crop ou deformação. A arte e o alfa originais são preservados. Há alfa zero externo; nos seis lotes o alfa varia de 250 a 254, cerca de 98 a 99,6% de opacidade, sem fixação artificial em 255.

| ID preservado | Localidade | Âncora de chão no master | Âncora do label no master |
| --- | --- | --- | --- |
| university | Área Educacional | 390,315 | 430,88 |
| bank | Área Financeira | 1100,341 | 1100,172 |
| store | Área Comercial | 1638,409 | 1600,194 |
| condominium | Área Residencial | 640,866 | 637,602 |
| marketing | Área Corporativa | 1595,899 | 1600,615 |
| implantação central | Restaurante, sem novo ID de localidade | 1172,657 | 1165,417 |

O contrato define polígonos de seleção e hit-areas sem sobreposição, envelopes de construção, âncora de porta central, câmera/luz e ordem das camadas. As camadas futuras aparecem somente como plano de encaixe, com origem `[0,0]`, escala `1` e pivot `[0,1]` no master.

Na caixa de exibição 1280×848, a transformação `contain` uniforme de `2/3` resulta em mapa 1280×720, com margem local vertical de 64 px. A caixa começa em `(24,112)` no canvas; a arte começa em `(24,176)`. Aplicar a mesma transformação aos sprites, polígonos e âncoras.

Os RectTransforms atuais foram registrados em [ART-10-scene-snapshot.json](qa/ART-10-scene-snapshot.json), com hashes das cenas. O contrato explica como substituir as coordenadas na integração futura; nenhuma alteração foi aplicada às cenas. A auditoria preserva os registros dos outros lotes, arte validada, fontes anteriores, catálogos, scripts e prefabs.

Validação: helper do projeto sem erros, schema Draft 2020-12 válido, cinco IDs e seis polígonos conferidos, zero pixels de sobreposição. Revisão visual do PNG em fundos claros/escuros, no master, em detalhes 1:1 e na caixa de exibição.

Pendências do ART-10: nenhuma. Construções do ART-11, variantes de restaurante/foreground do ART-12 e realces/marcadores do ART-13 seguem planejados.
