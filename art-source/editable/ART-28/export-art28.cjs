// Technical export only. All illustration pixels and source alpha come from ImageGen.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),tid='ART-28';
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const write=(p,d)=>{fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n');};
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.isAbsolute(p)?p:path.join(root,p))).digest('hex');
const png={palette:false,compressionLevel:9,adaptiveFiltering:true};
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
 return 'fileFormatVersion: 2\nguid: '+crypto.randomBytes(16).toString('hex')+'\nTextureImporter:\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  isReadable: 0\n  maxTextureSize: '+Math.max(256,2**Math.ceil(Math.log2(Math.max(...a.export_size_px))))+'\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  spriteMode: 1\n  spriteExtrude: 1\n  spriteMeshType: 0\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: '+Math.max(256,2**Math.ceil(Math.log2(Math.max(...a.export_size_px))))+'\n    textureFormat: -1\n    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n    overridden: 0\n  userData: ART-28 independent sprite; no scene integration\n  assetBundleName: \n  assetBundleVariant: \n';
}
async function main(){
 const m=read('docs/art/manifest.json'),p=read('docs/art/prompts/ART-28.json'),gens=read('art-source/editable/ART-28/generations.json'),records=[];
 for(const g of gens){
  const target=path.join(root,g.source_path);fs.mkdirSync(path.dirname(target),{recursive:true});
  if(fs.existsSync(target)&&sha(target)!==sha(g.origin_path))throw Error('Preserve source '+g.source_path);
  if(!fs.existsSync(target))fs.copyFileSync(g.origin_path,target);
  const md=await sharp(target).metadata();
  g.source_size_px=[md.width,md.height];g.source_mode=md.hasAlpha?'RGBA':'RGB';g.sha256=sha(target);
 }
 for(const a of m.assets.filter(a=>a.task_id===tid)){
  if(a.status!=='planejado')throw Error('Expected planned asset '+a.id);
  const g=gens.find(g=>g.selected&&g.asset_id===a.id),src=path.join(root,g.source_path),md=await sharp(src).metadata(),[w,h]=a.export_size_px;
  const dest=m.asset_root+'/'+a.path,target=path.join(root,dest);if(fs.existsSync(target))throw Error('Preserve final '+dest);
  fs.mkdirSync(path.dirname(target),{recursive:true});
  const inner=a.id==='CH01_DRE'?[614,819]:[w,h];
  const resampled=await sharp(src).resize({width:inner[0],height:inner[1],fit:'inside',kernel:'lanczos3'}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const sw=resampled.info.width,sh=resampled.info.height,l=Math.floor((w-sw)/2),t=Math.floor((h-sh)/2),r=w-sw-l,b=h-sh-t;
  if(a.alpha_policy==='recorte'&&!md.hasAlpha)throw Error('Source alpha missing');
  await sharp(resampled.data,{raw:resampled.info}).extend({left:l,right:r,top:t,bottom:b,background:{r:0,g:0,b:0,alpha:0}}).toColourspace('srgb').withIccProfile('srgb').png(png).toFile(target);
  fs.writeFileSync(target+'.meta',meta(a));
  const raw=await sharp(target).raw().toBuffer({resolveWithObject:true}),metrics=measure(raw.data,raw.info);
  if(raw.info.width!==w||raw.info.height!==h||raw.info.channels!==4)throw Error('Invalid export '+a.id);
  if(a.alpha_policy==='rgba_opaco'&&metrics.opaque_fraction!==1)throw Error('Opaque background required');
  if(a.alpha_policy==='recorte'&&(!metrics.transparent_fraction||metrics.outer_edge_alpha_max))throw Error('Invalid cutout');
  const refs=[...a.references,...(a.id==='CH01_DRE'?['Assets/Art/new/characters/CH01/ch01_canonical.png']:[])];
  const rec={id:a.id,path:a.path,output_path:dest,status:'gerado',tool:'image_gen.imagegen',model:null,prompt:g.prompt,transparent_background:g.transparent_background,source_path:g.source_path,source_size_px:g.source_size_px,source_mode:g.source_mode,source_sha256:g.sha256,references:refs,reference_sha256:Object.fromEntries(refs.map(p=>[p,sha(p)])),actual_size_px:[w,h],mode:'RGBA',sha256:sha(target),display_size_px:a.display_size_px,display_fit:a.display_fit,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,technical_export:{tool:'sharp/libvips',operation:'Uniform Lanczos reduction, transparent frame padding for CH01, RGB to RGBA for opaque background. No painting, alpha cleanup, recolor or cropping.',source_frame_px:g.source_size_px,resampled_frame_px:[sw,sh],uniform_scale:Math.min(inner[0]/md.width,inner[1]/md.height),transparent_padding_ltrb_px:[l,t,r,b],crop_ltrb_px:[0,0,0,0]},measurements:metrics,producer_source:'art-source/editable/ART-28/export-art28.cjs'};
  if(a.id==='CH01_DRE'){
   const ch=read('docs/art/characters.json').find(c=>c.id==='CH01');
   if(sha(m.asset_root+'/'+ch.canonical_asset)!==ch.identity_hash)throw Error('Canonical identity hash differs');
   rec.canonical_identity={path:m.asset_root+'/'+ch.canonical_asset,sha256:ch.identity_hash};
  }
  Object.assign(a,{status:'gerado',sha256:rec.sha256,actual_size_px:[w,h],generation_prompt:g.prompt,production_method:'imagegen',generation_tool:'image_gen.imagegen',generation_model:null,source_image:g.source_path,source_image_sha256:g.sha256,references:refs,production_record:'docs/art/prompts/ART-28.json',qa:{technical:false,visual:false,identity:false}});
  records.push(rec);
 }
 p.generations=gens;p.assets=records;p.producer_source='art-source/editable/ART-28/export-art28.cjs';p.integration_performed=false;
 m.tasks.find(t=>t.id===tid).status='gerado';write('docs/art/manifest.json',m);write('docs/art/prompts/ART-28.json',p);
 const tasks=read('docs/art/tasks.json');tasks.find(t=>t.id===tid).status='gerado';write('docs/art/tasks.json',tasks);
 console.log(JSON.stringify(records.map(r=>({id:r.id,size:r.actual_size_px,source:r.source_size_px,measurements:r.measurements})),null,2));
}
main().catch(e=>{console.error(e);process.exit(1);});
