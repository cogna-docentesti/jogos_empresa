# ART-11 — Construções e bairros do mapa

Cinco PNGs novos, separados e alinhados ao master do ART-10. O lote corresponde às construções/bairros do mapa; controles, campos e seleção de formulários pertencem ao ART-05.

| Asset | Conteúdo | PNG final |
| --- | --- | --- |
| MAP_BANK | Banco marfim com fachada azul e acesso frontal | [map_bank.png](../../Assets/Art/new/location/landmarks/map_bank.png) |
| MAP_CONDOMINIUM | Bairro residencial com seis casas marfim/terracota | [map_condominium.png](../../Assets/Art/new/location/landmarks/map_condominium.png) |
| MAP_MARKETING | Conjunto de três volumes corporativos marfim/azul | [map_marketing.png](../../Assets/Art/new/location/landmarks/map_marketing.png) |
| MAP_STORE | Comércio de dois pavimentos, marfim/coral e vitrines azuis | [map_store.png](../../Assets/Art/new/location/landmarks/map_store.png) |
| MAP_UNIVERSITY | Universidade com cúpula índigo, pórtico e duas alas terracota | [map_university.png](../../Assets/Art/new/location/landmarks/map_university.png) |

Todos são **PNG RGBA8 sRGB, 1920×1080**, com transparência real fora da construção/bairro, pivot `[0,1]`, posição de camada `[0,0]`, escala de camada `1` e bordas 9-slice zero. O conteúdo está posicionado no full-frame, portanto a integração deve usar o retângulo completo e preservar as margens transparentes; não centralizar cada silhueta automaticamente.

[Prévia completa](../../art-source/previews/ART-11/ART-11-contact-sheet.png) · [Montagem no master](../../art-source/previews/ART-11/ART-11-assembly-master-light.png) · [Montagem sobre fundo escuro no tamanho de exibição](../../art-source/previews/ART-11/ART-11-assembly-display-dark.png) · [Prova dos footprints](../../art-source/previews/ART-11/ART-11-footprint-assembly.png)

[Prompts exatos, referências, ferramenta, origem e SHA256](prompts/ART-11.json) · [QA visual](qa/ART-11.json) · [Validação técnica](qa/ART-11-technical.json) · [Medições de encaixe/alfa](qa/ART-11-measurements.json) · [Auditoria de escopo](qa/ART-11-scope-audit.json)

Cada fonte foi gerada individualmente com `image_gen.imagegen`, pela ferramenta integrada, usando o hub aprovado para arquitetura/acabamento e MAP_BASE para câmera/luz. O identificador do modelo não é exposto. As fontes nativas devolvidas são 1254×1254; o export técnico aplica somente escala uniforme e posicionamento para o master 1920×1080. Não houve pintura, warp, esticamento independente, remoção de fundo ou alteração de alfa por código. A distribuição das seis casas foi revisada pela própria ferramenta de imagem antes do encaixe final.

As seis fontes raster de geração, incluindo a primeira tentativa residencial, estão preservadas em `art-source/references/generations/ART-11`; são fontes de produção, não sprites finais. O [script de export](../../art-source/editable/ART-11/export-landmark.cjs) e o [registro de posicionamento](../../art-source/editable/ART-11/placements.json) permitem reproduzir os full-frames. Fontes raster generativas não têm um SVG editável fictício.

O [contrato do ART-10](layouts/map-layout.json) permanece intacto. Sua câmera, orientação, luz, zonas, envelopes e âncoras foram usados para encaixe. Os três edifícios corporativos e as seis casas são conjuntos coerentes nas respectivas camadas; plantas/escadas diretamente integradas às construções fazem parte das bases físicas. Não há terreno, rio, ruas, HUD, pinos, nomes ou realces incorporados nesses PNGs.

Na caixa 1280×848, a transformação comum de `2/3` produz o mapa 1280×720, com offset local `[0,64]`. Todas as camadas recebem a mesma transformação. O retângulo reservado ao restaurante central permanece livre, e vias, pontes e caminhos residenciais foram conferidos na prova de montagem.

O alfa original de ImageGen foi preservado. O recorte contém alfa zero real; pixels residuais de opacidade muito baixa da fonte ficam registrados no `alpha_bbox`. A silhueta visível usa alfa ≥128 para medir o envelope, com revisão visual complementar em fundos marfim e ameixa. Não declarar o `alpha_bbox` residual como dimensão da construção visível.

Validação concluída: cinco PNGs sem erros no helper; dimensões/pivot/origem compatíveis com o master; bases físicas nos gramados da zona correta; zero pixels de sobreposição entre silhuetas visíveis; restaurante central sem pixels de alfa ≥16. Cada construção foi inspecionada em detalhes 1:1 sobre fundos claros/escuros e na montagem ao tamanho de exibição.

Pendências do ART-11: nenhuma. Restaurante/foreground ART-12 e realces/marcadores ART-13 seguem planejados. Nenhum sprite foi aplicado às cenas e nenhum catálogo, script ou prefab foi alterado.
