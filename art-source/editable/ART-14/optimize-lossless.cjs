// Choose PNG filter configuration by byte size; prove equality of every RGBA byte.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),hash=b=>crypto.createHash('sha256').update(b).digest('hex');
async function main(){
 const file=path.join(root,'docs/art/prompts/ART-14.json'),prod=JSON.parse(fs.readFileSync(file,'utf8'));
 const m=JSON.parse(fs.readFileSync(path.join(root,'docs/art/manifest.json'),'utf8'));if(prod.status!=='gerado')throw Error('Only pending generated exports.');
 for(const r of prod.assets){
  if(m.assets.find(a=>a.id===r.id).status==='validado')throw Error('Preserve validated art.');
  const target=path.join(root,r.output_path),original=fs.readFileSync(target);if(hash(original)!==r.sha256)throw Error('Unexpected existing export.');
  const pixels=await sharp(original).raw().toBuffer();let best=original,selected='original',trials=[];
  for(const adaptive of [false,true]){
   const png=await sharp(original).keepMetadata().png({palette:false,compressionLevel:9,adaptiveFiltering:adaptive}).toBuffer();
   const identical=Buffer.compare(pixels,await sharp(png).raw().toBuffer())===0;if(!identical)throw Error('PNG optimization changed pixels.');
   trials.push({adaptiveFiltering:adaptive,bytes:png.length,all_rgba_bytes_equal:true});if(png.length<best.length){best=png;selected=adaptive;}
  }
  fs.writeFileSync(target,best);r.lossless_optimization={tool:'sharp PNG recompression',before_bytes:original.length,after_bytes:best.length,selected_adaptive_filtering:selected,trials,all_rgba_bytes_equal:true};
  r.output_bytes=best.length;r.sha256=hash(best);console.log(JSON.stringify({id:r.id,before:original.length,after:best.length,equal:true}));
 }
 prod.lossless_optimizer='art-source/editable/ART-14/optimize-lossless.cjs';fs.writeFileSync(file,JSON.stringify(prod,null,2)+'\n','utf8');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
