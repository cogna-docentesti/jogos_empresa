# Produção de arte Cogna

Abra [PLANO-DE-ARTE.html](PLANO-DE-ARTE.html) para consultar o inventário e os comandos dos 40 lotes. Solicite um lote por vez ao agente `$cogna-artista`.

| Pasta na raiz do projeto | Conteúdo |
| --- | --- |
| `Assets/Art/new/` | PNGs finais que o Unity importa, separados por interface e função |
| `docs/art/` | Plano, manifesto, tarefas, especificações, schemas, contratos de montagem, prompts e registros de QA |
| `art-source/references/` | Concepts e referências aprovadas |
| `art-source/editable/` | Fontes SVG/código e arquivos de trabalho editáveis |
| `art-source/previews/` | Provas de montagem e revisão visual |
| `tools/art/` | Validador e ferramentas de produção |

O inventário contém 272 assets. ART-01 e ART-02 entregaram seis masters canônicos CH01–CH06, validados em 1024×1536 com alfa real. ART-03 entregou 14 PNGs de barras, painéis e bases compartilhadas, com 13 fontes SVG editáveis e o logo oficial convertido para RGBA preservando todos os pixels. ART-04 entregou 20 PNGs e 20 fontes SVG de botões sem texto: cinco famílias com estados normal, hover, pressionado e desabilitado, silhueta alfa idêntica por família e bordas 9-slice preservadas. ART-05 entregou 24 PNGs e 24 fontes SVG de campos, abas e bases de seleção, slider, stepper e scrollbar, com peças separadas, transparência real e bounds/pivot preservados entre estados. ART-06 entregou 18 ícones brancos tintáveis de navegação e HUD em PNG 256×256 e SVG editável, conferidos em 32/40/48 px, sem fundos, badges ou variantes por cor. ART-07 entregou outros 18 ícones financeiros e indicadores no mesmo padrão, com silhuetas próprias e sem duplicar os masters do ART-06. ART-08 entregou 33 ícones de ações, status e categorias, em PNG 256×256 com alfa real e SVG editável, também revisados em 32/40/48 px e sem repetir os símbolos anteriores. ART-09 entregou 18 indicadores do HUD e componentes da DRE em PNG RGBA e SVG editável, com estados, trilho e preenchimento separados, sem texto ou dados incorporados e com bordas 9-slice conferidas nos tamanhos de exibição. São 151 assets validados; os outros 121 continuam planejados.

Consulte as revisões [ART-01](qa/ART-01.json), [ART-02](qa/ART-02.json), [ART-03](qa/ART-03.json), [ART-04](qa/ART-04.json), [ART-05](qa/ART-05.json), [ART-06](qa/ART-06.json), [ART-07](qa/ART-07.json), [ART-08](qa/ART-08.json) e [ART-09](qa/ART-09.json), com os registros de produção em [ART-01](prompts/ART-01.json), [ART-02](prompts/ART-02.json), [ART-03](prompts/ART-03.json), [ART-04](prompts/ART-04.json), [ART-05](prompts/ART-05.json), [ART-06](prompts/ART-06.json), [ART-07](prompts/ART-07.json), [ART-08](prompts/ART-08.json) e [ART-09](prompts/ART-09.json). As prévias do [ART-03](../../art-source/previews/ART-03/ART-03-contact-sheet.png), [ART-04](../../art-source/previews/ART-04/ART-04-contact-sheet.png), [ART-05](../../art-source/previews/ART-05/ART-05-contact-sheet.png), [ART-06](../../art-source/previews/ART-06/ART-06-contact-sheet.png), [ART-07](../../art-source/previews/ART-07/ART-07-contact-sheet.png), [ART-08](../../art-source/previews/ART-08/ART-08-contact-sheet.png) e [ART-09](../../art-source/previews/ART-09/ART-09-contact-sheet.png) reúnem os componentes; labels e fundos de QA não integram os sprites. A [montagem de controles](../../art-source/previews/ART-05/controls-assembly-1x.png) do ART-05 demonstra slider, toggle e scrollbar com peças independentes. Marcadores e sinais do ART-08 permanecem separados das bases do ART-05. Nos previews dos ícones ART-06/07/08, o tint ameixa serve apenas à leitura sobre fundo claro; os arquivos finais são masters brancos transparentes.

## Caminhos do manifesto v2

As raízes declaradas no manifesto são relativas à raiz do repositório. `assets[].path` e `characters[].canonical_asset` são relativos a `asset_root` (`Assets/Art/new`). Referências, `source_editable` e caminhos dos contratos/schemas são relativos à raiz do repositório. Não salvar SVGs, prompts, previews ou documentos em `Assets/Art/new`.

O [manifest.json](manifest.json) registra o status real de cada entrega. Os arquivos em `tasks/` detalham cada lote; `tokens.json`, `characters.json`, `sources.json` e `catalogos.json` completam a especificação.

## Validação

Na raiz do projeto, com Python e Pillow disponíveis:

```powershell
python tools/art/validate_assets.py --plan
python tools/art/validate_assets.py --task ART-01
```

O primeiro comando verifica o planejamento. O segundo exige arquivos reais do lote: itens planejados ainda ausentes falham, como esperado. A revisão visual continua obrigatória.
