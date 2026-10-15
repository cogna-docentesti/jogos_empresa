// Technical resampling and QA assembly only; all illustration pixels come from integrated ImageGen.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const write=(p,d)=>{const f=path.join(root,p);fs.mkdirSync(path.dirname(f),{recursive:true});fs.writeFileSync(f,JSON.stringify(d,null,2)+'\n','utf8');};
const palette={light:'#F5F0E8',dark:'#24113D',lavender:'#B99AE8',ink:'#30243C'};
const names={FACADE_REST_FRANCES:'Francês',FACADE_REST_JAPONES:'Japonês',FACADE_REST_LANCHES:'Lanches'};
const png=()=>({palette:false,compressionLevel:9,adaptiveFiltering:true});
const label=(text,width,color=palette.ink)=>Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="40"><text x="16" y="28" font-family="Segoe UI,Arial,sans-serif" font-size="20" font-weight="600" fill="${color}">${text}</text></svg>`);

function measure(data,info){
 const hist=Array(256).fill(0),boxes={1:[info.width,info.height,-1,-1],128:[info.width,info.height,-1,-1]};let edgeMax=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];hist[a]++;
  if(x===0||y===0||x===info.width-1||y===info.height-1)edgeMax=Math.max(edgeMax,a);
  for(const t of [1,128])if(a>=t){const b=boxes[t];b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x);b[3]=Math.max(b[3],y);}
 }
 const box=t=>boxes[t][2]<0?null:[boxes[t][0],boxes[t][1],boxes[t][2]-boxes[t][0]+1,boxes[t][3]-boxes[t][1]+1];
 const total=info.width*info.height;
 return {alpha_extrema:[hist.findIndex(x=>x>0),255-[...hist].reverse().findIndex(x=>x>0)],transparent_fraction:hist[0]/total,opaque_fraction:hist[255]/total,semitransparent_fraction:hist.slice(1,255).reduce((s,n)=>s+n,0)/total,alpha_bbox_xywh:box(1),visible_bbox_alpha_128_xywh:box(128),outer_edge_alpha_max:edgeMax};
}

async function previews(a,output){
 const dir='art-source/previews/ART-15/';fs.mkdirSync(path.join(root,dir),{recursive:true});
 for(const native of [true,false]){
  const w=native?1024:480,h=native?768:360,pw=w+32,ph=h+64;
  const sprite=await sharp(path.join(root,output)).resize(w,h,{kernel:'lanczos3'}).png(png()).toBuffer();
  const layers=[];
  for(let i=0;i<2;i++){
   layers.push({input:{create:{width:pw,height:ph,channels:4,background:i?palette.dark:palette.light}},left:i*pw,top:0});
   layers.push({input:label(`${names[a.id]} · ${w}×${h} · ${i?'escuro':'claro'}`,pw,i?'#F5F0E8':palette.ink),left:i*pw,top:0});
   layers.push({input:sprite,left:i*pw+16,top:48});
  }
  await sharp({create:{width:2*pw,height:ph,channels:4,background:palette.light}}).composite(layers).png(png()).toFile(path.join(root,dir+a.id+(native?'-native-light-dark.png':'-display-light-dark.png')));
 }
}

async function main(){
 const m=read('docs/art/manifest.json'),gen=read('art-source/editable/ART-15/generations.json'),records=[];
 const selected=m.assets.filter(a=>a.task_id==='ART-15');
 for(const a of selected){
  const g=gen.assets.find(g=>g.id===a.id),out=m.asset_root+'/'+a.path,target=path.join(root,out);
  if(a.status==='validado'||fs.existsSync(target))throw Error('Preserve existing asset: '+out);
  const src=path.join(root,g.source_path),meta=await sharp(src).metadata();
  if(!meta.hasAlpha||meta.width*3!==meta.height*4)throw Error('Source must be reviewed RGBA 4:3: '+a.id);
  fs.mkdirSync(path.dirname(target),{recursive:true});
  // Complete source frame retained, scaled uniformly 960/sourceWidth then padded to exact export.
  await sharp(src).resize(960,720,{fit:'contain',kernel:'lanczos3',background:{r:0,g:0,b:0,alpha:0}})
   .extend({left:32,right:32,top:24,bottom:24,background:{r:0,g:0,b:0,alpha:0}})
   .toColourspace('srgb').withIccProfile('srgb').png(png()).toFile(target);
  const outMeta=await sharp(target).metadata(),raw=await sharp(target).raw().toBuffer({resolveWithObject:true}),measurement=measure(raw.data,raw.info);
  if(outMeta.width!==1024||outMeta.height!==768||raw.info.channels!==4||measurement.outer_edge_alpha_max!==0)throw Error('Export geometry/alpha failure');
  const record={...g,production_method:'imagegen',model:null,model_note:'Identificador de modelo não exposto pela ferramenta integrada.',source_sha256:sha(g.source_path),source_size_px:[meta.width,meta.height],source_mode:'RGBA',source_editable:null,output_path:out,path:a.path,actual_size_px:[outMeta.width,outMeta.height],output_bytes:fs.statSync(target).size,sha256:sha(out),color_space:outMeta.space,srgb_profile:outMeta.icc?'sRGB':null,mode:'RGBA',display_size_px:a.display_size_px,display_fit:a.display_fit,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,technical_export:{tool:'sharp/libvips',uniform_scale:960/meta.width,resampled_frame_px:[960,720],transparent_padding_ltrb_px:[32,24,32,24],operation:'Uniform reduction of the entire generated source frame plus transparent padding. No crop, alpha cleanup, masks, painting, recolor, perspective change or independent axis stretch.'},reference_sha256:Object.fromEntries(g.references.map(r=>[r,sha(r)])),measurements:measurement,status:'gerado'};
  records.push(record);await previews(a,out);
  Object.assign(a,{status:'gerado',actual_size_px:record.actual_size_px,sha256:record.sha256,production_method:'imagegen',source_editable:null,generation_prompt:g.prompt,references:[...new Set([...a.references,...g.references])],production_record:'docs/art/prompts/ART-15.json',qa:{technical:false,visual:false,identity:false}});
  console.log(JSON.stringify({id:a.id,size:record.actual_size_px,bytes:record.output_bytes,...measurement}));
 }
 const layers=[{input:label('ART-15 · Fachadas dos três restaurantes · 480×360 por sprite · Fundos de QA, fora dos assets',1536),left:0,top:0}];
 for(let col=0;col<selected.length;col++){
  const a=selected[col],sprite=await sharp(path.join(root,m.asset_root,a.path)).resize(480,360).png(png()).toBuffer();
  for(let row=0;row<2;row++){
   const top=48+row*416;layers.push({input:{create:{width:512,height:416,channels:4,background:row?palette.dark:palette.light}},left:col*512,top});
   layers.push({input:label(names[a.id]+' · '+(row?'fundo escuro':'fundo claro'),512,row?palette.light:palette.ink),left:col*512,top});
   layers.push({input:sprite,left:col*512+16,top:top+48});
  }
 }
 await sharp({create:{width:1536,height:880,channels:4,background:palette.light}}).composite(layers).png(png()).toFile(path.join(root,'art-source/previews/ART-15/ART-15-contact-sheet.png'));
 const task=m.tasks.find(t=>t.id==='ART-15');task.status='gerado';
 write('docs/art/manifest.json',m);
 write('docs/art/prompts/ART-15.json',{task_id:'ART-15',date:'2026-10-14',status:'gerado',tool:'image_gen.imagegen',model:null,assets:records,renderer_versions:sharp.versions,producer_source:'art-source/editable/ART-15/export-art15.cjs',integration_performed:false});
 write('docs/art/qa/ART-15-measurements.json',{task_id:'ART-15',date:'2026-10-14',assets:records.map(r=>({id:r.id,path:r.output_path,actual_size_px:r.actual_size_px,output_bytes:r.output_bytes,sha256:r.sha256,...r.measurements})),total_bytes:records.reduce((s,r)=>s+r.output_bytes,0)});
 console.log(JSON.stringify({png_count:records.length,total_bytes:records.reduce((s,r)=>s+r.output_bytes,0)}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
