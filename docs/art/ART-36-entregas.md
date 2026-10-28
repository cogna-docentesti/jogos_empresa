# ART-36 — auditoria final e mapa de integração

Auditoria concluída em 15/10/2026 sobre `5d18996a6ff6cebc5e6dab0c04eaef4d5e93e107`: 265 PNGs existentes dos 35 lotes de produção, seis identidades canônicas e 87 provas novas de 1920×1080 inspecionadas diretamente. Nenhum sprite foi criado, regenerado ou alterado. As sete peças futuras de ART-37/38/39 conservam seus estados; ART-40 não define sprites.

O status **validado** da ART-36 significa auditoria e provas concluídas. **A integração no jogo permanece pendente.** Não houve execução do Unity, mudança de importadores, cenas, prefabs, scripts, catálogos ou economia.

## Entregas e provas

| Entrega | Evidência |
| --- | --- |
| Galeria das 87 provas separadas | [Abrir índice local](../../art-source/previews/ART-36/index.html) |
| Mapa: francês / banco | [Claro](../../art-source/previews/ART-36/ART-36-map-map_rest_frances-light.png), [escuro](../../art-source/previews/ART-36/ART-36-map-map_rest_frances-dark.png) |
| Mapa: japonês / universidade | [Claro](../../art-source/previews/ART-36/ART-36-map-map_rest_japones-light.png), [escuro](../../art-source/previews/ART-36/ART-36-map-map_rest_japones-dark.png) |
| Mapa: lanches / comércio | [Claro](../../art-source/previews/ART-36/ART-36-map-map_rest_lanches-light.png), [escuro](../../art-source/previews/ART-36/ART-36-map-map_rest_lanches-dark.png) |
| Mapa nativo 1:1 | [Residencial](../../art-source/previews/ART-36/ART-36-map-master-overlay_condominium.png), [empresas](../../art-source/previews/ART-36/ART-36-map-master-overlay_marketing.png), [centro](../../art-source/previews/ART-36/ART-36-map-master-overlay_center.png) |
| DRE em camadas | [Mensal](../../art-source/previews/ART-36/ART-36-dre-monthly.png), [trimestral](../../art-source/previews/ART-36/ART-36-dre-quarterly.png) |
| Rastreabilidade por asset | [Inventário: tamanho, alfa, bounds, SHA256, origem, prompts e referências](qa/ART-36-inventory.json) |
| Destinos por asset | [Mapa de integração: ID, PNG, catálogo alvo, consumidores e importação](qa/ART-36-integration-map.json) |
| Contratos e estados | [Schemas, 13 famílias de estados e medições das âncoras](qa/ART-36-contracts.json) |
| Registro de revisão | [QA completo](qa/ART-36.json), [índice com SHA256 e cobertura dos 265 assets](qa/ART-36-preview-index.json) |

As 49 páginas `ART-36-display-XX.png` mostram cada recorte/componente nos dois fundos, na caixa de exibição do manifesto e em miniatura de 50%. Dez provas independentes mostram barras, scrim, cenário de identificação e base do mapa. Há nove montagens de mapa, duas de DRE e 17 páginas com cortes de rosto/mãos em pixels nativos. Os componentes geométricos usam suas bordas 9-slice; recortes usam escala uniforme. Textos e fundos de QA pertencem às provas.

As montagens anteriores de identificação, banco, equipe e os quatro estados de estabelecimento/revisão do ART-27 também foram abertas e reinspecionadas, sem reexportação nem mudança. Os cinco concepts aprovados foram consultados como referência de linguagem; continuam separados dos sprites finais.

## Resultado técnico

Os 265 arquivos passaram na revalidação explícita ART-01 a ART-35: formato PNG RGBA, dimensões reais, alfa conforme política, referências e fontes editáveis exigidas. Seus hashes coincidem com o manifesto; os seis masters coincidem com `identity_hash`. Não houve falta de arquivo ou divergência de SHA256. Os contratos atuais de mapa e DRE passaram em todas as regras presentes nos schemas locais; as âncoras medidas coincidem com as declaradas.

As 13 famílias de estados têm dimensões, pivot e bordas estáveis. Doze têm canais alfa idênticos; `UI_INPUT` mantém bounds iguais e muda o alfa do contorno de foco/erro, sem mudança de layout. A inspeção visual confirmou a ausência de salto desenhado; interação real ainda precisa ser testada na integração.

