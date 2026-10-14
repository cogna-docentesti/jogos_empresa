/* ART-06: original editable white glyphs, one separate PNG master per symbol. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=d=>crypto.createHash('sha256').update(d).digest('hex');
const fileHash=p=>sha(fs.readFileSync(path.join(root,p)));
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n','utf8');};
const manifest=read('docs/art/manifest.json'),assets=manifest.assets.filter(a=>a.task_id==='ART-06');
const stroke=content=>`<g fill="none" stroke="#FFFFFF" stroke-width="16" stroke-linecap="round" stroke-linejoin="round">${content}</g>`;
const rect=(x,y,w,h,rx=0)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${rx}"/>`;
const circle=(x,y,r)=>`<circle cx="${x}" cy="${y}" r="${r}"/>`;
const pathEl=d=>`<path d="${d}"/>`;
const holeCircle=(x,y,r)=>`M${x-r} ${y}a${r} ${r} 0 1 0 ${r*2} 0a${r} ${r} 0 1 0 ${-r*2} 0Z`;
const polygon=points=>points.map((p,i)=>`${i?'L':'M'}${p[0].toFixed(3)} ${p[1].toFixed(3)}`).join('')+'Z';
const starPoints=Array.from({length:10},(_,i)=>{
  const a=(-90+i*36)*Math.PI/180,r=i%2?43:96;return [128+Math.cos(a)*r,132+Math.sin(a)*r];
});
const gearPoints=[];
for(let i=0;i<8;i++)for(const [offset,r] of [[-22.5,76],[-12,96],[12,96],[22.5,76]]){
  const a=(i*45-90+offset)*Math.PI/180;gearPoints.push([128+Math.cos(a)*r,128+Math.sin(a)*r]);
}
const corporateHoles=[];
for(const y of [64,104,144])for(const x of [80,120,160])corporateHoles.push(`M${x} ${y}h16v16h-16Z`);
const designs={
  ICON_HOME:{name:'Início',semantic:'Casa única com porta em recorte, conforme o símbolo de navegação do hub.',
    body:pathEl('M32 118L128 38L224 118L204 142L192 132V218H148V162H108V218H64V132L52 142Z')},
  ICON_RESTAURANT:{name:'Restaurante',semantic:'Garfo de três dentes e faca de cabo completo, sem círculo/badge, como o marcador de restaurante do concept.',
    body:stroke(pathEl('M62 40V92Q62 116 86 116Q110 116 110 92V40M86 40V216'))+
      pathEl('M194 32C168 52 162 84 162 120V136H178V216Q178 224 186 224H194Z')},
  ICON_PIN:{name:'Localização',semantic:'Pin de localização com centro vazado, sem mapa ou label.',
    body:`<path fill-rule="evenodd" d="M128 32C88 32 56 64 56 104C56 160 128 224 128 224C128 224 200 160 200 104C200 64 168 32 128 32Z${holeCircle(128,104,24)}"/>`},
  ICON_UNIVERSITY:{name:'Universidade',semantic:'Capelo acadêmico com borla; diferencia universidade de banco e edifício corporativo.',
    body:pathEl('M32 88L128 40L224 88L128 136ZM64 128L128 160L192 128V176Q128 216 64 176Z')+
      stroke(pathEl('M216 104V184'))+circle(216,192,8)},
  ICON_BANK:{name:'Banco',semantic:'Fachada bancária clássica com frontão e três colunas, distinta da pilha de moedas do caixa.',
    body:pathEl('M40 88L128 40L216 88Z')+rect(48,96,160,16,2)+
      rect(64,120,24,80,2)+rect(116,120,24,80,2)+rect(168,120,24,80,2)+rect(40,208,176,16,3)},
  ICON_COMMERCE:{name:'Comércio',semantic:'Loja com toldo e vitrine; símbolo comercial distinto da casa e do restaurante.',
    body:stroke(pathEl('M56 120V216H200V120'))+pathEl('M48 40H208L224 88H32ZM32 88H224V104C224 128 176 128 176 104C176 128 128 128 128 104C128 128 80 128 80 104C80 128 32 128 32 104Z')+
      rect(76,148,40,40,3)+stroke(rect(148,148,28,68,2))},
  ICON_RESIDENTIAL:{name:'Residencial',semantic:'Duas casas de alturas diferentes; distingue bairro residencial do botão de início.',
    body:stroke(pathEl('M40 136L88 96L136 136M56 136V216H120V136M120 104L168 64L216 104M136 104V216H200V104'))+
      rect(80,176,16,40,1)+rect(152,128,16,16,1)+rect(176,128,16,16,1)+rect(160,176,16,40,1)},
  ICON_CORPORATE:{name:'Empresas',semantic:'Edifício de escritórios vertical com janelas vazadas e entrada, sem letras.',
    body:`<path fill-rule="evenodd" d="M64 32H192Q200 32 200 40V224H144V184H112V224H56V40Q56 32 64 32Z${corporateHoles.join('')}"/>`},
  ICON_TEAM:{name:'Equipe',semantic:'Duas pessoas abstratas com cabeças e corpos distintos, como o glyph de equipe do concept; sem retratos.',
    body:circle(92,76,28)+circle(168,88,24)+
      pathEl('M40 216V184C40 152 64 132 92 132C120 132 144 152 144 184V216ZM160 216V184C160 164 154 150 144 142C152 138 160 136 172 136C196 136 216 154 216 180V216Z')},
  ICON_MENU_FOOD:{name:'Cardápio',semantic:'Livro de cardápio com garfo na capa, sem texto; distinto da folha de documento.',
    body:stroke(rect(48,40,160,176,12)+pathEl('M80 40V216M104 76V108Q104 128 128 128Q152 128 152 108V76M128 76V176'))},
  ICON_EQUIPMENT:{name:'Equipamentos',semantic:'Engrenagem de oito dentes, conforme navegação de equipamentos nos concepts. Centro vazado; sem objetos fotográficos.',
    body:`<path fill-rule="evenodd" d="${polygon(gearPoints)}${holeCircle(128,128,34)}"/>`},
  ICON_DOCUMENT:{name:'Documento',semantic:'Folha com dobra e duas linhas geométricas de conteúdo, sem letras ou valores.',
    body:stroke(pathEl('M64 40H152L200 88V216H64ZM152 40V88H200M88 128H168M88 168H152'))},
  ICON_CALENDAR:{name:'Calendário',semantic:'Calendário com duas argolas e seis células geométricas, sem mês, datas ou números.',
    body:stroke(rect(48,56,160,160,12)+pathEl('M88 40V72M168 40V72M48 104H208'))+
      [80,120,160].map(x=>rect(x,128,16,16,2)+rect(x,168,16,16,2)).join('')},
  ICON_STAR:{name:'Estrela',semantic:'Estrela sólida de cinco pontas, como o indicador de reputação/score do concept, sem valor ou badge.',body:pathEl(polygon(starPoints))},
  ICON_CASH:{name:'Caixa',semantic:'Pilha de moedas com separações transparentes, como o indicador de caixa do HUD; sem símbolo monetário ou quantidade.',
    body:'<ellipse cx="128" cy="56" rx="80" ry="24"/>'+pathEl('M48 88Q128 120 208 88V112Q128 144 48 112ZM48 136Q128 168 208 136V160Q128 192 48 160ZM48 184Q128 216 208 184V208Q128 240 48 208Z')},
  ICON_CHART_BARS:{name:'Gráfico de barras',semantic:'Três barras crescentes abstratas, conforme navegação financeira do concept; sem eixo, dados ou valores.',
    body:rect(40,144,40,72,2)+rect(108,96,40,120,2)+rect(176,40,40,176,2)},
  ICON_SETTINGS:{name:'Configurações',semantic:'Três ajustes horizontais com seletores vazados; diferencia configurações da engrenagem de equipamentos.',
    body:stroke(pathEl('M40 64H56M120 64H216M40 128H136M200 128H216M40 192H80M144 192H216')+circle(88,64,16)+circle(168,128,16)+circle(112,192,16))},
  ICON_CART:{name:'Carrinho',semantic:'Carrinho de compras com cesto aberto e duas rodas, sem quantidade ou badge.',
    body:stroke(pathEl('M40 48H64L88 168H200L216 88H72'))+circle(100,208,12)+circle(188,208,12)}
};
const refs=['art-source/references/concepts/01-hub.png','art-source/references/concepts/02-abertura.png','art-source/references/concepts/03-gestao.png','art-source/references/concepts/04-dre-mensal.png'];
async function main(){
  if(assets.length!==18||Object.keys(designs).length!==18)throw new Error('Unexpected ART-06 inventory');
  for(const a of assets){
    if(a.production_method!=='vector_or_code'||a.export_size_px.join(',')!=='256,256'||a.display_fit!=='contain')throw new Error(`Review contract: ${a.id}`);
    for(const p of [`${manifest.asset_root}/${a.path}`,a.source_editable])if(fs.existsSync(path.join(root,p)))throw new Error(`Existing asset/source: ${p}`);
  }
  for(const a of manifest.assets.filter(a=>a.task_id==='ART-03'))if(a.status!=='validado'||fileHash(`${manifest.asset_root}/${a.path}`)!==a.sha256)throw new Error(`Dependency changed: ${a.id}`);
  write('docs/art/qa/ART-06-scope-baseline.json',{
    task_id:'ART-06',date:'2026-10-14',other_asset_records_sha256:sha(JSON.stringify(manifest.assets.filter(a=>a.task_id!=='ART-06'))),
    other_task_records_sha256:sha(JSON.stringify(manifest.tasks.filter(t=>t.id!=='ART-06'))),
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
      'Símbolo UI editorial sóbrio, monocromático branco #FFFFFF tintável, sem fundo, badge, sombra ou texto; geometria original coerente com os concepts Cogna.',
      'Export PNG RGBA 8-bit sRGB exatamente 256×256; SVG editável no mesmo viewBox. Pivot [0.5, 0.5], sem 9-slice; escala uniforme contain para 40×40.',
      'Leitura em 32, 40 e 48 px. Traço-base 16 px no master equivale a 2, 2.5 e 3 px; preenchimentos sólidos para silhuetas que aparecem assim nos concepts.',
      'Margem segura mínima de 10% em cada borda; desenho centralizado opticamente em caixa comum de aproximadamente x/y=32..224. Vazios internos verdadeiramente transparentes.',
      'Apenas um master por símbolo, sem variantes finais por tela ou cor. Sem letras, números, datas, valores, gráficos de dados, logotipos, outras pessoas ou cenário.'
    ].join('\n');
    const record={id:a.id,name:d.name,semantic:d.semantic,output_path:`${manifest.asset_root}/${a.path}`,
      source_editable:a.source_editable,source_sha256:fileHash(a.source_editable),
      origin:'Geometria vetorial original, interpretada dos concepts aprovados e especificação do manifesto.',
      references:refs.map(p=>({path:p,sha256:fileHash(p)})),final_prompt:prompt,tool:'original SVG + deterministic sharp/librsvg rasterization',
      model:null,actual_size_px:[256,256],mode:'RGBA',color_space:'sRGB',master_color:'#FFFFFF',tintable:true,
      line_weight_master_px:16,line_weight_at_display_px:{32:2,40:2.5,48:3},sha256:fileHash(`${manifest.asset_root}/${a.path}`),status:'gerado'};
    records.push(record);
    Object.assign(a,{status:'gerado',source:record.origin,references:[...refs],sha256:record.sha256,actual_size_px:[256,256],
      generation_prompt:prompt,generation_tool:record.tool,generation_model:null,generation_model_note:'Ícones geométricos originais em SVG; nenhum modelo generativo necessário.',
      production_record:'docs/art/prompts/ART-06.json',master_color:'#FFFFFF',tintable:true});
  }
  write('docs/art/prompts/ART-06.json',{task_id:'ART-06',date:'2026-10-14',status:'gerado',
    scope:'Somente os dezoito ícones de navegação e HUD; controles/campos/seleção já entregues no ART-05 não são repetidos.',
    tool:'SVG original e rasterização determinística sharp/librsvg',model:null,model_note:'Nenhum modelo generativo necessário para geometria simples e consistente.',
    producer_source:'art-source/editable/ART-06/build-art06.cjs',renderer_versions:sharp.versions,assets:records,
    semantic_choices:{university:'Capelo acadêmico',bank:'Fachada com colunas',equipment:'Engrenagem conforme concept',settings:'Ajustes horizontais para evitar duplicar a engrenagem',menu_food:'Livro com garfo',document:'Folha dobrada',residential:'Duas casas',home:'Casa única'}});
  manifest.tasks.find(t=>t.id==='ART-06').status='gerado';write('docs/art/manifest.json',manifest);
  const tasks=read('docs/art/tasks.json');tasks.find(t=>t.id==='ART-06').status='gerado';write('docs/art/tasks.json',tasks);
  console.log(JSON.stringify({task_id:'ART-06',status:'gerado',png_count:18,svg_count:18}));
}
module.exports={designs};
if(require.main===module)main().catch(e=>{console.error(e);process.exitCode=1;});
