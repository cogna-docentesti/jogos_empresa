// Technical export only: preserve ImageGen illustration and alpha; uniform reduction and padding.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),tid='ART-29',file=p=>path.join(root,p);
const read=p=>JSON.parse(fs.readFileSync(file(p),'utf8'));
const write=(p,d)=>{fs.mkdirSync(path.dirname(file(p)),{recursive:true});fs.writeFileSync(file(p),JSON.stringify(d,null,2)+'\n');};
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.isAbsolute(p)?p:file(p))).digest('hex');
const png={compressionLevel:9,palette:false,adaptiveFiltering:true};
function measure(data,info){
 const hist=Array(256).fill(0),boxes={1:[info.width,info.height,-1,-1],128:[info.width,info.height,-1,-1]};let edge=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];hist[a]++;
  if(!x||!y||x===info.width-1||y===info.height-1)edge=Math.max(edge,a);
  for(const t of [1,128])if(a>=t){const b=boxes[t];b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x+1);b[3]=Math.max(b[3],y+1);}
 }
 return {alpha_extrema:[hist.findIndex(v=>v),255-[...hist].reverse().findIndex(v=>v)],transparent_fraction:hist[0]/(info.width*info.height),opaque_fraction:hist[255]/(info.width*info.height),alpha_bbox_ltrb_px:boxes[1],visible_bbox_alpha128_ltrb_px:boxes[128],outer_edge_alpha_max:edge};
}
function meta(a){
 return 'fileFormatVersion: 2\nguid: '+crypto.randomBytes(16).toString('hex')+'\nTextureImporter:\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  isReadable: 0\n  maxTextureSize: 1024\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  spriteMode: 1\n  spriteExtrude: 1\n  spriteMeshType: 0\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 1024\n    textureFormat: -1\n    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n    overridden: 0\n  userData: ART-29 independent sprite; no scene integration\n  assetBundleName:\n  assetBundleVariant:\n';
}
async function main(){
 const m=read('docs/art/manifest.json'),p=read('docs/art/prompts/ART-29.json'),characters=read('docs/art/characters.json');
 const revise=process.argv[2]==='--revise'?process.argv[3]:null;
 for(const a of m.assets.filter(a=>a.task_id===tid&&(!revise||a.id===revise))){
  if(a.status!==(revise?'gerado':'planejado'))throw Error('Expected unvalidated asset '+a.id);
  const g=p.generations.find(g=>g.asset_id===a.id&&g.selected),src=file(g.source_path),dest=m.asset_root+'/'+a.path;
  if(fs.existsSync(file(dest))&&(!revise||sha(dest)!==a.sha256))throw Error('Preserve existing final '+dest);
  fs.mkdirSync(path.dirname(src),{recursive:true});fs.copyFileSync(g.origin_path,src);
  const md=await sharp(src).metadata();if(!md.hasAlpha)throw Error('True source alpha required');
  g.source_size_px=[md.width,md.height];g.source_mode='RGBA';g.source_sha256=sha(src);
  const [w,h]=a.export_size_px,inner=[614,819];
  const rr=await sharp(src).resize({width:inner[0],height:inner[1],fit:'inside',kernel:'lanczos3'}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const sw=rr.info.width,sh=rr.info.height,l=Math.floor((w-sw)/2),t=Math.floor((h-sh)/2),r=w-sw-l,b=h-sh-t;
  await sharp(rr.data,{raw:rr.info}).extend({left:l,right:r,top:t,bottom:b,background:{r:0,g:0,b:0,alpha:0}}).toColourspace('srgb').withIccProfile('srgb').png(png).toFile(file(dest));
  if(!fs.existsSync(file(dest+'.meta')))fs.writeFileSync(file(dest+'.meta'),meta(a));
  const raw=await sharp(file(dest)).raw().toBuffer({resolveWithObject:true}),metrics=measure(raw.data,raw.info);
  if(raw.info.width!==w||raw.info.height!==h||raw.info.channels!==4||!metrics.transparent_fraction||metrics.outer_edge_alpha_max)throw Error('Invalid export '+a.id);
  const ch=characters.find(ch=>ch.id===a.character_ids[0]),canonical=m.asset_root+'/'+ch.canonical_asset;
  if(sha(canonical)!==ch.identity_hash)throw Error('Canonical hash changed '+ch.id);
  const rec={id:a.id,path:a.path,output_path:dest,status:'gerado',tool:'image_gen.imagegen',model:null,prompt:g.prompt,transparent_background:true,source_path:g.source_path,source_size_px:g.source_size_px,source_mode:'RGBA',source_sha256:g.source_sha256,references:g.references,reference_sha256:Object.fromEntries(g.references.map(p=>[p,sha(p)])),canonical_identity:{path:canonical,sha256:ch.identity_hash},actual_size_px:[w,h],mode:'RGBA',sha256:sha(dest),display_size_px:a.display_size_px,display_fit:a.display_fit,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,technical_export:{tool:'sharp/libvips',operation:'Uniform Lanczos reduction of complete source frame and transparent padding only. No crop, repaint, recolor, alpha cleanup or independent x/y scaling.',source_frame_px:g.source_size_px,resampled_frame_px:[sw,sh],uniform_scale:Math.min(inner[0]/md.width,inner[1]/md.height),transparent_padding_ltrb_px:[l,t,r,b],crop_ltrb_px:[0,0,0,0]},measurements:metrics,producer_source:'art-source/editable/ART-29/export-art29.cjs'};
  p.assets=p.assets.filter(r=>r.id!==a.id);p.assets.push(rec);
  Object.assign(a,{status:'gerado',sha256:rec.sha256,actual_size_px:[w,h],generation_prompt:g.prompt,production_method:'imagegen',generation_tool:'image_gen.imagegen',generation_model:null,source_image:g.source_path,source_image_sha256:g.source_sha256,references:g.references,production_record:'docs/art/prompts/ART-29.json',qa:{technical:false,visual:false,identity:false}});
 }
 m.tasks.find(t=>t.id===tid).status='gerado';p.producer_source='art-source/editable/ART-29/export-art29.cjs';
 const tasks=read('docs/art/tasks.json');tasks.find(t=>t.id===tid).status='gerado';
 write('docs/art/manifest.json',m);write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-29.json',p);
 console.log(JSON.stringify(p.assets.map(a=>({id:a.id,source:a.source_size_px,size:a.actual_size_px,alpha:a.measurements})),null,2));
}
main().catch(e=>{console.error(e);process.exit(1);});
