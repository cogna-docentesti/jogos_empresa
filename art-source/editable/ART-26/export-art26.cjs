// Technical PNG export and QA compositing. Artwork/alpha are supplied only by ImageGen.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp'),cp=require('node:child_process');
const root=path.resolve(__dirname,'../../..'),tid='ART-26',pr='art-source/previews/'+tid;
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n');};
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.isAbsolute(p)?p:path.join(root,p))).digest('hex');
const png={palette:false,compressionLevel:9,adaptiveFiltering:true},colors=['#F5F0E8','#24113D'];
const label=(s,w,color='#30243C',size=18)=>Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="'+w+'" height="42"><text x="12" y="28" font-family="Segoe UI,Arial,sans-serif" font-size="'+size+'" font-weight="600" fill="'+color+'">'+s.replace(/&/g,'&amp;').replace(/</g,'&lt;')+'</text></svg>');
function measure(data,info){
 const hist=Array(256).fill(0),bounds={1:[info.width,info.height,-1,-1],128:[info.width,info.height,-1,-1]};let edge=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];hist[a]++;if(!x||!y||x===info.width-1||y===info.height-1)edge=Math.max(edge,a);
  for(const t of [1,128])if(a>=t){const b=bounds[t];b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x+1);b[3]=Math.max(b[3],y+1);}
 }
 const box=t=>bounds[t][2]<0?null:bounds[t],n=info.width*info.height;
 return {alpha_extrema:[hist.findIndex(v=>v),255-[...hist].reverse().findIndex(v=>v)],transparent_fraction:hist[0]/n,fully_opaque_fraction:hist[255]/n,alpha_250_or_more_fraction:hist.slice(250).reduce((a,b)=>a+b,0)/n,semitransparent_fraction:hist.slice(1,255).reduce((a,b)=>a+b,0)/n,dominant_nonzero_alpha:hist.map((count,alpha)=>({alpha,count})).slice(1).sort((a,b)=>b.count-a.count).slice(0,4),alpha_bbox_ltrb:box(1),visible_bbox_alpha128_ltrb:box(128),outer_edge_alpha_max:edge};
}
async function render(r,w,h,fit='contain'){
 return sharp(path.join(root,r.output_path)).resize({width:w,height:h,fit,background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer();
}
async function pair(r,native){
 const [w,h]=native?r.actual_size_px:r.display_size_px,cw=w+32,ch=h+64,layers=[],sprite=await render(r,w,h);
 for(let i=0;i<2;i++){
  layers.push({input:{create:{width:cw,height:ch,channels:4,background:colors[i]}},left:i*cw,top:0});
  layers.push({input:label(r.short_name+' · '+w+'×'+h,cw,i?colors[0]:'#30243C'),left:i*cw,top:0});
  layers.push({input:sprite,left:i*cw+16,top:48});
 }
 const dest=pr+'/'+r.id+(native?'-native-light-dark':'-display-light-dark')+'.png';
 await sharp({create:{width:cw*2,height:ch,channels:4,background:colors[0]}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function contact(records){
 const width=1760,height=1200,layers=[];
 layers.push({input:label('ART-26 | Retratos de equipe e ambientes de trabalho',width,'#30243C',26),left:0,top:4});
 layers.push({input:label('10 PNGs separados | Retratos 240×240 · Ambientes 320×240 | Fundos e rótulos somente no QA',width,'#30243C',18),left:0,top:44});
 for(let i=0;i<5;i++){
  const x=i*352,r=records[i],bg=records[i+5];
  for(let j=0;j<2;j++){
   const y=100+j*305;
   layers.push({input:{create:{width:352,height:300,channels:4,background:colors[j]}},left:x,top:y});
   layers.push({input:label(r.short_name+' · '+r.character_id,352,j?colors[0]:'#30243C',18),left:x,top:y});
   layers.push({input:await render(r,240,240),left:x+56,top:y+42});
  }
  layers.push({input:label(bg.short_name+' · 320×240',352),left:x,top:718});
  layers.push({input:await render(bg,320,240),left:x+16,top:760});
 }
 const dest=pr+'/ART-26-contact-sheet.png';
 await sharp({create:{width,height:1020,channels:4,background:'#E7DFED'}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function identity(records){
 const width=1760,height=690,layers=[];
 layers.push({input:label('ART-26 | Comparação de identidade · master canônico e retrato',width,'#30243C',25),left:0,top:0});
 for(let i=0;i<5;i++){
  const r=records[i],x=i*352;
  layers.push({input:label(r.character_id+' · master',352),left:x,top:48});
  layers.push({input:await sharp(path.join(root,r.canonical_identity.path)).resize({width:240,height:360,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer(),left:x+56,top:88});
  layers.push({input:label('ART-26 · '+r.short_name,352),left:x,top:440});
  layers.push({input:await render(r,240,240),left:x+56,top:480});
 }
 const dest=pr+'/ART-26-identity-comparison.png';
 await sharp({create:{width,height:740,channels:4,background:colors[0]}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function composed(bg,r){
 // Preview only: uniform scale and transparent padding preserve the complete silhouette.
 const base=await render(bg,320,240),actor=await render(r,240,240);
 const top=240-Math.ceil(r.measurements.visible_bbox_alpha128_ltrb[3]*240/512);
 return sharp(base).composite([{input:actor,left:40,top}]).png(png).toBuffer();
}
function proofTransforms(records){
 return {preview_only:true,viewport_px:[320,240],background:{uniform_scale:1/3,crop_ltrb_px:[0,0,0,0]},portraits:records.filter(r=>r.character_id).map(r=>({asset_id:r.id,uniform_scale:240/512,frame_position_px:[40,240-Math.ceil(r.measurements.visible_bbox_alpha128_ltrb[3]*240/512)],note:'Recorte de meio corpo alinhado à base do viewport; somente o padding transparente excede o viewport.'}))};
}
async function layerProof(records){
 const width=1760,height=350,layers=[];
 layers.push({input:label('ART-26 | Prova de camadas independentes · 320×240 · somente preview',width,'#30243C',24),left:0,top:0});
 for(let i=0;i<5;i++){
  const r=records[i],bg=records[i+5],x=i*352;
  layers.push({input:label(r.short_name,352),left:x,top:44});
  layers.push({input:await composed(bg,r),left:x+16,top:90});
 }
 const dest=pr+'/ART-26-layer-proof.png';
 await sharp({create:{width,height,channels:4,background:colors[0]}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function main(){
 const m=read('docs/art/manifest.json'),index=read('docs/art/tasks.json'),g=read('art-source/editable/ART-26/generations.json'),outputs=read('art-source/editable/ART-26/tool-outputs.json'),revisions=read('art-source/editable/ART-26/revisions.json'),characters=read('docs/art/characters.json'),records=[];
 if(process.argv.includes('--previews-only')){
  const p=read('docs/art/prompts/ART-26.json'),bgIds=['TEAM_ENV_SERVICE_COUNTER','TEAM_ENV_GRILL_KITCHEN','TEAM_ENV_FRENCH_KITCHEN','TEAM_ENV_MANAGEMENT_OFFICE','TEAM_ENV_SUSHI_COUNTER'],ordered=[...p.assets.slice(0,5),...bgIds.map(id=>p.assets.find(r=>r.id===id))];
  p.delivery_preview=await contact(ordered);p.identity_preview=await identity(ordered);p.layer_proof=await layerProof(ordered);p.layer_proof_transform=proofTransforms(ordered);
  write('docs/art/prompts/ART-26.json',p);console.log(JSON.stringify({previews_updated:true}));return;
 }
 if(g.assets.length!==10)throw Error('Only ten ART-26 assets authorized');
 fs.mkdirSync(path.join(root,pr),{recursive:true});
 // Copy original results without modifying/deleting generated images.
 for(const req of g.assets){
  const result=outputs.find(x=>x.id===req.id),target=path.join(root,req.source_path);
  fs.mkdirSync(path.dirname(target),{recursive:true});
  if(fs.existsSync(target)&&sha(target)!==sha(result.generated_file))throw Error('Preserve original source: '+target);
  if(!fs.existsSync(target))fs.copyFileSync(result.generated_file,target);
 }
 for(const rev of revisions){
  const target=path.join(root,rev.source_path);
  if(fs.existsSync(target)&&sha(target)!==sha(rev.generated_file))throw Error('Preserve revision: '+target);
  if(!fs.existsSync(target))fs.copyFileSync(rev.generated_file,target);
 }
 for(const req of g.assets){
  const a=m.assets.find(a=>a.id===req.id),revision=revisions.find(r=>r.id===req.id&&r.selected),selection=revision||outputs.find(x=>x.id===req.id),sourcePath=revision?revision.source_path:req.source_path,src=path.join(root,sourcePath),meta=await sharp(src).metadata();
  if(!a||a.task_id!==tid||a.status!=='planejado')throw Error('Wrong task/status');
  const out=m.asset_root+'/'+a.path,target=path.join(root,out);if(fs.existsSync(target))throw Error('Preserve existing art: '+out);
  fs.mkdirSync(path.dirname(target),{recursive:true});
  const [w,h]=a.export_size_px;let sw=w,sh=h,l=0,t=0,r=0,b=0,scale=w/meta.width;
  if(a.alpha_policy==='rgba_opaco'){
   if(meta.width*3!==meta.height*4)throw Error('Environment must be 4:3 without deformation');
   await sharp(src).resize(w,h,{fit:'inside',kernel:'lanczos3'}).ensureAlpha().toColourspace('srgb').withIccProfile('srgb').png(png).toFile(target);
  }else{
   if(!meta.hasAlpha)throw Error('ImageGen alpha required');
   const scaled=await sharp(src).resize({width:408,height:408,fit:'inside',kernel:'lanczos3'}).raw().toBuffer({resolveWithObject:true});
   sw=scaled.info.width;sh=scaled.info.height;scale=sw/meta.width;l=Math.floor((w-sw)/2);r=w-sw-l;t=Math.floor((h-sh)/2);b=h-sh-t;
   await sharp(scaled.data,{raw:scaled.info}).extend({left:l,right:r,top:t,bottom:b,background:{r:0,g:0,b:0,alpha:0}}).toColourspace('srgb').withIccProfile('srgb').png(png).toFile(target);
  }
  const raw=await sharp(target).raw().toBuffer({resolveWithObject:true}),metrics=measure(raw.data,raw.info);
  if(raw.info.width!==w||raw.info.height!==h||raw.info.channels!==4)throw Error('Invalid dimensions/mode');
  if(a.alpha_policy==='rgba_opaco'&&metrics.fully_opaque_fraction!==1)throw Error('Opaque scene required');
  if(a.alpha_policy==='recorte'&&(metrics.outer_edge_alpha_max!==0||metrics.transparent_fraction===0))throw Error('Invalid cutout alpha');
  const refs=[...new Set([...req.references,...a.references,...(revision?revision.references:[])])];
  const record={...req,initial_prompt:req.prompt,prompt:revision?revision.prompt:req.prompt,references:refs,tool:g.tool,model:null,model_note:g.model_note,production_method:'imagegen',generated_file:selection.generated_file,tool_output_hint:selection.tool_output_hint,source_path:sourcePath,source_sha256:sha(sourcePath),source_size_px:[meta.width,meta.height],source_mode:meta.hasAlpha?'RGBA':'RGB',reference_sha256:Object.fromEntries(refs.map(p=>[p,sha(p)])),output_path:out,path:a.path,actual_size_px:[w,h],mode:'RGBA',color_space:'sRGB',output_bytes:fs.statSync(target).size,sha256:sha(out),source_editable:null,producer_source:'art-source/editable/ART-26/export-art26.cjs',technical_export:{tool:'sharp/libvips',operation:a.alpha_policy==='rgba_opaco'?'Uniform reduction of complete 4:3 source; add opaque alpha channel.':'Uniform reduction of complete ImageGen frame and transparent padding; original alpha preserved.',source_frame_px:[meta.width,meta.height],resampled_frame_px:[sw,sh],uniform_scale:scale,transparent_padding_ltrb_px:[l,t,r,b],crop_ltrb_px:[0,0,0,0],painting_or_alpha_cleanup:false},alpha_policy:a.alpha_policy,display_size_px:a.display_size_px,display_fit:a.display_fit,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,measurements:metrics,status:'gerado'};
  if(req.character_id){
   const ch=characters.find(c=>c.id===req.character_id),canonicalPath=m.asset_root+'/'+ch.canonical_asset;
   if(sha(canonicalPath)!==ch.identity_hash)throw Error('Canonical identity hash differs');
   record.canonical_identity={character_id:req.character_id,path:canonicalPath,sha256:sha(canonicalPath)};
  }
  if(revision)record.revisions=[{...revision,source_sha256:sha(revision.source_path),initial_source_path:req.source_path,initial_source_sha256:sha(req.source_path)}];
  record.native_preview=await pair(record,true);record.display_preview=await pair(record,false);records.push(record);
  Object.assign(a,{status:'gerado',sha256:record.sha256,actual_size_px:[w,h],generation_prompt:record.prompt,production_method:'imagegen',generation_tool:g.tool,generation_model:null,source_image:sourcePath,source_image_sha256:record.source_sha256,source_editable:null,references:refs,production_record:'docs/art/prompts/ART-26.json',qa:{technical:false,visual:false,identity:false}});
 }
 // Match canonical role order to associated environment, independent of manifest background order.
 const bgByRole=['TEAM_ENV_SERVICE_COUNTER','TEAM_ENV_GRILL_KITCHEN','TEAM_ENV_FRENCH_KITCHEN','TEAM_ENV_MANAGEMENT_OFFICE','TEAM_ENV_SUSHI_COUNTER'];
 const ordered=[...records.slice(0,5),...bgByRole.map(id=>records.find(r=>r.id===id))];
 const sheet=await contact(ordered),ident=await identity(ordered),layers=await layerProof(ordered);
 const prompts={task_id:tid,date:g.date,status:'gerado',tool:g.tool,model:null,model_note:g.model_note,scope_resolution:g.scope_resolution,distinct_asset_generations:10,revision_generations:revisions.length,producer_source:'art-source/editable/ART-26/export-art26.cjs',delivery_preview:sheet,identity_preview:ident,layer_proof:layers,layer_proof_transform:proofTransforms(ordered),assets:records,integration_performed:false};
 write('docs/art/prompts/ART-26.json',prompts);
 write('docs/art/qa/ART-26-measurements.json',{task_id:tid,assets:records.map(r=>({id:r.id,source_size_px:r.source_size_px,actual_size_px:r.actual_size_px,sha256:r.sha256,...r.measurements}))});
 Object.assign(m.tasks.find(t=>t.id===tid),{status:'gerado'});Object.assign(index.find(t=>t.id===tid),{status:'gerado'});
 write('docs/art/manifest.json',m);write('docs/art/tasks.json',index);
 for(const [flag,dest] of [['--task','ART-26-technical'],['--plan','ART-26-plan-validation']]){
  const args=['tools/art/validate_assets.py','--project',root,flag,...(flag==='--task'?[tid]:[])],res=cp.spawnSync('python',args,{cwd:root,encoding:'utf8'});
  fs.writeFileSync(path.join(root,'docs/art/qa/'+dest+'.json'),res.stdout);
  if(res.status!==0)throw Error('Validation failed '+dest+res.stdout+res.stderr);
 }
 console.log(JSON.stringify({task:tid,status:'gerado',asset_count:records.length,technical_passed:true,plan_passed:true,assets:records.map(r=>({id:r.id,size:r.actual_size_px,source_size:r.source_size_px,alpha:r.measurements.alpha_extrema})),previews:[sheet,ident,layers]}));
}
main().catch(e=>{console.error(e);process.exit(1);});

