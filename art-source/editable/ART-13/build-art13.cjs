// ART-13 geometric artwork: editable SVG and exact RGBA PNG; no gameplay/UI integration.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const sha=p=>hash(fs.readFileSync(path.join(root,p)));
const write=(p,d)=>{const f=path.join(root,p);fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,JSON.stringify(d,null,2)+'\n','utf8');};
const mix=(a,b,t)=>'#'+[1,3,5].map(i=>Math.round(parseInt(a.slice(i,i+2),16)*(1-t)+parseInt(b.slice(i,i+2),16)*t).toString(16).padStart(2,'0')).join('');
async function main(){
 const m=read('docs/art/manifest.json'),layout=read('docs/art/layouts/map-layout.json'),baseline=read('docs/art/qa/ART-13-scope-baseline.json');
 if(sha('docs/art/layouts/map-layout.json')!==baseline.original_layout_sha256)throw Error('Contract changed since baseline.');
 const p=read('docs/art/tokens.json').palette,assets=m.assets.filter(a=>a.task_id==='ART-13');
 if(assets.length!==13)throw Error('Wrong ART-13 inventory.');
 const palette={normal:{fill:p.ivory,line:mix(p.plum,p.lavender,.65),pinLine:p.violet,icon:p.plum},
  selected:{fill:p.violet,line:mix(p.violet,p.lavender,.45),pinLine:p.ivory,icon:'#FFFFFF'},
  disabled:{fill:mix(p.ivory,p.ink,.07),line:mix(p.ivory,p.ink,.13),pinLine:mix(p.ivory,p.ink,.25),icon:mix(p.ivory,p.ink,.4)}};
 const commonRefs=['art-source/references/concepts/01-hub.png','art-source/references/concepts/02-abertura.png',
  'docs/art/tokens.json','docs/art/layouts/archive/map-layout-before-art13.json',
  'Assets/Art/new/shared/components/controls/tab_selected.png','Assets/Art/new/shared/icons/icon_bank.png'];
 const records=[],layers=[];
 for(const a of assets){
  let width,height,viewBox,body,source=a.source_editable,position=null,design;
  if(a.kind==='overlay'){
   const central=a.id==='OVERLAY_CENTER',zone=central?layout.central_site:layout.zones.find(z=>z.catalog_id===a.catalog_id);
   if(!zone)throw Error('Missing polygon '+a.id);
   const poly=zone.polygon_px,minX=Math.min(...poly.map(p=>p[0])),minY=Math.min(...poly.map(p=>p[1]));
   const maxX=Math.max(...poly.map(p=>p[0])),maxY=Math.max(...poly.map(p=>p[1]));
   position=[minX-2,minY-2];width=maxX-minX+4;height=maxY-minY+4;viewBox=[...position,width,height];
   const points=poly.map(p=>p.join(',')).join(' '),fillOpacity=central?.09:.12,lineOpacity=central?.5:.64,stroke=central?6:8;
   body=`<defs><clipPath id="region"><polygon points="${points}"/></clipPath></defs><g clip-path="url(#region)"><polygon points="${points}" fill="${p.violet}" fill-opacity="${fillOpacity}"/><polygon points="${points}" fill="none" stroke="${p.violet}" stroke-opacity="${lineOpacity}" stroke-width="${stroke}" stroke-linejoin="round"/></g>`;
   source='art-source/editable/location/overlays/'+path.basename(a.path,'.png')+'.svg';
   design={family:'overlay',polygon_master_px:poly,fill:p.violet,fill_opacity:fillOpacity,stroke_opacity:lineOpacity,stroke_px:stroke,
    visible_inner_border_px:stroke/2,clip:'Exact authoritative polygon; anti-aliased pixel coverage only at boundary.',transparent_padding_px:2,
    origin:'Polígono canônico do mapa; realce violeta translúcido e contorno interno sem bloom, gradiente ou sombra.'};
  }else{
   [width,height]=a.export_size_px;viewBox=[0,0,width,height];
   const state=a.id.endsWith('_SELECTED')?'selected':a.id.endsWith('_DISABLED')?'disabled':'normal',c=palette[state];
   if(a.id.startsWith('UI_PIN_')){
    const d='M128 20C74 20 32 62 32 112C32 166 78 211 128 238C178 211 224 166 224 112C224 62 182 20 128 20Z';
    body=`<defs><clipPath id="shape"><path d="${d}"/></clipPath></defs><g clip-path="url(#shape)"><rect width="256" height="256" fill="${c.fill}"/><path d="${d}" fill="none" stroke="${c.pinLine}" stroke-width="16" stroke-linejoin="round"/></g>`;
    design={family:'pin',state,fill:c.fill,stroke:c.pinLine,inner_border_px:8,tip_local_px:[128,238],
     icon_slot_px:[80,58,96,96],icon_tint:c.icon,origin:'Pino de localização com silhueta estável, centro vazio para ícone ART-06 independente, sem sombra.'};
   }else if(a.id==='UI_MAP_LABEL_TIP'){
    body='<path d="M8 10H56L32 54Z" fill="#FFFFFF"/>';
    design={family:'label_tip',fill:'#FFFFFF',tintable:true,tip_local_px:[32,54],
     state_tints:Object.fromEntries(Object.entries(palette).map(([k,v])=>[k,v.fill])),
     origin:'Ponteira separada branca tintável, sem contorno para encaixar sob a base e assumir a cor de cada estado.'};
   }else{
    body=`<defs><clipPath id="shape"><rect x="4" y="4" width="248" height="120" rx="20"/></clipPath></defs><g clip-path="url(#shape)"><rect width="256" height="128" fill="${c.fill}"/><rect x="6" y="6" width="244" height="116" rx="18" fill="none" stroke="${c.line}" stroke-width="4"/></g>`;
    design={family:'label',state,fill:c.fill,stroke:c.line,inner_border_px:4,radius_px:20,transparent_inset_px:4,
     safe_content_rect_px:[24,24,208,80],display_safe_content_rect_px:[24,24,212,28],text_icon_tint:c.icon,
     origin:'Rótulo vazio com 9-slice de 24 px; cantos estáveis, área central plana e fundo/contorno independentes do conteúdo.'};
   }
  }
  for(const file of [m.asset_root+'/'+a.path,source])if(fs.existsSync(path.join(root,file)))throw Error('Preserve existing file '+file);
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="${viewBox.join(' ')}">${body}</svg>\n`;
  const target=path.join(root,m.asset_root,a.path);fs.mkdirSync(path.dirname(target),{recursive:true});fs.mkdirSync(path.dirname(path.join(root,source)),{recursive:true});
  fs.writeFileSync(path.join(root,source),svg,'utf8');
  await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb')
   .png({palette:false,compressionLevel:9,adaptiveFiltering:true}).toFile(target);
  const meta=await sharp(target).metadata();if(meta.width!==width||meta.height!==height||meta.channels!==4)throw Error('Unexpected PNG geometry.');
  const prompt=[`Produzir somente ${a.id}, ${a.name}.`,design.origin,
   `Geometria e cores exatas: ${JSON.stringify(design)}.`,
   `SVG editável e PNG RGBA sRGB ${width}x${height} px, pivot [${a.pivot.join(',')}], bordas [${a.nine_slice_lbrt_px.join(',')}].`,
   position?`Posição top-left [${position.join(',')}] no master 1920x1080; sem reescala do polígono; preservar transformação do mapa.`:`Estados da mesma família preservam bounds, pivot, máscara alfa e escala; display nominal ${a.display_size_px.join('x')} px.`,
   'Sem texto, labels, números, logos, ícones incorporados, construção, pessoas, sombra, bloom, halo neon ou checkerboard. Ícones e conteúdo são camadas independentes.',
   'Derivar paleta dos tokens e dos concepts aprovados; normal marfim, selecionado violeta, desabilitado neutro. Nenhuma implementação de UI ou alteração de cena.'].join('\n');
  const record={id:a.id,path:a.path,output_path:m.asset_root+'/'+a.path,source_editable:source,source_sha256:sha(source),
   production_method:'vector_or_code',tool:'SVG editable geometry + sharp/libvips rasterizer',model:null,model_note:'Produção vetorial determinística; não houve geração por modelo de imagem.',
   prompt,design,actual_size_px:[width,height],sha256:sha(m.asset_root+'/'+a.path),output_bytes:fs.statSync(target).size,
   references:commonRefs.map(ref=>({path:ref,sha256:sha(ref)})),catalog_id:a.catalog_id,catalog_source:a.source,
   color_space:'sRGB',mode:'RGBA',pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,position_px:position,status:'gerado'};
  if(position){record.crop_rect_px=[...position,width,height];record.display_size_px=[width*2/3,height*2/3];record.scale=1;
   const layer=layout.layers.find(l=>l.asset_id===a.id);Object.assign(layer,{position_px:position,texture_size_px:[width,height],texture_storage:'cropped',
    crop_rect_px:record.crop_rect_px,asset_path:record.output_path,status:'gerado'});layers.push({...layer});}
  a.export_size_px=[width,height];a.actual_size_px=[width,height];a.source_editable=source;a.production_method='vector_or_code';
  a.generation_prompt=prompt;a.status='gerado';a.sha256=record.sha256;a.production_record='docs/art/prompts/ART-13.json';a.export_source='art-source/editable/ART-13/build-art13.cjs';
  a.references=[...new Set([...a.references,...commonRefs])];a.qa={technical:false,visual:false,identity:false,record:'docs/art/qa/ART-13.json'};
  if(position){Object.assign(a,{display_size_px:record.display_size_px,texture_storage:'cropped',map_position_px:position,
   master_crop_rect_px:record.crop_rect_px,map_master_size_px:[1920,1080],coordinate_contract:'docs/art/layouts/map-layout.json',placement_record:'docs/art/layouts/map-selection-compact.json'});}
  else a.component_contract='docs/art/layouts/map-selection-compact.json';
  records.push(record);console.log(JSON.stringify({id:a.id,size:[width,height],bytes:record.output_bytes,position}));
 }
 const icons={bank:'ICON_BANK',university:'ICON_UNIVERSITY',store:'ICON_COMMERCE',condominium:'ICON_RESIDENTIAL',marketing:'ICON_CORPORATE'};
 const sidecar={schema_version:1,task_id:'ART-13',date:'2026-10-14',master_size_px:[1920,1080],coordinate_origin:'top_left_y_down',
  camera:layout.camera,display_transform:layout.display_transform,active_contract:'docs/art/layouts/map-layout.json',layers,assets:records,
  selection_rule:'Exibir somente o realce da localidade selecionada na ordem 10. O centro na ordem 11 é independente e não cria um sexto ID de Location.',
  overlay_placement:'PNG compacto com posição do canto superior esquerdo, pivot [0,1], escala 1. Aplicar a transformação compartilhada do mapa ao parent.',
  marker_states:palette,marker_order:100,icon_order:101,label_tip_order:102,label_order:103,native_content_order:104,
  marker_instance_proposal:{coordinate_space:'map1920x1080',pin_size_master_px:[80,80],label_size_master_px:[260,76],
   note:'Tamanhos desta prova são em pixels do parent master: na viewport comum tornam-se 53.333x53.333 e 173.333x50.667. Os tamanhos nominais de UI 80x80 e 260x76 também são revisados em preview isolado. Integração pode usar UI em canvas e converter todos os anchors pela mesma matriz.',
   pin_tip_local_px:[40,74.375],label_offset_from_pin_origin_master_px:[72,-2],icon_size_master_px:[30,30],tip_size_master_px:[20,20],
   nodes:layout.zones.map(z=>({catalog_id:z.catalog_id,anchor_master_px:z.label_anchor_px,icon_asset_id:icons[z.catalog_id],overlay_asset_id:'OVERLAY_'+z.catalog_id.toUpperCase(),
    pin_top_left_master_px:[z.label_anchor_px[0]-40,z.label_anchor_px[1]-74.375],
    label_top_left_master_px:[z.label_anchor_px[0]+32,z.label_anchor_px[1]-76.375]}))},
  components:{pin:{icon_slot_source_px:[80,58,96,96],shared_alpha_required:true},label:{nine_slice_lbrt_px:[24,24,24,24],display_px:[260,76],safe_content_display_px:[24,24,212,28],shared_alpha_required:true},
   tip:{source_white_tintable:true,color_rule:'Tint igual ao preenchimento do rótulo; desenhar sob a base, sobrepondo 2 px à borda para evitar emenda. Não criar novo sprite por estado.'}},
  integration_performed:false};
 layout.art13_revision={date:'2026-10-14',task_id:'ART-13',previous_contract:baseline.original_layout_path,previous_contract_sha256:baseline.original_layout_sha256,
  selection_contract:'docs/art/layouts/map-selection-compact.json',master_geometry_unchanged:true};
 layout.layer_notes.future_status='ART-11, ART-12 e realces ART-13 usam PNGs compactos com posições explícitas. Outros lotes preservam seu escopo.';
 layout.layer_notes.labels='Pinos, ponteiras, rótulos vazios, ícones ART-06 e texto nativo são camadas independentes. Proposta de montagem no map-selection-compact.json.';
 write('docs/art/layouts/map-layout.json',layout);sidecar.active_contract_sha256=sha('docs/art/layouts/map-layout.json');write('docs/art/layouts/map-selection-compact.json',sidecar);
 const note='Seis realces compactos dos polígonos e sete componentes de pino/rótulo em SVG e PNG; estados com geometria comum, sem conteúdo ou sombras embutidos.';
 for(const tasks of [m.tasks,read('docs/art/tasks.json')]){Object.assign(tasks.find(t=>t.id==='ART-13'),{status:'gerado',note,qa_record:'docs/art/qa/ART-13.json',delivery_record:'docs/art/ART-13-entregas.md'});if(tasks!==m.tasks)write('docs/art/tasks.json',tasks);}
 // Merge only ART-13 records through Python, retaining prior JSON numeric representations (e.g. 90.0).
 require('node:child_process').execFileSync('C:/Python310/python.exe',['-c',
  "import json,sys;from pathlib import Path;p=Path('docs/art/manifest.json');old=json.loads(p.read_text(encoding='utf-8'));updates=json.load(sys.stdin);assets={a['id']:a for a in updates['assets']};tasks={t['id']:t for t in updates['tasks']};old['assets']=[assets.get(a['id'],a) for a in old['assets']];old['tasks']=[tasks.get(t['id'],t) for t in old['tasks']];p.write_text(json.dumps(old,ensure_ascii=False,indent=2)+'\\n',encoding='utf-8')"],
  {cwd:root,input:JSON.stringify({assets:m.assets.filter(a=>a.task_id==='ART-13'),tasks:m.tasks.filter(t=>t.id==='ART-13')})});
 write('docs/art/prompts/ART-13.json',{task_id:'ART-13',date:'2026-10-14',status:'gerado',tool:'SVG + sharp',model:null,assets:records,
  renderer_versions:sharp.versions,active_contract_sha256:sidecar.active_contract_sha256,integration_performed:false});
 const lines=['ART-13 — Marcadores, realces e rótulos vazios do mapa','Escopo: núcleo','Pré-requisitos: ART-10, ART-11, ART-06','',note,
  'COMANDO PARA O CODEX','Use $cogna-artista para executar somente ART-13. Produzir SVGs editáveis e PNGs separados dos seis realces geométricos e dos sete componentes, conforme manifesto e concepts. Realces compactos com coordenadas exatas no master. Não implementar UI nem integrar cenas.','',
  'LEITURAS','docs/art/manifest.json, tokens.json, characters.json, sources.json','docs/art/layouts/map-layout.json','docs/art/layouts/map-selection-compact.json','art-source/references/concepts/01-hub.png e 02-abertura.png','',
  'ENTREGÁVEIS'];
 for(const r of records){lines.push(r.id+' | '+r.path,`Export ${r.actual_size_px.join('x')} px | alfa recorte | bordas [${r.nine_slice_lbrt_px.join(', ')}] | pivot [${r.pivot.join(', ')}]`,
  r.position_px?'Posição top-left no master: '+JSON.stringify(r.position_px)+' | escala 1':'Display nominal: '+assets.find(a=>a.id===r.id).display_size_px.join('x')+' px',r.design.origin,'SVG: '+r.source_editable,'');}
 lines.push('CRITÉRIOS','PNG RGBA sRGB com alfa real, sem textos, ícones, logos, construção, sombra ou bloom. Pinos e rótulos preservam máscara alfa, bounds e pivot entre estados. Ponteira branca é tintável e separada.',
  'Realces derivados dos polígonos canônicos, sem invade vias, com contorno interno; sem escala independente por eixo. Compartilhar transformação do mapa e preservar layers anteriores.',
  'Revisar sobre marfim/ameixa, no tamanho de exibição, em 9-slice e na montagem conjunta. Registrar prompts, fontes, hashes, medidas e QA; validar apenas após inspeção.',
  'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-13','python art-source/editable/qa/ART-13-preview.py');
 fs.writeFileSync(path.join(root,'docs/art/tasks/ART-13.txt'),lines.join('\n')+'\n','utf8');
 console.log(JSON.stringify({status:'gerado',assets:records.length,total_png_bytes:records.reduce((n,r)=>n+r.output_bytes,0)}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
