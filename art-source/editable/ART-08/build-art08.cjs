/* ART-08: original action/status/category glyphs, separate white tintable masters. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=d=>crypto.createHash('sha256').update(d).digest('hex');
const fileHash=p=>sha(fs.readFileSync(path.join(root,p)));
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n','utf8');};
const manifest=read('docs/art/manifest.json'),assets=manifest.assets.filter(a=>a.task_id==='ART-08');
const designs=require('./designs.cjs');
const refs=['art-source/references/concepts/01-hub.png','art-source/references/concepts/03-gestao.png',
  'art-source/references/concepts/04-dre-mensal.png','art-source/references/concepts/05-dre-trimestral.png',
  'Assets/Art/new/shared/icons/icon_chart_bars.png','Assets/Art/new/shared/icons/icon_cash.png',
  'Assets/Art/new/shared/icons/icon_team.png','Assets/Art/new/shared/icons/icon_document.png'];
async function main(){
  if(assets.length!==33||Object.keys(designs).length!==33)throw new Error('Unexpected ART-08 inventory');
  for(const a of assets){
    if(a.production_method!=='vector_or_code'||a.export_size_px.join(',')!=='256,256'||a.display_fit!=='contain')throw new Error(`Review contract: ${a.id}`);
    for(const p of [`${manifest.asset_root}/${a.path}`,a.source_editable])if(fs.existsSync(path.join(root,p)))throw new Error(`Existing asset/source: ${p}`);
  }
  for(const a of manifest.assets.filter(a=>a.task_id==='ART-03'))if(a.status!=='validado'||fileHash(`${manifest.asset_root}/${a.path}`)!==a.sha256)throw new Error(`Dependency changed: ${a.id}`);
  for(const ref of refs)if(!fs.existsSync(path.join(root,ref)))throw new Error(`Missing reference: ${ref}`);
  write('docs/art/qa/ART-08-scope-baseline.json',{
    task_id:'ART-08',date:'2026-10-14',other_asset_records_sha256:sha(JSON.stringify(manifest.assets.filter(a=>a.task_id!=='ART-08'))),
    other_task_records_sha256:sha(JSON.stringify(manifest.tasks.filter(t=>t.id!=='ART-08'))),
    protected_files:Object.fromEntries(['docs/art/characters.json','docs/art/tokens.json','docs/art/catalogos.json',
      ...manifest.assets.filter(a=>a.status==='validado').map(a=>`${manifest.asset_root}/${a.path}`)].map(p=>[p,fileHash(p)]))
  });
  const records=[];
  for(const a of assets){
    const d=designs[a.id];if(!d)throw new Error(`Missing original geometry: ${a.id}`);
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256"><g fill="#FFFFFF">${d.body}</g></svg>\n`;
    const source=path.join(root,a.source_editable),output=path.join(root,manifest.asset_root,a.path);
    fs.mkdirSync(path.dirname(source),{recursive:true});fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(source,svg,'utf8');
    await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(output);
    const meta=await sharp(output).metadata();if(meta.width!==256||meta.height!==256||meta.channels!==4)throw new Error(`Invalid PNG: ${a.id}`);
    const prompt=[`Criar somente o master ${a.id}: ${d.semantic}`,
      'Símbolo UI editorial sóbrio, monocromático branco #FFFFFF tintável, sem fundo, badge, sombra ou texto; geometria original coerente com os concepts Cogna e o kit ART-06/07.',
      'Export PNG RGBA 8-bit sRGB exatamente 256×256; SVG editável no mesmo viewBox. Pivot [0.5, 0.5], sem 9-slice; escala uniforme contain para 40×40.',
      'Leitura em 32, 40 e 48 px. Traço-base 16 px no master equivale a 2, 2.5 e 3 px; preenchimentos sólidos apenas quando favorecem a silhueta do símbolo.',
      'Margem segura mínima de 10% em cada borda; centro óptico dentro da mesma caixa de desenho do ART-06/07. Vazios internos verdadeiramente transparentes.',
      'Apenas um master por símbolo, sem variantes por tela/cor e sem duplicar os símbolos existentes. Sem letras, números, datas, salários, preços, valores, dados gráficos, logos ou cenário.'
    ].join('\n');
    const r={id:a.id,name:d.name,semantic:d.semantic,output_path:`${manifest.asset_root}/${a.path}`,
      source_editable:a.source_editable,source_sha256:fileHash(a.source_editable),
      origin:'Geometria vetorial original a partir do significado do manifesto, concepts aprovados e linguagem estabelecida no ART-06/07.',
      references:refs.map(p=>({path:p,sha256:fileHash(p)})),final_prompt:prompt,
      tool:'original SVG + deterministic sharp/librsvg rasterization',model:null,actual_size_px:[256,256],mode:'RGBA',color_space:'sRGB',
      master_color:'#FFFFFF',tintable:true,line_weight_master_px:16,line_weight_at_display_px:{32:2,40:2.5,48:3},
      sha256:fileHash(`${manifest.asset_root}/${a.path}`),status:'gerado'};
    records.push(r);
    Object.assign(a,{status:'gerado',source:r.origin,references:[...refs],sha256:r.sha256,actual_size_px:[256,256],
      generation_prompt:prompt,generation_tool:r.tool,generation_model:null,generation_model_note:'Ícones geométricos originais em SVG; nenhum modelo generativo necessário.',
      production_record:'docs/art/prompts/ART-08.json',master_color:'#FFFFFF',tintable:true});
  }
  write('docs/art/prompts/ART-08.json',{task_id:'ART-08',date:'2026-10-14',status:'gerado',
    scope:'Somente os 33 ícones de ações, status e categorias do ART-08; componentes dos outros lotes preservados.',
    tool:'SVG original e rasterização determinística sharp/librsvg',model:null,model_note:'Nenhum modelo generativo necessário para geometria simples e consistente.',
    producer_source:'art-source/editable/ART-08/build-art08.cjs',renderer_versions:sharp.versions,assets:records,
    semantic_choices:Object.fromEntries(Object.entries(designs).map(([id,d])=>[id,d.semantic]))});
  manifest.tasks.find(t=>t.id==='ART-08').status='gerado';write('docs/art/manifest.json',manifest);
  const tasks=read('docs/art/tasks.json');tasks.find(t=>t.id==='ART-08').status='gerado';write('docs/art/tasks.json',tasks);
  console.log(JSON.stringify({task_id:'ART-08',status:'gerado',png_count:33,svg_count:33}));
}
module.exports={designs};
if(require.main===module)main().catch(e=>{console.error(e);process.exitCode=1;});
