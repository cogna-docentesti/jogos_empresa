/* ART-07: original financial/indicator glyphs, separate white tintable masters. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=d=>crypto.createHash('sha256').update(d).digest('hex');
const fileHash=p=>sha(fs.readFileSync(path.join(root,p)));
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n','utf8');};
const manifest=read('docs/art/manifest.json'),assets=manifest.assets.filter(a=>a.task_id==='ART-07');
const stroke=body=>`<g fill="none" stroke="#FFFFFF" stroke-width="16" stroke-linecap="round" stroke-linejoin="round">${body}</g>`;
const rect=(x,y,w,h,rx=0)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${rx}"/>`;
const circle=(x,y,r)=>`<circle cx="${x}" cy="${y}" r="${r}"/>`;
const pathEl=d=>`<path d="${d}"/>`;
const trend=stroke(pathEl('M40 192 L96 136 L128 168 L216 64 M168 64 H216 V112'));
const designs={
  ICON_REVENUE:{name:'Receita',semantic:'Entrada de receita representada por cédula com seta entrando por cima; distinta das barras e moedas do ART-06.',
    body:stroke(rect(40,112,176,104,12)+circle(128,164,20)+pathEl('M128 40 V96 M104 72 L128 96 L152 72'))},
  ICON_TREND_UP:{name:'Tendência de alta',semantic:'Linha ascendente em zigue-zague com seta no extremo superior direito, conforme o resultado previsto dos concepts.',body:trend},
  ICON_TREND_DOWN:{name:'Tendência de queda',semantic:'Par descendente da tendência de alta, espelhado verticalmente no mesmo canvas e pivot.',body:`<g transform="translate(0 256) scale(1 -1)">${trend}</g>`},
  ICON_CLIENTS:{name:'Clientes',semantic:'Três pessoas abstratas, para público/clientes; distinto do símbolo de equipe com duas pessoas do ART-06.',
    body:circle(128,68,24)+circle(64,104,20)+circle(192,104,20)+
      pathEl('M88 216 V176 C88 144 104 128 128 128 C152 128 168 144 168 176 V216 Z M32 216 V180 C32 156 48 144 64 144 H72 V216 Z M184 216 V144 H192 C208 144 224 156 224 180 V216 Z')},
  ICON_CLIENT:{name:'Cliente',semantic:'Uma pessoa abstrata, sem retrato ou identidade canônica, para unidade de cliente.',
    body:circle(128,72,32)+pathEl('M56 216 V184 C56 144 88 120 128 120 C168 120 200 144 200 184 V216 Z')},
  ICON_COHERENCE:{name:'Coerência',semantic:'Dois elos interligados simbolizando compatibilidade entre decisões; sem check ou badge.',
    body:stroke('<rect x="48" y="120" width="112" height="64" rx="32" transform="rotate(-45 104 152)"/><rect x="96" y="72" width="112" height="64" rx="32" transform="rotate(-45 152 104)"/>')},
  ICON_REPUTATION:{name:'Reputação',semantic:'Polegar de aprovação; diferencia avaliação/reputação da estrela genérica de score já entregue no ART-06.',
    body:rect(40,128,40,88,6)+pathEl('M96 216 V120 L120 88 L128 48 Q132 32 148 40 Q168 48 160 80 L152 112 H200 Q224 112 216 136 L200 200 Q196 216 180 216 Z')},
  ICON_RENT:{name:'Aluguel',semantic:'Chave de acesso ao imóvel, para custo de aluguel; sem casa duplicada, preço ou contrato incorporado.',
    body:stroke(circle(72,72,32)+pathEl('M96 96 L208 208 M152 152 L168 136 M184 184 L200 168'))},
  ICON_CAPACITY:{name:'Capacidade',semantic:'Medidor com ponteiro, representando capacidade operacional; sem escala numérica ou gráfico de dados.',
    body:stroke(pathEl('M64 208 A88 88 0 1 1 192 208 M128 72 V88 M56 128 L72 136 M184 136 L200 128 M128 152 L176 104'))+circle(128,152,12)},
  ICON_COMPETITION:{name:'Concorrência',semantic:'Duas setas em sentidos opostos, para comparação e disputa de alternativas; sem logotipos ou concorrentes específicos.',
    body:stroke(pathEl('M48 88 H208 M176 56 L208 88 L176 120 M208 168 H48 M80 136 L48 168 L80 200'))},
  ICON_INPUT_COSTS:{name:'Custos de insumos',semantic:'Saco de insumos com pequeno ramo geométrico, distinto da caixa de estoque e de valores monetários.',
    body:stroke(pathEl('M80 40 H176 L160 80 C184 112 208 144 208 184 Q208 216 176 216 H80 Q48 216 48 184 C48 144 72 112 96 80 Z M96 80 H160 M128 128 V184'))+
      pathEl('M104 128 L128 152 H104 Z M152 144 L128 168 H152 Z')},
  ICON_UTILITIES:{name:'Despesas operacionais',semantic:'Gota com raio em recorte transparente, combinando água e energia como despesas de utilidades.',
    body:'<path fill-rule="evenodd" d="M128 32 C112 64 56 112 56 156 C56 200 88 224 128 224 C168 224 200 200 200 156 C200 112 144 64 128 32 Z M136 88 L104 144 H128 L112 184 L160 128 H136 Z"/>'},
  ICON_PAYROLL:{name:'Folha de pagamento',semantic:'Recibo de pagamento com pessoa e campos geométricos, sem nome, salário, letras ou valores.',
    body:stroke(rect(40,56,176,144,12)+pathEl('M144 104 H184 M144 144 H168'))+circle(88,100,16)+pathEl('M64 168 V152 Q64 128 88 128 Q112 128 112 152 V168 Z')},
  ICON_INTEREST:{name:'Juros',semantic:'Taxa percentual desenhada com duas circunferências e diagonal, sem tipografia ou números.',
    body:stroke(circle(80,80,28)+circle(176,176,28)+pathEl('M64 208 L192 48'))},
  ICON_TERM:{name:'Prazo',semantic:'Ampulheta de prazo, distinta do calendário do ART-06; sem datas ou contagem.',
    body:stroke(pathEl('M48 40 H208 M48 216 H208 M64 40 V72 Q64 104 128 128 Q192 152 192 184 V216 M192 40 V72 Q192 104 128 128 Q64 152 64 184 V216'))+
      pathEl('M88 192 L128 152 L168 192 Z')},
  ICON_LOAN_PRINCIPAL:{name:'Principal do empréstimo',semantic:'Duas cédulas sobrepostas, simbolizando principal disponibilizado; sem repetir a pilha de moedas ou seta de receita.',
    body:stroke(pathEl('M40 144 V64 H184 V88')+rect(64,104,152,104,10)+circle(140,156,20))},
  ICON_INVENTORY:{name:'Estoque',semantic:'Caixa tridimensional simples, para estoque; faces transparentes e nenhuma quantidade ou produto incorporado.',
    body:stroke(pathEl('M128 40 L216 80 L128 120 L40 80 Z M40 80 V176 L128 216 L216 176 V80 M128 120 V216'))},
  ICON_CREDIT_CONTRACT:{name:'Contrato de crédito',semantic:'Folha contratual com caneta geométrica; sem assinatura, letras, valores ou texto rasterizado.',
    body:stroke(pathEl('M192 104 V40 H56 V216 H120 M88 80 H160 M88 112 H144'))+
      pathEl('M128 192 L136 160 L196 100 L220 124 L160 184 Z')}
};
const refs=['art-source/references/concepts/01-hub.png','art-source/references/concepts/03-gestao.png',
  'art-source/references/concepts/04-dre-mensal.png','art-source/references/concepts/05-dre-trimestral.png',
  'Assets/Art/new/shared/icons/icon_chart_bars.png','Assets/Art/new/shared/icons/icon_cash.png',
  'Assets/Art/new/shared/icons/icon_team.png','Assets/Art/new/shared/icons/icon_document.png'];
async function main(){
  if(assets.length!==18||Object.keys(designs).length!==18)throw new Error('Unexpected ART-07 inventory');
  for(const a of assets){
    if(a.production_method!=='vector_or_code'||a.export_size_px.join(',')!=='256,256'||a.display_fit!=='contain')throw new Error(`Review contract: ${a.id}`);
    for(const p of [`${manifest.asset_root}/${a.path}`,a.source_editable])if(fs.existsSync(path.join(root,p)))throw new Error(`Existing asset/source: ${p}`);
  }
  for(const a of manifest.assets.filter(a=>a.task_id==='ART-03'))if(a.status!=='validado'||fileHash(`${manifest.asset_root}/${a.path}`)!==a.sha256)throw new Error(`Dependency changed: ${a.id}`);
  for(const ref of refs)if(!fs.existsSync(path.join(root,ref)))throw new Error(`Missing reference: ${ref}`);
  write('docs/art/qa/ART-07-scope-baseline.json',{
    task_id:'ART-07',date:'2026-10-14',other_asset_records_sha256:sha(JSON.stringify(manifest.assets.filter(a=>a.task_id!=='ART-07'))),
    other_task_records_sha256:sha(JSON.stringify(manifest.tasks.filter(t=>t.id!=='ART-07'))),
    protected_files:Object.fromEntries(['docs/art/characters.json','docs/art/tokens.json','docs/art/catalogos.json',
      ...manifest.assets.filter(a=>a.status==='validado').map(a=>`${manifest.asset_root}/${a.path}`)].map(p=>[p,fileHash(p)]))
  });
  const records=[];
  for(const a of assets){
    const d=designs[a.id];
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256"><g fill="#FFFFFF">${d.body}</g></svg>\n`;
    const source=path.join(root,a.source_editable),output=path.join(root,manifest.asset_root,a.path);
    fs.mkdirSync(path.dirname(source),{recursive:true});fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(source,svg,'utf8');
    await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(output);
    const meta=await sharp(output).metadata();if(meta.width!==256||meta.height!==256||meta.channels!==4)throw new Error(`Invalid PNG: ${a.id}`);
    const prompt=[`Criar somente o master ${a.id}: ${d.semantic}`,
      'Símbolo UI editorial sóbrio, monocromático branco #FFFFFF tintável, sem fundo, badge, sombra ou texto; geometria original coerente com os concepts Cogna e o kit ART-06.',
      'Export PNG RGBA 8-bit sRGB exatamente 256×256; SVG editável no mesmo viewBox. Pivot [0.5, 0.5], sem 9-slice; escala uniforme contain para 40×40.',
      'Leitura em 32, 40 e 48 px. Traço-base 16 px no master equivale a 2, 2.5 e 3 px; preenchimentos sólidos apenas quando favorecem a silhueta do símbolo.',
      'Margem segura mínima de 10% em cada borda; centro óptico dentro da mesma caixa de desenho do ART-06. Vazios internos verdadeiramente transparentes.',
      'Apenas um master por símbolo, sem variantes por tela/cor e sem duplicar os símbolos existentes. Sem letras, números, datas, salários, preços, valores, dados gráficos, logos ou cenário.'
    ].join('\n');
    const r={id:a.id,name:d.name,semantic:d.semantic,output_path:`${manifest.asset_root}/${a.path}`,
      source_editable:a.source_editable,source_sha256:fileHash(a.source_editable),
      origin:'Geometria vetorial original a partir do significado do manifesto, concepts aprovados e linguagem estabelecida no ART-06.',
      references:refs.map(p=>({path:p,sha256:fileHash(p)})),final_prompt:prompt,
      tool:'original SVG + deterministic sharp/librsvg rasterization',model:null,actual_size_px:[256,256],mode:'RGBA',color_space:'sRGB',
      master_color:'#FFFFFF',tintable:true,line_weight_master_px:16,line_weight_at_display_px:{32:2,40:2.5,48:3},
      sha256:fileHash(`${manifest.asset_root}/${a.path}`),status:'gerado'};
    records.push(r);
    Object.assign(a,{status:'gerado',source:r.origin,references:[...refs],sha256:r.sha256,actual_size_px:[256,256],
      generation_prompt:prompt,generation_tool:r.tool,generation_model:null,generation_model_note:'Ícones geométricos originais em SVG; nenhum modelo generativo necessário.',
      production_record:'docs/art/prompts/ART-07.json',master_color:'#FFFFFF',tintable:true});
  }
  write('docs/art/prompts/ART-07.json',{task_id:'ART-07',date:'2026-10-14',status:'gerado',
    scope:'Somente os dezoito ícones financeiros e indicadores do ART-07; nenhum controle do ART-05 repetido.',
    tool:'SVG original e rasterização determinística sharp/librsvg',model:null,model_note:'Nenhum modelo generativo necessário para geometria simples e consistente.',
    producer_source:'art-source/editable/ART-07/build-art07.cjs',renderer_versions:sharp.versions,assets:records,
    semantic_choices:{revenue:'Entrada em cédula',clients:'Três pessoas versus equipe de duas do ART-06',client:'Pessoa única',
      coherence:'Elos interligados',reputation:'Polegar de aprovação versus estrela de score',rent:'Chave',capacity:'Medidor operacional',
      competition:'Setas opostas',input_costs:'Saco de insumos',utilities:'Água/energia',payroll:'Recibo de pagamento',
      interest:'Percentual geométrico',term:'Ampulheta',loan_principal:'Cédulas sobrepostas',inventory:'Caixa',credit_contract:'Folha com caneta'}});
  manifest.tasks.find(t=>t.id==='ART-07').status='gerado';write('docs/art/manifest.json',manifest);
  const tasks=read('docs/art/tasks.json');tasks.find(t=>t.id==='ART-07').status='gerado';write('docs/art/tasks.json',tasks);
  console.log(JSON.stringify({task_id:'ART-07',status:'gerado',png_count:18,svg_count:18}));
}
module.exports={designs};
if(require.main===module)main().catch(e=>{console.error(e);process.exitCode=1;});
