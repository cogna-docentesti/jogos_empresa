// ART-16 technical export and previews. Illustration and alpha originate in integrated ImageGen.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const write=(p,d)=>{const f=path.join(root,p);fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,JSON.stringify(d,null,2)+'\n','utf8');};
const png={palette:false,compressionLevel:9,adaptiveFiltering:true};
const palette={light:'#F5F0E8',dark:'#24113D',ink:'#30243C'};
const label=(s,width,color=palette.ink)=>Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="40"><text x="14" y="27" font-family="Segoe UI,Arial,sans-serif" font-size="18" font-weight="600" fill="${color}">${s}</text></svg>`);
function measure(data,info){
 const hist=Array(256).fill(0),bb={1:[info.width,info.height,-1,-1],128:[info.width,info.height,-1,-1]};let edge=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];hist[a]++;
  if(!x||!y||x===info.width-1||y===info.height-1)edge=Math.max(edge,a);
  for(const t of [1,128])if(a>=t){const b=bb[t];b[0]=Math.min(x,b[0]);b[1]=Math.min(y,b[1]);b[2]=Math.max(x,b[2]);b[3]=Math.max(y,b[3]);}
 }
 const box=t=>bb[t][2]<0?null:[bb[t][0],bb[t][1],bb[t][2]-bb[t][0]+1,bb[t][3]-bb[t][1]+1],n=info.width*info.height;
 return {alpha_extrema:[hist.findIndex(v=>v),255-[...hist].reverse().findIndex(v=>v)],transparent_fraction:hist[0]/n,fully_opaque_fraction:hist[255]/n,alpha_250_or_more_fraction:hist.slice(250).reduce((a,b)=>a+b,0)/n,semitransparent_fraction:hist.slice(1,255).reduce((a,b)=>a+b,0)/n,dominant_nonzero_alpha:hist.map((count,a)=>[a,count]).slice(1).sort((a,b)=>b[1]-a[1]).slice(0,4),alpha_bbox_xywh:box(1),visible_bbox_alpha_128_xywh:box(128),outer_edge_alpha_max:edge};
}
async function display(input,w,h){
 return sharp(input).resize({width:w,height:h,fit:'contain',background:{r:0,g:0,b:0,alpha:0},kernel:'lanczos3'}).png(png).toBuffer();
}
async function individualPreview(a,g,out,native){
 const w=native?960:520,h=native?540:292,pw=w+32,ph=h+64,layers=[];
 const sprite=await display(path.join(root,out),w,h);
 for(let i=0;i<2;i++){
  layers.push({input:{create:{width:pw,height:ph,channels:4,background:i?palette.dark:palette.light}},left:i*pw,top:0});
  layers.push({input:label(g.name+' · '+(native?'960×540':'caixa 520×292'),pw,i?palette.light:palette.ink),left:i*pw,top:0});
  layers.push({input:sprite,left:i*pw+16,top:48});
 }
 const dest='art-source/previews/ART-16/'+a.id+(native?'-native-light-dark.png':'-display-light-dark.png');
 await sharp({create:{width:pw*2,height:ph,channels:4,background:palette.light}}).composite(layers).png(png).toFile(path.join(root,dest));
 return dest;
}
async function main(){
 const m=read('docs/art/manifest.json'),gen=read('art-source/editable/ART-16/generations.json'),selected=m.assets.filter(a=>a.task_id==='ART-16'),records=[];
 fs.mkdirSync(path.join(root,'art-source/previews/ART-16'),{recursive:true});
 for(const a of selected){
  const g=gen.assets.find(r=>r.id===a.id),out=m.asset_root+'/'+a.path,target=path.join(root,out),src=path.join(root,g.source_path);
  if(a.status!=='planejado'||fs.existsSync(target))throw Error('Preserve existing art: '+out);
  const meta=await sharp(src).metadata();if(!meta.hasAlpha)throw Error('True alpha required: '+a.id);
  const scale=Math.min(912/meta.width,513/meta.height);
  const {data:scaled,info}=await sharp(src).resize({width:912,height:513,fit:'inside',kernel:'lanczos3'}).raw().toBuffer({resolveWithObject:true});
  const l=Math.floor((960-info.width)/2),r=960-info.width-l,t=Math.floor((540-info.height)/2),b=540-info.height-t;
  fs.mkdirSync(path.dirname(target),{recursive:true});
  await sharp(scaled,{raw:info}).extend({left:l,right:r,top:t,bottom:b,background:{r:0,g:0,b:0,alpha:0}}).toColourspace('srgb').withIccProfile('srgb').png(png).toFile(target);
  const final=await sharp(target).metadata(),raw=await sharp(target).raw().toBuffer({resolveWithObject:true}),measurements=measure(raw.data,raw.info);
  if(final.width!==960||final.height!==540||raw.info.channels!==4||measurements.outer_edge_alpha_max)throw Error('Invalid export geometry or clipped alpha');
  const nativePreview=await individualPreview(a,g,out,true),displayPreview=await individualPreview(a,g,out,false);
  const record={id:g.id,name:g.name,prompt:g.prompt,tool:g.tool,model:null,model_note:'Identificador de modelo não exposto pelo ImageGen integrado.',production_method:'imagegen',transparent_background:true,tool_output_hint:g.tool_output_hint,source_path:g.source_path,source_sha256:sha(g.source_path),source_size_px:[meta.width,meta.height],source_mode:'RGBA',source_editable:null,references:g.references,reference_sha256:Object.fromEntries(g.references.map(p=>[p,sha(p)])),canonical_map_asset:{id:g.canonical,path:m.asset_root+'/'+m.assets.find(a=>a.id===g.canonical).path,sha256:sha(m.asset_root+'/'+m.assets.find(a=>a.id===g.canonical).path)},output_path:out,path:a.path,actual_size_px:[960,540],output_bytes:fs.statSync(target).size,sha256:sha(out),mode:'RGBA',color_space:'sRGB',srgb_profile:final.icc?'sRGB':null,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,display_size_px:a.display_size_px,display_fit:'contain',display_uniform_scale:Math.min(520/960,292/540),technical_export:{tool:'sharp/libvips',nominal_uniform_scale:scale,resampled_frame_px:[info.width,info.height],transparent_padding_ltrb_px:[l,t,r,b],operation:'Uniform inside reduction of the complete generated source, followed by transparent padding to 960x540. Integer pixel rounding only; no crop, alpha cleanup, masks, painting, recolor or independent axis stretching.'},native_preview:nativePreview,display_preview:displayPreview,measurements,status:'gerado'};
  records.push(record);
  Object.assign(a,{status:'gerado',production_method:'imagegen',actual_size_px:[960,540],sha256:record.sha256,generation_prompt:g.prompt,source_editable:null,references:[...new Set([...a.references,...g.references,record.canonical_map_asset.path])],production_record:'docs/art/prompts/ART-16.json',qa:{technical:false,visual:false,identity:false}});
  console.log(JSON.stringify({id:a.id,size:record.actual_size_px,bytes:record.output_bytes,visible_bbox:measurements.visible_bbox_alpha_128_xywh,padding:[l,t,r,b],dominant_nonzero_alpha:measurements.dominant_nonzero_alpha}));
 }
 // Every vignette appears at its actual 520x292 display box on both backgrounds.
 const tw=552,th=704,layers=[{input:label('ART-16 · Cinco vinhetas de localização · 520×292 · Fundos e títulos somente na QA',1656),left:0,top:0}];
 for(let i=0;i<records.length;i++){
  const rec=records[i],left=(i%3)*tw,top=48+Math.floor(i/3)*th,sprite=await display(path.join(root,rec.output_path),520,292);
  for(let j=0;j<2;j++){
   layers.push({input:{create:{width:tw,height:352,channels:4,background:j?palette.dark:palette.light}},left,top:top+j*352});
   layers.push({input:label(rec.name+(j?' · escuro':' · claro'),tw,j?palette.light:palette.ink),left,top:top+j*352});
   layers.push({input:sprite,left:left+16,top:top+j*352+44});
  }
 }
 await sharp({create:{width:1656,height:1456,channels:4,background:'#DDD5E5'}}).composite(layers).png(png).toFile(path.join(root,'art-source/previews/ART-16/ART-16-contact-sheet.png'));
 // Recognizable architecture compared with the actual map sprite; both use contain.
 const compare=[];
 for(let i=0;i<records.length;i++){
  const r=records[i],top=i*340;
  compare.push({input:label(r.name+' · mapa / vinheta',832),left:0,top});
  compare.push({input:await display(path.join(root,r.canonical_map_asset.path),260,292),left:16,top:top+40});
  compare.push({input:await display(path.join(root,r.output_path),520,292),left:296,top:top+40});
 }
 await sharp({create:{width:832,height:1700,channels:4,background:palette.light}}).composite(compare).png(png).toFile(path.join(root,'art-source/previews/ART-16/ART-16-architecture-comparison.png'));
 m.tasks.find(t=>t.id==='ART-16').status='gerado';write('docs/art/manifest.json',m);
 write('docs/art/prompts/ART-16.json',{task_id:'ART-16',date:'2026-10-14',status:'gerado',tool:'image_gen.imagegen',model:null,model_note:'Nenhum identificador de modelo é exposto pela ferramenta integrada.',producer_source:'art-source/editable/ART-16/export-art16.cjs',assets:records,renderer_versions:sharp.versions,integration_performed:false});
 write('docs/art/qa/ART-16-measurements.json',{task_id:'ART-16',date:'2026-10-14',assets:records.map(r=>({id:r.id,path:r.output_path,actual_size_px:r.actual_size_px,sha256:r.sha256,bytes:r.output_bytes,...r.measurements})),total_bytes:records.reduce((s,r)=>s+r.output_bytes,0)});
 console.log(JSON.stringify({png_count:records.length,total_bytes:records.reduce((s,r)=>s+r.output_bytes,0)}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
