/* Repair only the generated, unvalidated restaurant SVG after visual review. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const {designs}=require('./build-art06.cjs');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const write=(p,d)=>fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n','utf8');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
async function main(){
  const m=read('docs/art/manifest.json'),p=read('docs/art/prompts/ART-06.json');
  const a=m.assets.find(a=>a.id==='ICON_RESTAURANT'),r=p.assets.find(r=>r.id===a.id);
  if(a.status!=='gerado'||r.status!=='gerado'||sha(r.output_path)!==r.sha256)throw new Error('Protected or changed restaurant asset');
  const before={sha256:r.sha256,source_sha256:r.source_sha256,prompt:r.final_prompt};
  const d=designs.ICON_RESTAURANT;
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256"><g fill="#FFFFFF">${d.body}</g></svg>\n`;
  fs.writeFileSync(path.join(root,r.source_editable),svg,'utf8');
  await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(path.join(root,r.output_path));
  r.semantic=d.semantic;r.final_prompt=r.final_prompt.replace(/^.*\n/,`Criar somente o master ICON_RESTAURANT: ${d.semantic}\n`);
  r.sha256=sha(r.output_path);r.source_sha256=sha(r.source_editable);
  r.repair={reason:'Corrigir separadores numéricos nos caminhos quadráticos do SVG para restaurar os três dentes, haste do garfo e cabo da faca.',
    before,after:{sha256:r.sha256,source_sha256:r.source_sha256},source:'art-source/editable/ART-06/repair-restaurant.cjs'};
  a.sha256=r.sha256;a.generation_prompt=r.final_prompt;
  write('docs/art/manifest.json',m);write('docs/art/prompts/ART-06.json',p);
  console.log(JSON.stringify({repaired_unvalidated_asset:'ICON_RESTAURANT',dimensions_unchanged:true}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
