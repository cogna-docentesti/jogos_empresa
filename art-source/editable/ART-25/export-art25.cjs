// Technical export and QA compositing only. All illustration and alpha come from ImageGen.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),tid='ART-25',previewRoot='art-source/previews/'+tid;
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n');};
const png={palette:false,compressionLevel:9,adaptiveFiltering:true},colors=['#F5F0E8','#24113D'];
const label=(s,w,color='#30243C',size=18)=>Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="42"><text x="16" y="28" font-family="Segoe UI,Arial,sans-serif" font-size="${size}" font-weight="600" fill="${color}">${s.replace(/&/g,'&amp;').replace(/</g,'&lt;')}</text></svg>`);
function measure(data,info){
 const hist=Array(256).fill(0),bounds={1:[info.width,info.height,-1,-1],128:[info.width,info.height,-1,-1]};let edge=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];hist[a]++;if(!x||!y||x===info.width-1||y===info.height-1)edge=Math.max(edge,a);
  for(const threshold of [1,128])if(a>=threshold){const b=bounds[threshold];b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x+1);b[3]=Math.max(b[3],y+1);}
 }
 const box=t=>bounds[t][2]<0?null:bounds[t],n=info.width*info.height;
 return {alpha_extrema:[hist.findIndex(v=>v),255-[...hist].reverse().findIndex(v=>v)],transparent_fraction:hist[0]/n,fully_opaque_fraction:hist[255]/n,alpha_250_or_more_fraction:hist.slice(250).reduce((a,b)=>a+b,0)/n,semitransparent_fraction:hist.slice(1,255).reduce((a,b)=>a+b,0)/n,dominant_nonzero_alpha:hist.map((count,alpha)=>({alpha,count})).slice(1).sort((a,b)=>b.count-a.count).slice(0,4),alpha_bbox_ltrb:box(1),visible_bbox_alpha128_ltrb:box(128),outer_edge_alpha_max:edge};
}
async function render(r,w,h,fit='contain'){
 return sharp(path.join(root,r.output_path)).resize({width:w,height:h,fit,position:'centre',background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer();
}
async function pair(r,native){
 const [w,h]=native?r.actual_size_px:r.display_size_px,cellw=w+32,cellh=h+64,layers=[];
 const sprite=await render(r,w,h,native?'contain':r.display_fit);
 for(let i=0;i<2;i++){
  layers.push({input:{create:{width:cellw,height:cellh,channels:4,background:colors[i]}},left:i*cellw,top:0});
  layers.push({input:label(r.short_name+' · '+w+'×'+h,cellw,i?colors[0]:'#30243C',w<200?10:18),left:i*cellw,top:0});
  layers.push({input:sprite,left:i*cellw+16,top:48});
 }
 const dest=previewRoot+'/'+r.id+(native?'-native-light-dark':'-display-light-dark')+'.png';
 await sharp({create:{width:cellw*2,height:cellh,channels:4,background:colors[0]}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function contact(records){
 const width=1560,height=1390,layers=[];
 layers.push({input:label('ART-25 | Financeiro e quatro opções de crédito',width,'#30243C',26),left:0,top:4});
 layers.push({input:label('6 PNGs separados | Cenário opaco + 5 recortes RGBA | Rótulos e fundos apenas no QA',width,'#30243C',18),left:0,top:44});
 const scene=records[0],person=records[1];
 for(let col=0;col<3;col++){
  const x=col*520,y=100,w=520,h=900;
  layers.push({input:{create:{width:w,height:h,channels:4,background:col===2?colors[1]:colors[0]}},left:x,top:y});
  layers.push({input:label(col===0?'Ambiente · 480×840 · cover':'CH01 · 420×760 · contain',w,col===2?colors[0]:'#30243C'),left:x,top:y});
  layers.push({input:await render(col===0?scene:person,col===0?480:420,col===0?840:760,col===0?'cover':'contain'),left:x+(col===0?20:50),top:y+46+(col===0?0:40)});
 }
 for(let row=0;row<2;row++)for(let i=0;i<4;i++){
  const r=records[i+2],x=i*390,y=1008+row*185;
  layers.push({input:{create:{width:390,height:185,channels:4,background:colors[row]}},left:x,top:y});
  layers.push({input:label(r.short_name+' · 120×120',390,row?colors[0]:'#30243C',16),left:x,top:y});
  layers.push({input:await render(r,120,120),left:x+135,top:y+48});
 }
 const dest=previewRoot+'/ART-25-contact-sheet.png';
 await sharp({create:{width,height,channels:4,background:'#E7DFED'}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function identity(person){
 const width=1040,height=870,layers=[];
 layers.push({input:label('CH01 | Master canônico e pose bancária · comparação de identidade',width,'#30243C',21),left:0,top:0});
 const files=['Assets/Art/new/characters/CH01/ch01_canonical.png',person.output_path];
 for(let i=0;i<2;i++){
  layers.push({input:label(i?'ART-25 · mão aberta e prancheta':'ART-01 · master canônico',520),left:i*520,top:44});
  layers.push({input:await sharp(path.join(root,files[i])).resize({width:480,height:720,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer(),left:i*520+20,top:90});
 }
 const dest=previewRoot+'/ART-25-identity-comparison.png';
 await sharp({create:{width,height,channels:4,background:colors[0]}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function composition(scene,person){
 // QA proof only: no scene, prefab, UI hierarchy or game code is touched.
 const w=480,h=840,bg=await render(scene,w,h,'cover'),actor=await render(person,420,760,'contain');
 const dest=previewRoot+'/ART-25-layer-proof.png';
 // Place the visible shoes on the floor at y~810; only transparent frame padding extends outside the viewport.
 await sharp(bg).composite([{input:actor,left:30,top:190}]).png(png).toFile(path.join(root,dest));return dest;
}
async function main(){
 const m=read('docs/art/manifest.json'),g=read('art-source/editable/ART-25/generations.json'),hints=read('art-source/editable/ART-25/tool-output-hints.json'),index=read('docs/art/tasks.json'),records=[];
 if(g.assets.length!==6)throw Error('Only six ART-25 assets authorized');
 fs.mkdirSync(path.join(root,previewRoot),{recursive:true});
 for(const req of g.assets){
  const a=m.assets.find(a=>a.id===req.id);if(!a||a.task_id!==tid||a.status!=='planejado')throw Error('Wrong task/status');
  const out=m.asset_root+'/'+a.path,target=path.join(root,out),src=path.join(root,req.source_path),meta=await sharp(src).metadata();
  if(fs.existsSync(target))throw Error('Preserve existing art: '+out);
  const [w,h]=a.export_size_px;let scale=1,l=0,t=0,r=0,b=0,scaledW=meta.width,scaledH=meta.height;
  fs.mkdirSync(path.dirname(target),{recursive:true});
  if(a.alpha_policy==='rgba_opaco'){
   if(meta.width!==w||meta.height!==h)throw Error('Scene size must match without deformation');
   await sharp(src).ensureAlpha().toColourspace('srgb').withIccProfile('srgb').png(png).toFile(target);
  }else{
   if(!meta.hasAlpha)throw Error('Original ImageGen alpha required');
   const box=w===512?[408,408]:[816,1224];
   const scaled=await sharp(src).resize({width:box[0],height:box[1],fit:'inside',kernel:'lanczos3'}).raw().toBuffer({resolveWithObject:true});
   scaledW=scaled.info.width;scaledH=scaled.info.height;scale=scaledW/meta.width;
   l=Math.floor((w-scaledW)/2);r=w-scaledW-l;t=Math.floor((h-scaledH)/2);b=h-scaledH-t;
   await sharp(scaled.data,{raw:scaled.info}).extend({left:l,right:r,top:t,bottom:b,background:{r:0,g:0,b:0,alpha:0}}).toColourspace('srgb').withIccProfile('srgb').png(png).toFile(target);
  }
  const raw=await sharp(target).raw().toBuffer({resolveWithObject:true}),metrics=measure(raw.data,raw.info);
  if(raw.info.width!==w||raw.info.height!==h||raw.info.channels!==4)throw Error('Invalid dimensions/mode');
  if(a.alpha_policy==='rgba_opaco'&&metrics.fully_opaque_fraction!==1)throw Error('Scene must be opaque');
  if(a.alpha_policy==='recorte'&&(metrics.outer_edge_alpha_max!==0||metrics.transparent_fraction===0))throw Error('Invalid cutout alpha');
  const references=[...new Set([...req.references,...a.references])],record={...req,references,tool:g.tool,model:null,model_note:'Specific model identifier not exposed by integrated tool.',production_method:'imagegen',tool_output_hint:hints[a.id],source_sha256:sha(req.source_path),source_size_px:[meta.width,meta.height],source_mode:meta.hasAlpha?'RGBA':'RGB',reference_sha256:Object.fromEntries(references.map(p=>[p,sha(p)])),output_path:out,path:a.path,actual_size_px:[w,h],mode:'RGBA',color_space:'sRGB',output_bytes:fs.statSync(target).size,sha256:sha(out),source_editable:null,producer_source:'art-source/editable/ART-25/export-art25.cjs',technical_export:{tool:'sharp/libvips',operation:a.alpha_policy==='rgba_opaco'?'Add opaque alpha channel to full-bleed RGB source; no resizing or painting.':'Uniform reduction of the complete ImageGen frame and transparent padding; no crop, alpha cleanup, masks, painting, recolor or axis stretching.',source_frame_px:[meta.width,meta.height],resampled_frame_px:[scaledW,scaledH],uniform_scale:scale,transparent_padding_ltrb_px:[l,t,r,b]},alpha_policy:a.alpha_policy,display_size_px:a.display_size_px,display_fit:a.display_fit,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,measurements:metrics,status:'gerado'};
  if(a.id==='CH01_BANK'){record.canonical_identity={path:'Assets/Art/new/characters/CH01/ch01_canonical.png',sha256:sha('Assets/Art/new/characters/CH01/ch01_canonical.png')};}
  record.native_preview=await pair(record,true);record.display_preview=await pair(record,false);records.push(record);
  Object.assign(a,{status:'gerado',sha256:record.sha256,actual_size_px:[w,h],generation_prompt:req.prompt,production_method:'imagegen',generation_tool:g.tool,generation_model:null,source_image:req.source_path,source_image_sha256:record.source_sha256,source_editable:null,references,production_record:'docs/art/prompts/ART-25.json',qa:{technical:false,visual:false,identity:false}});
 }
 const sheet=await contact(records),identityPreview=await identity(records[1]),layerProof=await composition(records[0],records[1]);
 Object.assign(m.tasks.find(t=>t.id===tid),{status:'gerado',production_record:'docs/art/prompts/ART-25.json',delivery_preview:sheet});
 Object.assign(index.find(t=>t.id===tid),{status:'gerado',production_record:'docs/art/prompts/ART-25.json',delivery_preview:sheet});
 write('docs/art/prompts/ART-25.json',{task_id:tid,date:'2026-10-15',status:'gerado',tool:g.tool,model:null,model_note:'Specific model identifier not exposed by integrated tool.',scope_resolution:g.scope_resolution,producer_source:'art-source/editable/ART-25/export-art25.cjs',assets:records,delivery_preview:sheet,identity_preview:identityPreview,layer_proof:layerProof});
 write('docs/art/qa/ART-25-measurements.json',{task_id:tid,assets:records.map(r=>({id:r.id,actual_size_px:r.actual_size_px,source_size_px:r.source_size_px,sha256:r.sha256,measurements:r.measurements,technical_export:r.technical_export}))});
 write('docs/art/manifest.json',m);write('docs/art/tasks.json',index);
 console.log(JSON.stringify({task:tid,status:'gerado',assets:records.map(r=>({id:r.id,source_size_px:r.source_size_px,actual_size_px:r.actual_size_px,alpha_extrema:r.measurements.alpha_extrema,transparent_fraction:r.measurements.transparent_fraction,visible_bbox:r.measurements.visible_bbox_alpha128_ltrb})),contact_sheet:sheet,identity_preview:identityPreview,layer_proof:layerProof}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
