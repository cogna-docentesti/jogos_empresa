// Technical resampling only; ImageGen provides the illustrations and true alpha.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),tid=process.argv[2];
if(!['ART-23'].includes(tid))throw Error('Only authorized equipment batches');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const write=(p,d)=>{const f=path.join(root,p);fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,JSON.stringify(d,null,2)+'\n','utf8');};
const png={palette:false,compressionLevel:9,adaptiveFiltering:true},bg=['#F5F0E8','#24113D'];
function measure(data,info){
 const hist=Array(256).fill(0),bounds={1:[info.width,info.height,-1,-1],128:[info.width,info.height,-1,-1]};let edge=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];hist[a]++;
  if(!x||!y||x===info.width-1||y===info.height-1)edge=Math.max(edge,a);
  for(const threshold of [1,128])if(a>=threshold){const b=bounds[threshold];b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x+1);b[3]=Math.max(b[3],y+1);}
 }
 const box=t=>bounds[t][2]<0?null:bounds[t],n=info.width*info.height;
 return {alpha_extrema:[hist.findIndex(v=>v),255-[...hist].reverse().findIndex(v=>v)],transparent_fraction:hist[0]/n,fully_opaque_fraction:hist[255]/n,alpha_250_or_more_fraction:hist.slice(250).reduce((a,b)=>a+b,0)/n,semitransparent_fraction:hist.slice(1,255).reduce((a,b)=>a+b,0)/n,dominant_nonzero_alpha:hist.map((count,alpha)=>({alpha,count})).slice(1).sort((a,b)=>b.count-a.count).slice(0,4),alpha_bbox_ltrb:box(1),visible_bbox_alpha128_ltrb:box(128),outer_edge_alpha_max:edge};
}
const label=(s,w,color='#30243C',size=18)=>Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="44"><text x="16" y="28" font-family="Segoe UI,Arial,sans-serif" font-size="${size}" font-weight="600" fill="${color}">${s}</text></svg>`);
async function preview(r,native){
 const side=native?512:250,cw=side+32,ch=side+68,layers=[],sprite=await sharp(path.join(root,r.output_path)).resize(side,side).png(png).toBuffer();
 for(let i=0;i<2;i++){
  layers.push({input:{create:{width:cw,height:ch,channels:4,background:bg[i]}},left:i*cw,top:0});
  layers.push({input:label(r.short_name+' · '+side+'×'+side,cw,i?bg[0]:'#30243C',native?18:14),left:i*cw,top:0});
  layers.push({input:sprite,left:i*cw+16,top:48});
 }
 const dest=`art-source/previews/${tid}/${r.id}${native?'-native-light-dark':'-display-light-dark'}.png`;
 await sharp({create:{width:cw*2,height:ch,channels:4,background:bg[0]}}).composite(layers).png(png).toFile(path.join(root,dest));return dest;
}
async function main(){
 const m=read('docs/art/manifest.json'),g=read(`art-source/editable/${tid}/generations.json`),hints=read(`art-source/editable/${tid}/tool-output-hints.json`),index=read('docs/art/tasks.json'),records=[];
 if(g.assets.length!==3)throw Error('Expected three equipment requests');
 fs.mkdirSync(path.join(root,`art-source/previews/${tid}`),{recursive:true});
 for(const req of g.assets){
  const a=m.assets.find(a=>a.id===req.id);if(a.task_id!==tid)throw Error('Wrong batch');
  const out=m.asset_root+'/'+a.path,target=path.join(root,out),src=path.join(root,req.source_path);
  if(a.status!=='planejado'||fs.existsSync(target))throw Error('Preserve existing validated art: '+out);
  const meta=await sharp(src).metadata();if(!meta.hasAlpha)throw Error('Original alpha required');
  const scaled=await sharp(src).resize({width:410,height:410,fit:'inside',kernel:'lanczos3'}).raw().toBuffer({resolveWithObject:true});
  const l=Math.floor((512-scaled.info.width)/2),r=512-scaled.info.width-l,t=Math.floor((512-scaled.info.height)/2),b=512-scaled.info.height-t;
  fs.mkdirSync(path.dirname(target),{recursive:true});
  await sharp(scaled.data,{raw:scaled.info}).extend({left:l,right:r,top:t,bottom:b,background:{r:0,g:0,b:0,alpha:0}}).toColourspace('srgb').withIccProfile('srgb').png(png).toFile(target);
  const final=await sharp(target).raw().toBuffer({resolveWithObject:true}),metrics=measure(final.data,final.info);
  if(final.info.width!==512||final.info.height!==512||final.info.channels!==4||metrics.outer_edge_alpha_max!==0)throw Error('Invalid export');
  const record={...req,production_method:'imagegen',tool_output_hint:hints[a.id],source_sha256:sha(req.source_path),source_size_px:[meta.width,meta.height],source_mode:'RGBA',reference_sha256:Object.fromEntries(req.references.map(p=>[p,sha(p)])),output_path:out,path:a.path,actual_size_px:[512,512],mode:'RGBA',color_space:'sRGB',output_bytes:fs.statSync(target).size,sha256:sha(out),source_editable:null,producer_source:'art-source/editable/ART-23/export-art23.cjs',technical_export:{tool:'sharp/libvips',operation:'Uniform reduction of the complete generated frame followed by transparent padding. No crop, alpha cleanup, painting, masks, recolor or axis stretching.',source_frame_px:[meta.width,meta.height],resampled_frame_px:[scaled.info.width,scaled.info.height],uniform_scale:scaled.info.width/meta.width,transparent_padding_ltrb_px:[l,t,r,b]},display_size_px:a.display_size_px,display_fit:a.display_fit,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,measurements:metrics,status:'gerado'};
  record.native_preview=await preview(record,true);record.display_preview=await preview(record,false);records.push(record);
  Object.assign(a,{status:'gerado',sha256:record.sha256,actual_size_px:[512,512],generation_prompt:req.prompt,production_method:'imagegen',generation_tool:req.tool,generation_model:null,source_image:req.source_path,source_image_sha256:record.source_sha256,source_editable:null,references:req.references,visual_composition_proposal:req.visual_composition_proposal,production_record:`docs/art/prompts/${tid}.json`,qa:{technical:false,visual:false,identity:false}});
 }
 const width=1560,height=710,cw=520,cellh=300,layers=[],task=m.tasks.find(t=>t.id===tid);
 layers.push({input:label(tid+' | '+task.title,width,'#30243C',25),left:0,top:4});
 layers.push({input:label('3 PNGs RGBA · 512×512 | Exibição 250×250 | Fundos e rótulos apenas no QA',width,'#30243C',17),left:0,top:44});
 for(let i=0;i<records.length;i++){
  const r=records[i],sprite=await sharp(path.join(root,r.output_path)).resize(250,250).png(png).toBuffer();
  for(let row=0;row<2;row++){
   const x=i*cw,y=100+row*cellh;
   layers.push({input:{create:{width:cw,height:cellh,channels:4,background:bg[row]}},left:x,top:y});
   layers.push({input:label(r.short_name+' · '+(row?'fundo ameixa':'fundo marfim'),cw,row?bg[0]:'#30243C'),left:x,top:y});
   layers.push({input:sprite,left:x+Math.floor((cw-250)/2),top:y+44});
  }
 }
 const sheet=`art-source/previews/${tid}/${tid}-contact-sheet.png`;
 await sharp({create:{width,height,channels:4,background:'#E7DFED'}}).composite(layers).png(png).toFile(path.join(root,sheet));
 Object.assign(task,{status:'gerado',production_record:`docs/art/prompts/${tid}.json`,delivery_preview:sheet});
 Object.assign(index.find(t=>t.id===tid),{status:'gerado',production_record:task.production_record,delivery_preview:sheet});
 write(task.production_record,{task_id:tid,date:'2026-10-14',status:'gerado',tool:'image_gen.imagegen',model:null,model_note:'Specific model identifier not exposed by integrated tool.',scope_resolution:'Execute only ART-23 Japanese equipment in the manifest, continuing the prior clarification about numbered batches. ART-05 controls and previous batches preserved.',producer_source:'art-source/editable/ART-23/export-art23.cjs',assets:records,delivery_preview:sheet});
 write('docs/art/manifest.json',m);write('docs/art/tasks.json',index);
 console.log(JSON.stringify({task:tid,status:'gerado',asset_count:records.length,assets:records.map(r=>({id:r.id,source_size_px:r.source_size_px,actual_size_px:r.actual_size_px,measurements:r.measurements})),contact_sheet:sheet}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
