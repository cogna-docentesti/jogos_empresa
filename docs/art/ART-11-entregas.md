# ART-11 — Construções compactas

Os cinco assets principais agora são recortes das mesmas construções validadas, com transparência e posição exata de encaixe. O canvas vazio foi removido; a arte conserva sua resolução e seus pixels RGBA. Os originais 1920×1080 ficam preservados como variantes de comparação.

| Construção | PNG principal | Export (px) | X,Y no master | Arquivo (KB decimais) |
| --- | --- | --- | --- | --- |
| Banco | [map_bank.png](../../Assets/Art/new/location/landmarks/compact/map_bank.png) | 250×235 | 986,151 | 83,2 |
| Bairro residencial | [map_condominium.png](../../Assets/Art/new/location/landmarks/compact/map_condominium.png) | 391×313 | 393,631 | 100,7 |
| Área corporativa | [map_marketing.png](../../Assets/Art/new/location/landmarks/compact/map_marketing.png) | 364×364 | 1372,575 | 157,1 |
| Comércio | [map_store.png](../../Assets/Art/new/location/landmarks/compact/map_store.png) | 317×274 | 1475,175 | 129,0 |
| Universidade | [map_university.png](../../Assets/Art/new/location/landmarks/compact/map_university.png) | 309×277 | 218,51 | 124,6 |

As posições são o **canto superior esquerdo do PNG**, em pixels do master 1920×1080, com origem no alto à esquerda e Y crescendo para baixo. Usar escala `1` e pivot `[0,1]`. O tamanho inclui uma margem transparente de 2 pixels. Bairros/conjuntos mantêm suas construções agrupadas.

[Contrato completo do mapa](layouts/map-layout.json) · [Posições, dimensões, pivots e âncoras locais dos recortes](layouts/map-landmarks-compact.json)

Para um RectTransform no parent master 1920×1080, usar âncoras no alto à esquerda, pivot `[0,1]`, `anchoredPosition = [x,-y]` e `sizeDelta` igual ao tamanho do PNG. As posições para pivot central e para a viewport 1280×848 também estão no JSON. Esses valores são propostas de integração; nenhuma cena ou import existente foi alterado.

Aplicar ao parent do mapa a transformação compartilhada de `2/3`, conforme o contrato. A caixa 1280×848 contém o mapa 1280×720, com offset vertical local de 64 px. Cada textura compacta ocupa só sua área; a posição fornecida recompõe o layout.

[Prévia dos recortes](../../art-source/previews/ART-11/compact/ART-11-compact-contact-sheet.png) · [Montagem no master](../../art-source/previews/ART-11/compact/compact-assembly-light.png) · [Exibição sobre fundo escuro](../../art-source/previews/ART-11/compact/compact-display-dark.png)

Os PNGs somam **594,6 KB**, ante 817,9 KB. A capacidade de pixels RGBA8 sem compressão e sem mipmaps passa de **39,55 MiB para 1,85 MiB**, redução de **95,3%**. A memória real e o tamanho do aplicativo dependem dos ajustes de import, compressão, plataforma e atlas; estes números medem os arquivos e a área das texturas.

Produção: recorte técnico lossless com `sharp`, incluindo todos os pixels de alfa não zero e a margem transparente. A ilustração original foi produzida com ImageGen integrado. O [registro desta exportação](prompts/ART-11-compact.json) conserva os caminhos, SHA256, medidas e origem; os [prompts de arte originais](prompts/ART-11.json) continuam disponíveis.

[Script de recorte](../../art-source/editable/ART-11/crop-compact.cjs) · [QA](qa/ART-11-compact.json) · [Prova pixel a pixel](qa/ART-11-compact-measurements.json) · [Auditoria](qa/ART-11-compact-scope-audit.json)

Validação: cada recorte, colocado nas coordenadas registradas, reconstrói o full-frame com **diferença zero em todos os bytes RGBA**. A montagem conjunta também é idêntica. Recortes revisados sobre marfim/ameixa e no tamanho de exibição. Geometria, câmera, zonas, labels e hit-areas do mapa continuam com o mesmo encaixe.

[Entrega full-frame preservada](ART-11-fullframe-entregas.md) · [Contrato anterior preservado](layouts/archive/map-layout-art11-fullframe.json)

Pendências desta exportação: nenhuma. A aplicação dos novos PNGs às cenas fica para a integração.