Os comandos obrigatórios `validate_assets.py --project D:/Cogna/jogos_empresa --task ART-36` e `--plan` passaram. O primeiro tem **zero sprites próprios**; a prova dos 265 está nos [relatórios dos pré-requisitos](qa/ART-36-prerequisites.json), e não nesse resultado vazio.

## Mapa de integração

| Família | Destino proposto e contrato |
| --- | --- |
| CH01–CH06 | Masters de referência; manter aparência nas poses e retratos. Não substituir por recortes dos concepts. |
| ART-03–09 e ART-35 | Kit compartilhado de HUD, botões, controles, ícones e tutorial. Reusar bases/sinais separados e texto nativo. |
| ART-10–13 | `Assets/Scenes/LocationSelectionScene.unity`; autoridade: [map-layout.json](layouts/map-layout.json). |
| ART-14 | `Assets/Scenes/0_Identification.unity`; proposta: [identification-layout.json](layouts/identification-layout.json). |
| ART-15–16 | Fachadas e detalhes de restaurante/localidade, com PNGs e IDs individuais. |
| ART-17–24 | Campos de ícone de Products e Equipments; o mapa JSON resolve cada `catalog_id` para seu arquivo real em `Assets/Resources`. |
| ART-25–26 | Financeiro/créditos e equipe/RoleData; pessoas e ambientes são camadas independentes. |
| ART-27 | Proposta de estabelecimento em [establishment-review-layout.json](layouts/establishment-review-layout.json). |
| ART-28–29 | DRE de `GameScene`; autoridade: [dre-layout.json](layouts/dre-layout.json), com viewport e âncoras mensais/trimestrais. |
| ART-30–34 | Eventos, conclusão, dificuldade e vazios; títulos, efeitos, opções e dados continuam dinâmicos. |

No mapa, todos os PNGs compactos entram no parent master 1920×1080 em `position_px`, escala 1 e pivot top-left `[0,1]`. Aplicar ao parent **uma única** escala `2/3`, offset `[24,176]`: desenho 1280×720 contido no viewport `[24,112,1280,848]`. Transformar polígonos, hit areas e âncoras com a mesma regra. Mostrar uma variante de restaurante e uma zona selecionada; o centro é independente e não cria um sexto ID. Os hashes dos contratos compactos antigos registram suas revisões de produção; a autoridade atual é `map-layout.json`.

Na DRE, `position_px` é o pivot em coordenadas de canvas: top-left = posição − tamanho nativo × escala × pivot. Preservar a ordem papel → pessoa(s) → mesa → props e o clip comum `[1320,120,560,420]`. A folha permanece atrás dos dedos de CH01. Não reutilizar a semântica top-left do mapa para a DRE.

## Pendências reais para integração posterior

- **227 importadores** diferem da proposta de QA: 207 em Multiple, 67 com bordas 9-slice diferentes, 16 com pivot diferente e 227 com compressão padrão. As categorias se sobrepõem. [Relatório por PNG/.meta e GUID](qa/ART-36-import-audit.json). Revisar consumidores/fileIDs antes de converter para Single; não reescrever metadados em massa.
- A busca estática em **175 arquivos serializados** não encontrou referência a nenhum dos 265 GUIDs novos. Os IDs dos catálogos foram comparados com os arquivos atuais e coincidem. A busca não prova ausência de carregamento dinâmico; não houve teste de runtime.
- A identificação usa CanvasScaler 1280×720; `GameScene` contém o principal 1920×1080 e quatro painéis em 800×600. A integração precisa resolver essa combinação e testar layout/input nas plataformas.
- O logo oficial e objetos escuros precisam de superfície clara. Masters brancos precisam de tint conforme o fundo. Overlays de seleção/desabilitado e sombras dependem de uma base; não são painéis completos.
- A escala móvel é uma prova óptica a 50%. Alvos de toque, reflow, orientação, acessibilidade e desempenho dependem de validação no jogo.

Não há pendência de arquivo, montagem ou inspeção dentro da ART-36. As condições acima ficam registradas sem declarar o jogo integrado.

## Reprodução

Fontes de auditoria e montagem: [audit_art36.py](../../art-source/editable/ART-36/audit_art36.py) e [preview_art36.py](../../art-source/editable/ART-36/preview_art36.py). Usam Python/Pillow/PyYAML e não invocam geração de imagem. O [registro de revisão](../../art-source/editable/ART-36/record_review_art36.py) enumera as 87 páginas efetivamente abertas nesta conversa; execução do script, sozinha, não substitui inspeção visual futura.
