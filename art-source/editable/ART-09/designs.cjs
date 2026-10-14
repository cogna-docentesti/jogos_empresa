/* Original, flat-color component geometry. All outer coverage is clipped once. */
module.exports=(p,mix)=>{
  const muted=mix(p.ivory,p.ink,.07),mutedLine=mix(p.ivory,p.ink,.13);
  const activeLine=mix(p.violet,p.lavender,.45);
  const designs={
    UI_HUD_PILL:{name:'Pílula do HUD',family:'hud',part:'pill',radius:22,fill:mix(p.violet,p.plum,.25),stroke:mix(p.violet,p.lavender,.28),border:1,
      note:'Base violeta profunda para indicadores da barra superior do hub; sem conteúdo. Cantos de 22 px dentro das bordas 24 px.'},
    UI_KPI_TILE:{name:'Base de KPI',family:'kpi',part:'tile',radius:14,fill:mix(p.ivory,'#FFFFFF',.65),stroke:mix(p.ivory,p.ink,.12),border:1,
      note:'Superfície clara de KPI conforme cards de receita/resultado/clientes das DREs, sem dados, ícone ou gráfico.'},
    UI_PROGRESS_TRACK:{name:'Trilho de progresso',family:'progress',part:'track',radius:5,fill:mix(p.ivory,p.plum,.18),stroke:null,border:0,
      note:'Trilho cinza-lavanda uniforme. Raio 5 px dentro das faixas 6 px; display 480×16, sem divisões, etapa ou percentual incorporado.'},
    UI_PROGRESS_FILL:{name:'Preenchimento de progresso',family:'progress',part:'fill',radius:5,fill:p.violet,stroke:null,border:0,
      note:'Preenchimento violeta independente, com a mesma silhueta do trilho. Largura dinâmica, sem etapas ou percentual fixados na arte.'},
    UI_NAV_UNDERLINE:{name:'Sublinhado da navegação',family:'nav',part:'underline',shape:'pixel_caps',radius:0,fill:p.lavender,stroke:null,border:0,
      note:'Linha lavanda para navegação ativa; quatro cantos transparentes de 1×1 px, formando extremidades em degrau de um pixel na exibição de 4 px. Nenhum texto ou ícone.'},
    UI_PILL_NORMAL:{name:'Pílula normal',family:'pill',state:'normal',radius:22,fill:mix(p.ivory,p.lavender,.16),stroke:mix(p.ivory,p.lavender,.5),border:1,
      note:'Pílula de seleção neutra lavanda clara. Mesma geometria e alfa dos estados selecionado/desabilitado, sem marcador.'},
    UI_PILL_SELECTED:{name:'Pílula selecionada',family:'pill',state:'selected',radius:22,fill:p.violet,stroke:activeLine,border:1,
      note:'Pílula selecionada violeta, coerente com a seleção aprovada e com o kit ART-05; sem check incorporado.'},
    UI_PILL_DISABLED:{name:'Pílula desabilitada',family:'pill',state:'disabled',radius:22,fill:muted,stroke:mutedLine,border:1,
      note:'Pílula desabilitada com fundo dessaturado e contorno discreto; sem deslocar bounds ou pivot.'},
    UI_TOAST_NEUTRAL:{name:'Aviso neutro',family:'toast',state:'neutral',radius:16,fill:mix(p.ivory,'#FFFFFF',.7),stroke:mix(p.ivory,p.lavender,.6),border:2,
      note:'Superfície de aviso neutro clara, com contorno lavanda; mensagem, botão de fechar e ícone permanecem separados.'},
    UI_TOAST_SUCCESS:{name:'Aviso de sucesso',family:'toast',state:'success',radius:16,fill:mix(p.ivory,p.teal,.13),stroke:p.teal,border:2,
      note:'Superfície de sucesso em verde muito claro e contorno teal; sem símbolo de confirmação ou mensagem.'},
    UI_TOAST_WARNING:{name:'Aviso de atenção',family:'toast',state:'warning',radius:16,fill:mix(p.ivory,p.amber,.24),stroke:mix(p.amber,p.ink,.15),border:2,
      note:'Superfície de atenção em âmbar claro e contorno ocre derivado do token amber; sem triângulo ou mensagem.'},
    UI_TABLE_HEADER:{name:'Cabeçalho da DRE',family:'table_header',part:'header',radius:8,fill:mix(p.ivory,p.lavender,.35),stroke:mix(p.ivory,p.lavender,.5),border:1,
      note:'Faixa de cabeçalho lavanda clara conforme DRE trimestral; sem colunas, períodos, rótulos ou valores.'},
    UI_TABLE_ROW_NEUTRAL:{name:'Linha neutra da DRE',family:'table_row',state:'neutral',radius:6,fill:mix(p.ivory,'#FFFFFF',.6),stroke:mix(p.ivory,p.ink,.12),border:1,
      note:'Linha de tabela clara neutra, com contorno discreto; nenhuma conta, separador vertical ou valor incorporado.'},
    UI_TABLE_ROW_SUBTOTAL:{name:'Subtotal da DRE',family:'table_row',state:'subtotal',radius:6,fill:mix(p.ivory,p.lavender,.38),stroke:mix(p.ivory,p.lavender,.5),border:1,
      note:'Linha de subtotal lavanda, como os resultados bruto/operacional do concept mensal; geometria compartilhada com as demais linhas.'},
    UI_TABLE_ROW_POSITIVE:{name:'Resultado positivo da DRE',family:'table_row',state:'positive',radius:6,fill:mix(p.ivory,p.teal,.18),stroke:mix(p.ivory,p.teal,.28),border:1,
      note:'Linha de resultado positivo em teal claro, conforme resultado mensal e acumulado dos concepts; sem sinal ou valor incorporado.'},
    UI_TABLE_ROW_NEGATIVE:{name:'Resultado negativo da DRE',family:'table_row',state:'negative',radius:6,fill:mix(p.ivory,p.coral,.16),stroke:mix(p.ivory,p.coral,.28),border:1,
      note:'Extensão semântica do token coral para linha de resultado negativo; mesmos bounds/pivot/alfa da linha positiva, sem valor ou sinal.'},
    UI_TABLE_TOTAL_COLUMN:{name:'Coluna de total da DRE',family:'table_total',part:'column',radius:10,fill:mix(p.ivory,p.lavender,.44),stroke:mix(p.ivory,p.lavender,.6),border:1,
      note:'Destaque vertical lavanda para coluna total, conforme DRE trimestral; sem células, divisórias, números ou rótulo.'},
    UI_CHART_PANEL:{name:'Superfície do gráfico',family:'chart',part:'panel',radius:16,fill:mix(p.ivory,'#FFFFFF',.7),stroke:mix(p.ivory,p.lavender,.5),border:1,
      note:'Painel claro para área de gráfico nativo, coerente com a DRE; export vazio, sem barras, eixos, legenda ou dados.'}
  };
  function body(d,w,h){
    if(d.shape==='pixel_caps')return `<path fill="${d.fill}" d="M1 0 H${w-1} V1 H${w} V${h-1} H${w-1} V${h} H1 V${h-1} H0 V1 H1 Z"/>`;
    const b=d.border;
    const clip=`<rect width="${w}" height="${h}" rx="${d.radius}"/>`;
    const line=b?`<rect x="${b/2}" y="${b/2}" width="${w-b}" height="${h-b}" rx="${d.radius-b/2}" fill="none" stroke="${d.stroke}" stroke-width="${b}"/>`:'';
    return `<defs><clipPath id="silhouette">${clip}</clipPath></defs><g clip-path="url(#silhouette)"><rect width="${w}" height="${h}" fill="${d.fill}"/>${line}</g>`;
  }
  return {designs,body};
};
