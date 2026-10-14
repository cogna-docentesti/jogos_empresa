/* Targeted repair of generated, unvalidated scrollbar sources. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const write=(p,d)=>fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n','utf8');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
async function main(){
  const m=read('docs/art/manifest.json'),p=read('docs/art/prompts/ART-05.json');
  const initial=read('docs/art/qa/ART-05-technical.json');
  if(initial.passed)throw new Error('Repair expected initial alpha failure');
  write('docs/art/qa/ART-05-initial-alpha-findings.json',initial);
  for(const [id,radius] of [['UI_SCROLLBAR_TRACK',2],['UI_SCROLLBAR_THUMB',1]]){
    const a=m.assets.find(a=>a.id===id),r=p.assets.find(a=>a.id===id);
    if(a.status!=='gerado'||r.status!=='gerado'||sha(r.output_path)!==r.sha256)throw new Error(`Protected or changed asset: ${id}`);
    const before={sha256:r.sha256,source_sha256:r.source_sha256,design:{...r.design},prompt:r.final_prompt};
    Object.assign(r.design,{radius,inset:1});
    r.design.note=id==='UI_SCROLLBAR_TRACK'
      ?'Trilho vertical independente, margem externa transparente de 1 px e cantos contidos nas bordas 4 px para ampliar até 20×640 sem emendas.'
      :'Puxador vertical independente em violeta, margem externa transparente de 1 px e cantos contidos nas bordas 3 px para ampliar até 16×140.';
    const d=r.design,[w,h]=a.export_size_px,b=d.border,s=d.inset;
    const clip=`<rect x="${s}" y="${s}" width="${w-2*s}" height="${h-2*s}" rx="${radius}"/>`;
    const line=`<rect x="${s+b/2}" y="${s+b/2}" width="${w-2*s-b}" height="${h-2*s-b}" rx="${radius-b/2}" fill="none" stroke="${d.stroke}" stroke-width="${b}"/>`;
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><defs><clipPath id="silhouette">${clip}</clipPath></defs><g clip-path="url(#silhouette)"><rect width="${w}" height="${h}" fill="${d.fill}"/>${line}</g></svg>\n`;
    fs.writeFileSync(path.join(root,a.source_editable),svg,'utf8');
    await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(path.join(root,r.output_path));
    const lines=r.final_prompt.split('\n');
    lines[1]=d.note;
    lines[2]=`Preenchimento uniforme ${d.fill}; contorno interno ${d.stroke} de ${b} px; raio externo dos cantos ${radius} px; margem externa ${s} px.`;
    r.final_prompt=lines.join('\n');r.sha256=sha(r.output_path);r.source_sha256=sha(a.source_editable);
    r.repair={reason:'Cantos com antialias sem pixels de alfa zero na primeira exportação; acrescentar margem externa transparente e manter curvatura dentro das faixas sliced.',
      before,after:{sha256:r.sha256,source_sha256:r.source_sha256},source:'art-source/editable/ART-05/repair-scrollbar-alpha.cjs'};
    a.sha256=r.sha256;a.generation_prompt=r.final_prompt;
  }
  write('docs/art/manifest.json',m);write('docs/art/prompts/ART-05.json',p);
  console.log(JSON.stringify({repaired_unvalidated_assets:2,scope:'scrollbar only',dimensions_unchanged:true}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
