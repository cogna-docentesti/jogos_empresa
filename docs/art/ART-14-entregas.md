# ART-14 — Arte da identificação

Quatro PNGs separados, no estilo dos concepts aprovados: cenário de fachada/bairro, dois ornamentos roxos discretos e a CH01 em pé com prancheta e gesto natural de acolhimento. Título, Nome, RA, erro e Cadastrar permanecem conteúdo nativo da interface.

| Asset | PNG em Assets/Art/new | Export (px) | Arquivo | Alfa |
| --- | --- | --- | --- | --- |
| Cenário da fachada | [street_restaurant.png](../../Assets/Art/new/identification/backgrounds/street_restaurant.png) | 1920×1080 | 2,34 MB | Opaco, RGBA |
| Ornamento esquerdo | [arc_left.png](../../Assets/Art/new/identification/ornaments/arc_left.png) | 512×1024 | 25,3 KB | Recorte transparente |
| Ornamento direito | [arc_right.png](../../Assets/Art/new/identification/ornaments/arc_right.png) | 512×1024 | 25,5 KB | Recorte transparente |
| CH01 com prancheta | [ch01_welcome.png](../../Assets/Art/new/characters/CH01/ch01_welcome.png) | 1024×1536 | 474,8 KB | Recorte transparente |

Os PNGs somam **2,87 MB**. O cenário de tela inteira ocupa 2,34 MB, redução de **15,9%** ante o primeiro export de 2,78 MB. O acabamento mais uniforme foi feito pelo ImageGen, mantendo composição e arquitetura. A CH01 e os dois ornamentos somam **525,6 KB**.

A resolução do cenário permanece 1920×1080 conforme o manifesto, pois a ilustração preenche toda a tela. Não foi tratado como construção isolada do mapa. A recompressão PNG usa filtros escolhidos por tamanho com igualdade comprovada de todos os bytes RGBA; não houve redução de paleta, conversão JPEG ou remoção de alfa por código. O tamanho de build e a memória real dependem dos ajustes de import/plataforma da integração.

Cenário e personagem produzidos por ImageGen integrado, um asset por chamada. O modelo não é exposto pela ferramenta e está registrado como `null`. A versão anterior do cenário e os prompts de cada etapa foram preservados fora do Unity. Ornamentos produzidos em SVG determinístico, com rasterização direta em RGBA sRGB.

[SVG do ornamento esquerdo](../../art-source/editable/identification/ornaments/arc_left.svg) · [SVG do ornamento direito](../../art-source/editable/identification/ornaments/arc_right.svg)

A nova pose usa o master canônico da CH01, com SHA256 registrado. O master e `characters.json` permanecem intactos. A redução uniforme de 7/8 e o padding transparente enquadram a pessoa com margens verticais visíveis maiores que 10%, sem recortar mãos, cabeça ou pés.

Todos os PNGs têm pivot `[0.5,0.5]` e bordas zero. Usar escala uniforme: ornamentos cabem na caixa 320×860 como 320×640; a textura inteira da CH01 cabe na caixa 420×760 como 420×630. As caixas não devem ser usadas para esticar cada eixo.

[Proposta de montagem e posições](layouts/identification-layout.json) · [Camadas de arte montadas](../../art-source/previews/ART-14/ART-14-art-layers.png) · [Prova da identificação com componentes reutilizados](../../art-source/previews/ART-14/ART-14-identification-composed.png)

A composição de referência registra uma alternativa de encaixe uniforme pela silhueta visível da CH01 para aproximar sua presença em primeiro plano do concept. Nesse caso, a textura inteira é desenhada como 654×981 na posição `[15,57]`, com escala comum `654/1024`; a silhueta ocupa aproximadamente a caixa `[170,174,420,760]`. Nenhum pixel de alfa foi removido. A montagem não aplica esses valores a cenas.

A prova reutiliza painel, campos, botão e barra já validados nos ART-03/04/05. Os nomes e o botão Cadastrar aparecem apenas como simulação de texto nativo na prévia. A identificação não exibe caixa, score ou mês antes da sessão.

[Comparação de identidade](../../art-source/previews/ART-14/CH01-identity-comparison.png) · [Mãos e prancheta](../../art-source/previews/ART-14/CH01-welcome-hands-clipboard.png) · [CH01 nos dois fundos](../../art-source/previews/ART-14/CH01_WELCOME-display-light-dark.png) · [Ornamento esquerdo nos dois fundos](../../art-source/previews/ART-14/ID_ORNAMENT_LEFT-display-light-dark.png) · [Ornamento direito nos dois fundos](../../art-source/previews/ART-14/ID_ORNAMENT_RIGHT-display-light-dark.png)

[Prompts exatos, referências, fontes e SHA256](prompts/ART-14.json) · [Exportador técnico](../../art-source/editable/ART-14/export-art14.cjs) · [Recompressão PNG lossless](../../art-source/editable/ART-14/optimize-lossless.cjs) · [QA](qa/ART-14.json) · [Medições](qa/ART-14-measurements.json) · [Auditoria](qa/ART-14-scope-audit.json)

Validação dos quatro PNGs e do plano aprovada. Dimensões, RGBA sRGB, alfa, margens, fontes SVG, identidade e montagem revisados. Geometria dos ornamentos espelhada no SVG, com variações subpixel de antialias sem alteração perceptível.

Pendências de produção: nenhuma. Integração às cenas fica para etapa própria. Outros assets, fontes, identidades, cenas, scripts, catálogos e contratos do mapa foram preservados.
