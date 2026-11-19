/* ART-05: deterministic editable sources and separate exact-size RGBA exports. */
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=d=>crypto.createHash('sha256').update(d).digest('hex');
const fileHash=p=>sha(fs.readFileSync(path.join(root,p)));
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n','utf8');};
const manifest=read('docs/art/manifest.json');
const p=read('docs/art/tokens.json').palette;
const assets=manifest.assets.filter(a=>a.task_id==='ART-05');
const mix=(a,b,t)=>'#'+[1,3,5].map(i=>Math.round(parseInt(a.slice(i,i+2),16)*(1-t)+parseInt(b.slice(i,i+2),16)*t).toString(16).padStart(2,'0')).join('');
const muted=mix(p.ivory,p.ink,.07), mutedLine=mix(p.ivory,p.ink,.13);
const activeLine=mix(p.violet,p.lavender,.45), neutralLine=mix(p.plum,p.lavender,.65);
const designs={
  UI_INPUT_NORMAL:{family:'input',state:'normal',radius:8,fill:'#FFFFFF',stroke:neutralLine,border:2,note:'Campo branco, contorno neutro lavanda discreto, conforme identificação em 02-abertura.'},
  UI_INPUT_FOCUS:{family:'input',state:'focus',radius:8,fill:'#FFFFFF',stroke:p.violet,border:3,note:'Mesmo campo com contorno violeta de foco mais espesso, aplicado para dentro da silhueta; sem halo.'},
  UI_INPUT_INVALID:{family:'input',state:'invalid',radius:8,fill:mix('#FFFFFF',p.coral,.04),stroke:p.coral,border:3,note:'Mesmo campo com contorno coral de erro e fundo levemente aquecido; sem mensagem ou ícone de erro.'},
  UI_INPUT_DISABLED:{family:'input',state:'disabled',radius:8,fill:muted,stroke:mutedLine,border:2,note:'Campo desabilitado com preenchimento neutro dessaturado e baixo contraste de contorno.'},
  UI_TAB_NORMAL:{family:'tab',state:'normal',radius:10,fill:mix(p.ivory,p.lavender,.2),stroke:mix(p.ivory,p.lavender,.5),border:2,note:'Aba normal lavanda clara, como abas não selecionadas da DRE; sem texto.'},
  UI_TAB_SELECTED:{family:'tab',state:'selected',radius:10,fill:p.violet,stroke:activeLine,border:2,note:'Aba selecionada violeta, coerente com o botão primário ART-04 e a seleção dos concepts.'},
  UI_TAB_HOVER:{family:'tab',state:'hover',radius:10,fill:mix(p.ivory,p.lavender,.36),stroke:mix(p.ivory,p.violet,.55),border:2,note:'Hover realça o tom lavanda e o contorno, sem deslocar ou aumentar a aba.'},
  UI_TAB_PRESSED:{family:'tab',state:'pressed',radius:10,fill:mix(p.violet,p.plum,.24),stroke:mix(p.violet,p.plum,.5),border:2,note:'Aba pressionada violeta mais profundo, em harmonia com o pressed do ART-04.'},
  UI_TAB_DISABLED:{family:'tab',state:'disabled',radius:10,fill:muted,stroke:mutedLine,border:2,note:'Aba desabilitada neutra e dessaturada; geometria igual aos demais estados.'},
  UI_RADIO_OFF:{family:'radio',state:'off',shape:'circle',radius:30,fill:'#FFFFFF',stroke:neutralLine,border:4,note:'Base circular clara não selecionada; contorno legível a 32×32. Sem marcador ou ícone incorporado.'},
  UI_RADIO_ON:{family:'radio',state:'on',shape:'circle',radius:30,fill:p.violet,stroke:activeLine,border:4,note:'Base circular selecionada violeta, sem ponto, check ou outro ícone incorporado. Marcadores permanecem em camada independente.'},
  UI_RADIO_DISABLED:{family:'radio',state:'disabled',shape:'circle',radius:30,fill:muted,stroke:mutedLine,border:4,note:'Base circular desabilitada neutra, com mesma silhueta do radio off/on.'},
  UI_CHECKBOX_OFF:{family:'checkbox',state:'off',radius:10,inset:2,fill:'#FFFFFF',stroke:neutralLine,border:4,note:'Base quadrada arredondada clara, não selecionada; sem símbolo incorporado.'},
  UI_CHECKBOX_ON:{family:'checkbox',state:'on',radius:10,inset:2,fill:p.violet,stroke:activeLine,border:4,note:'Base selecionada violeta, sem check incorporado. ICON_CHECK é asset independente previsto no ART-08.'},
  UI_CHECKBOX_DISABLED:{family:'checkbox',state:'disabled',radius:10,inset:2,fill:muted,stroke:mutedLine,border:4,note:'Base quadrada desabilitada neutra; mesma silhueta do checkbox off/on.'},
  UI_TOGGLE_OFF:{family:'toggle',state:'off',radius:18,fill:mix(p.ivory,p.lavender,.2),stroke:neutralLine,border:2,note:'Apenas trilho/fundo do toggle desligado. Puxador independente: reutilizar UI_SLIDER_THUMB na montagem, alinhado à esquerda.'},
  UI_TOGGLE_ON:{family:'toggle',state:'on',radius:18,fill:p.violet,stroke:activeLine,border:2,note:'Apenas trilho/fundo do toggle ligado. Puxador independente: reutilizar UI_SLIDER_THUMB na montagem, alinhado à direita.'},
  UI_TOGGLE_DISABLED:{family:'toggle',state:'disabled',radius:18,fill:muted,stroke:mutedLine,border:2,note:'Apenas trilho/fundo desabilitado neutro; puxador independente e posição controlada pela UI.'},
  UI_SLIDER_TRACK:{family:'slider',part:'track',radius:5,fill:mix(p.ivory,p.lavender,.26),stroke:mix(p.ivory,p.ink,.2),border:1,note:'Trilho horizontal independente, raio contido nas bordas 6 px; área central plana para 9-slice até 680×16.'},
  UI_SLIDER_FILL:{family:'slider',part:'fill',radius:5,fill:p.violet,stroke:activeLine,border:1,note:'Preenchimento violeta independente, mesma silhueta do trilho; nenhum percentual ou puxador incorporado.'},
  UI_SLIDER_THUMB:{family:'slider',part:'thumb',shape:'circle',radius:30,fill:'#FFFFFF',stroke:p.violet,border:3,note:'Puxador circular branco com contorno violeta; escala uniforme para 40×40, sem sombra. Reutilizável como puxador dos toggles.'},
  UI_STEPPER_BASE:{family:'stepper',part:'base',radius:8,fill:'#FFFFFF',stroke:neutralLine,border:2,note:'Base vazia do controle incremental, sem quantidade, sinais, divisórias ou botões incorporados; sinais plus/minus pertencem ao ART-08.'},
  UI_SCROLLBAR_TRACK:{family:'scrollbar',part:'track',radius:2,inset:1,fill:mix(p.ivory,p.lavender,.18),stroke:mutedLine,border:1,note:'Trilho vertical independente, margem externa transparente de 1 px e cantos contidos nas bordas 4 px para ampliar até 20×640 sem emendas.'},
  UI_SCROLLBAR_THUMB:{family:'scrollbar',part:'thumb',radius:1,inset:1,fill:p.violet,stroke:activeLine,border:1,note:'Puxador vertical independente em violeta, margem externa transparente de 1 px e cantos contidos nas bordas 3 px para ampliar até 16×140.'}
};
const refs=[
  'art-source/references/concepts/01-hub.png','art-source/references/concepts/02-abertura.png',
  'art-source/references/concepts/03-gestao.png','art-source/references/concepts/04-dre-mensal.png',
  'Assets/Art/new/shared/components/buttons/primary_violet_normal.png',
  'Assets/Art/new/shared/components/buttons/secondary_ivory_normal.png',
  'Assets/Art/new/shared/components/panels/panel_lavender.png'
];
function body(d,w,h){
  const b=d.border,s=d.inset??0;
  let clip,line;
  if(d.shape==='circle'){
    clip=`<circle cx="${w/2}" cy="${h/2}" r="${d.radius}"/>`;
    line=`<circle cx="${w/2}" cy="${h/2}" r="${d.radius-b/2}" fill="none" stroke="${d.stroke}" stroke-width="${b}"/>`;
  }else{
    clip=`<rect x="${s}" y="${s}" width="${w-2*s}" height="${h-2*s}" rx="${d.radius}"/>`;
    line=`<rect x="${s+b/2}" y="${s+b/2}" width="${w-2*s-b}" height="${h-2*s-b}" rx="${d.radius-b/2}" fill="none" stroke="${d.stroke}" stroke-width="${b}"/>`;
  }
  return `<defs><clipPath id="silhouette">${clip}</clipPath></defs><g clip-path="url(#silhouette)"><rect width="${w}" height="${h}" fill="${d.fill}"/>${line}</g>`;
}
async function main(){
  if(assets.length!==24||Object.keys(designs).length!==24)throw new Error('Unexpected ART-05 inventory');
  for(const a of assets){
    if(a.production_method!=='vector_or_code'||!designs[a.id])throw new Error(`Review contract: ${a.id}`);
    for(const target of [`${manifest.asset_root}/${a.path}`,a.source_editable])if(fs.existsSync(path.join(root,target)))throw new Error(`Existing asset/source: ${target}`);
  }
  for(const a of manifest.assets.filter(a=>['ART-03','ART-04'].includes(a.task_id))){
    if(a.status!=='validado'||fileHash(`${manifest.asset_root}/${a.path}`)!==a.sha256)throw new Error(`Dependency changed: ${a.id}`);
  }
  write('docs/art/qa/ART-05-scope-baseline.json',{
    task_id:'ART-05',date:'2026-10-14',
    other_asset_records_sha256:sha(JSON.stringify(manifest.assets.filter(a=>a.task_id!=='ART-05'))),
    other_task_records_sha256:sha(JSON.stringify(manifest.tasks.filter(t=>t.id!=='ART-05'))),
    protected_files:Object.fromEntries([
      'docs/art/characters.json','docs/art/tokens.json','docs/art/catalogos.json',
      ...manifest.assets.filter(a=>a.status==='validado').map(a=>`${manifest.asset_root}/${a.path}`)
    ].map(p=>[p,fileHash(p)]))
  });
  const records=[];
  for(const a of assets){
    const d=designs[a.id],[w,h]=a.export_size_px;
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body(d,w,h)}</svg>\n`;
    const source=path.join(root,a.source_editable),output=path.join(root,manifest.asset_root,a.path);
    fs.mkdirSync(path.dirname(source),{recursive:true});fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(source,svg,'utf8');
    await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(output);
    const meta=await sharp(output).metadata();
    if(meta.width!==w||meta.height!==h||meta.channels!==4)throw new Error(`Invalid PNG: ${a.id}`);
    const prompt=[
      `Produzir somente ${path.basename(a.path,'.png')}, base isolada de controle UI Cogna.`,d.note,
      `Preenchimento uniforme ${d.fill}; contorno interno ${d.stroke} de ${d.border} px; ${d.shape==='circle'?'raio circular':'raio externo dos cantos'} ${d.radius} px${d.inset?`; margem externa ${d.inset} px`:''}.`,
      `PNG RGBA 8-bit sRGB ${w}×${h} px; display ${a.display_size_px.join('×')} px; fit ${a.display_fit}; bordas l,b,r,t [${a.nine_slice_lbrt_px.join(', ')}]; pivot [0.5, 0.5].`,
      'Preservar alfa, bounds e pivot nos estados da mesma família. Centro opaco e cantos/área externa com alfa verdadeiro. Fontes editáveis SVG, sem bitmap incorporado.',
      'Sem texto, labels, números, valores, ícones, check, ponto de seleção, sinais, puxador incorporado à base, logos, sombras, halos ou quadriculado. Puxadores solicitados são arquivos independentes.',
      'Concepts aprovados e ART-03/04 definem acabamento e paleta; estados sem referência explícita são derivados dos tokens.'
    ].join('\n');
    const record={id:a.id,family:d.family,state:d.state??null,part:d.part??null,
      output_path:`${manifest.asset_root}/${a.path}`,source_editable:a.source_editable,source_sha256:fileHash(a.source_editable),
      origin:'Geometria SVG determinística; manifesto/tokens e concepts aprovados, coerentes com ART-03/04.',
      references:refs.map(p=>({path:p,sha256:fileHash(p)})),final_prompt:prompt,
      tool:'deterministic SVG + sharp/librsvg',model:null,actual_size_px:[w,h],mode:'RGBA',color_space:'sRGB',
      sha256:fileHash(`${manifest.asset_root}/${a.path}`),design:d,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,status:'gerado'};
    records.push(record);
    Object.assign(a,{status:'gerado',source:'SVG editável; rasterização determinística sharp/librsvg',references:[...refs],
      sha256:record.sha256,actual_size_px:[w,h],generation_prompt:prompt,generation_tool:record.tool,
      generation_model:null,generation_model_note:'Lote geométrico; nenhum modelo generativo necessário.',production_record:'docs/art/prompts/ART-05.json'});
  }
  write('docs/art/prompts/ART-05.json',{task_id:'ART-05',date:'2026-10-14',status:'gerado',
    scope:'Somente os 24 controles, campos e bases de seleção do ART-05, sem ícones ou texto incorporados.',
    tool:'SVG editável e rasterização determinística sharp/librsvg',model:null,model_note:'Nenhum modelo generativo necessário para componentes geométricos exatos.',
    producer_source:'art-source/editable/ART-05/build-art05.cjs',renderer_versions:sharp.versions,palette:p,assets:records,
    assembly_notes:{
      selection:'Radio e checkbox on usam base violeta sem marcador incorporado; ícones permanecem independentes. ICON_CHECK pertence ao ART-08 e não foi produzido neste lote.',
      toggle:{base_asset_ids:['UI_TOGGLE_OFF','UI_TOGGLE_ON','UI_TOGGLE_DISABLED'],thumb_asset_id:'UI_SLIDER_THUMB',
        base_display_px:[180,52],thumb_display_px:[40,40],off_thumb_center_px:[26,26],on_thumb_center_px:[154,26],disabled:'Posição definida pelo estado lógico; mesmos bounds, puxador em camada independente.'},
      slider:{track_asset_id:'UI_SLIDER_TRACK',fill_asset_id:'UI_SLIDER_FILL',thumb_asset_id:'UI_SLIDER_THUMB',
        track_display_px:[680,16],thumb_display_px:[40,40],thumb_center_px:'[680 * valor_normalizado, 8], relativo ao trilho; prever 20 px de espaço de cada lado na montagem.',
        fill:'Largura dinâmica; ocultar quando zero. Não rasterizar valor ou posição.'},
      stepper:'Somente base vazia; quantidades e layout nativos. ICON_PLUS e ICON_MINUS pertencem ao ART-08.',
      scrollbar:'Trilho e puxador em arquivos independentes, centralizados no eixo horizontal; posição e altura do puxador são dinâmicas.'
    }});
  manifest.tasks.find(t=>t.id==='ART-05').status='gerado';write('docs/art/manifest.json',manifest);
  const tasks=read('docs/art/tasks.json');tasks.find(t=>t.id==='ART-05').status='gerado';write('docs/art/tasks.json',tasks);
  console.log(JSON.stringify({task_id:'ART-05',status:'gerado',png_count:24,svg_count:24}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
