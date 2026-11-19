// ART-14: geometry for ornaments; technical resampling only for integrated ImageGen art.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const sha=p=>hash(fs.readFileSync(path.join(root,p)));
const write=(p,d)=>{const file=path.join(root,p);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,JSON.stringify(d,null,2)+'\n','utf8');};
async function main(){
 const m=read('docs/art/manifest.json'),p=read('docs/art/tokens.json').palette,records=[];
 const previous=fs.existsSync(path.join(root,'docs/art/prompts/ART-14.json'))?read('docs/art/prompts/ART-14.json'):null;
 const idIndex=process.argv.indexOf('--id'),onlyId=idIndex<0?null:process.argv[idIndex+1];
 const gen=read('art-source/editable/ART-14/generations.json');
 for(const a of m.assets.filter(a=>a.task_id==='ART-14'&&(!onlyId||a.id===onlyId))){
  const out=m.asset_root+'/'+a.path,target=path.join(root,out);
  if(a.status==='validado')throw Error('Preserve validated art: '+out);
  if(fs.existsSync(target)&&!(process.argv.includes('--replace-generated')&&previous?.assets.find(r=>r.id===a.id)?.sha256===sha(out)&&previous.status==='gerado'))throw Error('Preserve existing art: '+out);
  fs.mkdirSync(path.dirname(target),{recursive:true});let record;
  if(a.kind==='ornamento'){
   const right=a.id==='ID_ORNAMENT_RIGHT',source='art-source/editable/identification/ornaments/'+(right?'arc_right':'arc_left')+'.svg';
   if(fs.existsSync(path.join(root,source)))throw Error('Existing SVG');
   const drawing=`<path d="M438 88C114 106 52 410 190 594C292 730 318 843 180 936" fill="none" stroke="${p.lavender}" stroke-width="44" stroke-opacity="0.24" stroke-linecap="round"/><path d="M396 278C266 210 142 312 166 430C182 512 260 550 330 530" fill="none" stroke="${p.violet}" stroke-width="20" stroke-opacity="0.18" stroke-linecap="round"/><circle cx="388" cy="170" r="44" fill="${p.violet}" fill-opacity="0.14"/><circle cx="145" cy="810" r="68" fill="${p.lavender}" fill-opacity="0.18"/><circle cx="400" cy="894" r="18" fill="${p.violet}" fill-opacity="0.24"/>`;
   const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="512" height="1024" viewBox="0 0 512 1024"><g${right?' transform="translate(512 0) scale(-1 1)"':''}>${drawing}</g></svg>\n`;
   fs.mkdirSync(path.dirname(path.join(root,source)),{recursive:true});fs.writeFileSync(path.join(root,source),svg,'utf8');
   await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9,adaptiveFiltering:true}).toFile(target);
   record={id:a.id,production_method:'vector_or_code',tool:'SVG editable geometry + sharp/libvips',model:null,source_editable:source,source_sha256:sha(source),
    prompt:`Produzir somente ${a.id}: arcos largos lavanda #B99AE8 a 24%, arco fino violeta #6D28D9 a 18% e três círculos discretos a 14–24%, fundo alfa zero, sem efeito/sombra/texto/ícone. Export SVG editável e PNG RGBA sRGB 512x1024; display contain na caixa 320x860, sem esticar. Ornamento ${right?'direito, espelhado horizontalmente do desenho geométrico esquerdo':'esquerdo'}. Linguagem dos arcos roxos do concept de identificação aprovado.`,
    references:['art-source/references/concepts/02-abertura.png','art-source/references/concepts/01-hub.png','docs/art/tokens.json'],
    mirror_of:right?'ID_ORNAMENT_LEFT':null,alpha_preserved:'Direct SVG alpha coverage, no bitmap background.',actual_size_px:[512,1024]};
  }else{
   const g=gen.assets.find(g=>g.id===a.id);if(!g)throw Error('Missing selected generation');
   const input=path.join(root,g.source_path),meta=await sharp(input).metadata();
   if(a.kind==='personagem'&&!meta.hasAlpha)throw Error('Character source requires true alpha.');
   let operation,scale,padding;
   if(a.kind==='cenario'){
    scale=Math.max(1920/meta.width,1080/meta.height);padding=null;
    await sharp(input).resize({width:1920,height:1080,fit:'cover',position:'centre',kernel:'lanczos3'})
     .toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9,adaptiveFiltering:true}).toFile(target);
    operation='Uniform cover resize; slight centered aspect crop only, preserve colors. Opaque RGB source gains alpha 255.';
   }else{
    // Canonical output frame with uniform 7/8 scale to provide >=10% clear vertical margins.
    if(meta.width!==1024||meta.height!==1536)throw Error('Review source framing for unexpected character size.');
    scale=.875;padding=[64,96,64,96];
    await sharp(input).resize({width:896,height:1344,kernel:'lanczos3'})
     .extend({left:64,right:64,top:96,bottom:96,background:{r:0,g:0,b:0,alpha:0}})
     .toColourspace('srgb').withIccProfile('srgb').png({palette:false,compressionLevel:9,adaptiveFiltering:true}).toFile(target);
    operation='Uniform 7/8 resample of complete generated sprite and transparent padding. No alpha cleanup, painting, mask, anatomy edit or independent axis stretch.';
   }
   record={...g,production_method:'imagegen',source_sha256:sha(g.source_path),source_size_px:[meta.width,meta.height],source_mode:meta.hasAlpha?'RGBA':'RGB',
    source_editable:null,actual_size_px:a.export_size_px,technical_export:{tool:'sharp',nominal_uniform_scale:scale,padding_ltrb_px:padding,operation},
    canonical_reference:a.kind==='personagem'?{path:'Assets/Art/new/characters/CH01/ch01_canonical.png',sha256:sha('Assets/Art/new/characters/CH01/ch01_canonical.png')}:null};
  }
  const meta=await sharp(target).metadata();if(meta.width!==a.export_size_px[0]||meta.height!==a.export_size_px[1]||meta.channels!==4)throw Error('Invalid output geometry');
  const {data,info}=await sharp(target).raw().toBuffer({resolveWithObject:true});let x0=info.width,y0=info.height,x1=-1,y1=-1;
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>=128){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}
  Object.assign(record,{output_path:out,path:a.path,sha256:sha(out),output_bytes:fs.statSync(target).size,status:'gerado',color_space:'sRGB',mode:'RGBA',
   display_size_px:a.display_size_px,display_fit:a.display_fit,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px,
   visible_bbox_alpha_128_px:x1<0?null:[x0,y0,x1-x0+1,y1-y0+1],reference_sha256:Object.fromEntries(record.references.map(ref=>[ref,sha(ref)]))});
  records.push(record);console.log(JSON.stringify({id:a.id,size:record.actual_size_px,bytes:record.output_bytes,bbox:record.visible_bbox_alpha_128_px}));
 }
 const allRecords=previous?previous.assets.map(old=>records.find(r=>r.id===old.id)||old):records;
 write('docs/art/prompts/ART-14.json',{task_id:'ART-14',date:'2026-10-14',status:'gerado',assets:allRecords,renderer_versions:sharp.versions,
  model_note:'ImageGen integrado não expõe identificador de modelo. Ornamentos são vetoriais e não usam modelo.',integration_performed:false});
 console.log(JSON.stringify({png_count:allRecords.length,total_bytes:allRecords.reduce((n,r)=>n+r.output_bytes,0)}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
