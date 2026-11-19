/* ART-09: editable component masters and deterministic exact-size RGBA exports. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=d=>crypto.createHash('sha256').update(d).digest('hex');
const fileHash=p=>sha(fs.readFileSync(path.join(root,p)));
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n','utf8');};
const manifest=read('docs/art/manifest.json'),p=read('docs/art/tokens.json').palette;
const assets=manifest.assets.filter(a=>a.task_id==='ART-09');
const mix=(a,b,t)=>'#'+[1,3,5].map(i=>Math.round(parseInt(a.slice(i,i+2),16)*(1-t)+parseInt(b.slice(i,i+2),16)*t).toString(16).padStart(2,'0')).join('');
const {designs,body}=require('./designs.cjs')(p,mix);
const refs=['art-source/references/concepts/01-hub.png','art-source/references/concepts/03-gestao.png',
  'art-source/references/concepts/04-dre-mensal.png','art-source/references/concepts/05-dre-trimestral.png',
  'Assets/Art/new/shared/components/panels/panel_ivory.png','Assets/Art/new/shared/components/panels/panel_lavender.png',
  'Assets/Art/new/shared/components/controls/tab_selected.png','Assets/Art/new/shared/components/controls/slider_track.png'];
async function main(){
  if(assets.length!==18||Object.keys(designs).length!==18)throw new Error('Unexpected ART-09 inventory');
  for(const a of assets){
    if(a.production_method!=='vector_or_code'||a.display_fit!=='sliced'||!designs[a.id])throw new Error(`Review contract: ${a.id}`);
    for(const target of [`${manifest.asset_root}/${a.path}`,a.source_editable])if(fs.existsSync(path.join(root,target)))throw new Error(`Existing asset/source: ${target}`);
    if(designs[a.id].radius+1>Math.min(...a.nine_slice_lbrt_px)&&designs[a.id].shape!=='pixel_caps')throw new Error(`Corner leaves slice band: ${a.id}`);
  }
  for(const a of manifest.assets.filter(a=>['ART-03','ART-05'].includes(a.task_id)))
    if(a.status!=='validado'||fileHash(`${manifest.asset_root}/${a.path}`)!==a.sha256)throw new Error(`Dependency changed: ${a.id}`);
  for(const ref of refs)if(!fs.existsSync(path.join(root,ref)))throw new Error(`Missing reference: ${ref}`);
  write('docs/art/qa/ART-09-scope-baseline.json',{
    task_id:'ART-09',date:'2026-10-14',
    other_asset_records_sha256:sha(JSON.stringify(manifest.assets.filter(a=>a.task_id!=='ART-09'))),
    other_task_records_sha256:sha(JSON.stringify(manifest.tasks.filter(t=>t.id!=='ART-09'))),
    protected_files:Object.fromEntries(['docs/art/characters.json','docs/art/tokens.json','docs/art/catalogos.json',
      ...manifest.assets.filter(a=>a.status==='validado').map(a=>`${manifest.asset_root}/${a.path}`)].map(p=>[p,fileHash(p)]))
  });
  const records=[];
  for(const a of assets){
    const d=designs[a.id],[w,h]=a.export_size_px;
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body(d,w,h)}</svg>\n`;
    const source=path.join(root,a.source_editable),output=path.join(root,manifest.asset_root,a.path);
    fs.mkdirSync(path.dirname(source),{recursive:true});fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(source,svg,'utf8');
    await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(output);
    const meta=await sharp(output).metadata();if(meta.width!==w||meta.height!==h||meta.channels!==4)throw new Error(`Invalid PNG: ${a.id}`);
    const prompt=[`Produzir somente ${a.id}: ${d.name}, superfície isolada de UI Cogna.`,d.note,
      `Preenchimento uniforme ${d.fill}; ${d.border?`contorno interno ${d.stroke} de ${d.border} px`:'sem contorno'}; ${d.shape==='pixel_caps'?'quatro pixels de canto totalmente transparentes':`raio externo ${d.radius} px`}.`,
      `PNG RGBA 8-bit sRGB exatamente ${w}×${h}; display ${a.display_size_px.join('×')}; 9-slice l,b,r,t [${a.nine_slice_lbrt_px.join(', ')}]; pivot [0.5, 0.5].`,
      'Fonte SVG editável; cores derivadas dos tokens e acabamento plano coerente com concepts e ART-03/05. Não usar gradiente no centro expansível.',
      'Centro opaco, alfa verdadeiro nos cantos; preservar bounds, alfa e pivot entre estados. Preservar cantos e espessura de contorno ao expandir por 9-slice.',
      'Sem texto, labels, números, valores, datas, percentuais, ícones, gráficos, eixos, marcadores, divisórias de conteúdo, sombras, halos, logos ou checkerboard incorporados. Conteúdo dinâmico fica em camadas independentes.'
    ].join('\n');
    const record={id:a.id,name:d.name,family:d.family,state:d.state??null,part:d.part??null,
      output_path:`${manifest.asset_root}/${a.path}`,source_editable:a.source_editable,source_sha256:fileHash(a.source_editable),
      origin:'Geometria SVG original; manifesto, tokens e concepts aprovados, coerentes com ART-03/05.',
      references:refs.map(p=>({path:p,sha256:fileHash(p)})),final_prompt:prompt,
      tool:'deterministic SVG + sharp/librsvg',model:null,actual_size_px:[w,h],mode:'RGBA',color_space:'sRGB',
      sha256:fileHash(`${manifest.asset_root}/${a.path}`),design:d,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,status:'gerado'};
    records.push(record);
    Object.assign(a,{status:'gerado',source:record.origin,references:[...refs],sha256:record.sha256,actual_size_px:[w,h],generation_prompt:prompt,
      generation_tool:record.tool,generation_model:null,generation_model_note:'Lote geométrico; nenhum modelo generativo necessário.',production_record:'docs/art/prompts/ART-09.json'});
  }
  write('docs/art/prompts/ART-09.json',{task_id:'ART-09',date:'2026-10-14',status:'gerado',
    scope:'Somente os 18 indicadores do HUD e componentes da DRE do ART-09, separados e vazios conforme manifesto.',
    tool:'SVG editável e rasterização determinística sharp/librsvg',model:null,model_note:'Nenhum modelo generativo necessário para componentes geométricos exatos.',
    producer_source:'art-source/editable/ART-09/build-art09.cjs',geometry_source:'art-source/editable/ART-09/designs.cjs',renderer_versions:sharp.versions,palette:p,assets:records,
    assembly_notes:{
      hud:'Ícones e labels são camadas nativas independentes sobre UI_HUD_PILL e UI_KPI_TILE.',
      progress:'Track e fill separados; largura do fill dinâmica. Ocultar em zero; entre zero e 12 px, usar clipping nativo para preservar os caps. O preview usa zero/metade/cheio sem gravar valores no asset.',
      navigation:'UI_NAV_UNDERLINE lavanda é separado da barra e dos ícones; display de 240×4 com cantos de um pixel preservados.',
      pills:'Normal/selected/disabled compartilham a mesma silhueta, bounds, pivot e bordas; conteúdo separado.',
      toasts:'Neutral/success/warning compartilham geometria; mensagens e símbolos são camadas independentes.',
      dre:'Rows são fundos de linhas independentes. Total column é um destaque vertical separado; montar sob conteúdo nativo e revisar a ordem das camadas. Não há contas, colunas, totais ou gráficos rasterizados.',
      chart:'UI_CHART_PANEL é apenas superfície vazia; dados, eixos e barras devem ser desenhados nativamente na integração posterior.',
      integration_performed:false}});
  manifest.tasks.find(t=>t.id==='ART-09').status='gerado';write('docs/art/manifest.json',manifest);
  const tasks=read('docs/art/tasks.json');tasks.find(t=>t.id==='ART-09').status='gerado';write('docs/art/tasks.json',tasks);
  console.log(JSON.stringify({task_id:'ART-09',status:'gerado',png_count:18,svg_count:18}));
}
module.exports={designs,body};
if(require.main===module)main().catch(e=>{console.error(e);process.exitCode=1;});
