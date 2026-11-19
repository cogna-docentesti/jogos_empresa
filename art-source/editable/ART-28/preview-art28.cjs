// QA only: composite untouched sprites using the exact dre-layout contract.
const fs=require('node:fs'),path=require('node:path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),base='art-source/previews/ART-28',colors=['#F5F0E8','#24113D'];
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const p=read('docs/art/prompts/ART-28.json'),c=read('docs/art/layouts/dre-layout.json');
const file=p=>path.join(root,p),png={compressionLevel:9,palette:false};
const label=(s,w,col='#30243C',size=20)=>Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="'+w+'" height="42"><text x="12" y="28" font-family="Segoe UI,Arial,sans-serif" font-size="'+size+'" fill="'+col+'">'+s.replace(/&/g,'&amp;').replace(/</g,'&lt;')+'</text></svg>');
const render=(r,w,h)=>sharp(file(r.output_path)).resize({width:w,height:h,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer();
async function pair(r,native){
 const [w,h]=native?r.actual_size_px:r.display_size_px,cw=w+32,ch=h+64,items=[],im=await render(r,w,h);
 for(let i=0;i<2;i++){items.push({input:{create:{width:cw,height:ch,channels:4,background:colors[i]}},left:i*cw,top:0},{input:label(r.id+' '+w+'x'+h,cw,i?colors[0]:'#30243C',16),left:i*cw,top:0},{input:im,left:i*cw+16,top:48});}
 const dest=base+'/'+r.id+(native?'-native-light-dark':'-display-light-dark')+'.png';
 await sharp({create:{width:cw*2,height:ch,channels:4,background:colors[0]}}).composite(items).png(png).toFile(file(dest));return dest;
}
async function composed(omit=[]){
 const pad=256,W=560+pad*2,H=420+pad*2,items=[];
 for(const l of [...c.layers].sort((a,b)=>a.order-b.order)){
  if(omit.includes(l.asset_id))continue;
  const r=p.assets.find(a=>a.id===l.asset_id),w=Math.round(r.actual_size_px[0]*l.scale),h=Math.round(r.actual_size_px[1]*l.scale);
  const x=Math.round(l.position_px[0]-r.actual_size_px[0]*l.scale*l.pivot[0]-1320)+pad,y=Math.round(l.position_px[1]-r.actual_size_px[1]*l.scale*l.pivot[1]-120)+pad;
  items.push({input:await render(r,w,h),left:x,top:y});
 }
 return sharp({create:{width:W,height:H,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite(items).png(png).toBuffer().then(b=>sharp(b).extract({left:pad,top:pad,width:560,height:420}).png(png).toBuffer());
}
async function main(){
 fs.mkdirSync(file(base),{recursive:true});const inspected=[];
 for(const r of p.assets){r.native_preview=await pair(r,true);r.display_preview=await pair(r,false);inspected.push(r.native_preview,r.display_preview);}
 const comp=await composed(),noDesk=await composed(['DRE_DESK','DRE_PROP_COFFEE_CUP','DRE_PROP_CALCULATOR']);
 await sharp(comp).png(png).toFile(file(base+'/ART-28-monthly-composite.png'));
 const overlay=Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="560" height="420">'+[['mão / papel',...c.anchors.ch01_hand],['borda frontal',...c.anchors.desk_front_edge]].map(([s,x,y])=>'<circle cx="'+(x-1320)+'" cy="'+(y-120)+'" r="5" fill="#F5F0E8" stroke="#6D28D9" stroke-width="2"/><text x="'+(x-1320+10)+'" y="'+(y-120-8)+'" font-family="Segoe UI" font-size="12" fill="#24113D" stroke="#F5F0E8" stroke-width="2" paint-order="stroke">'+s+'</text>').join('')+'</svg>');
 const annotated=await sharp(comp).composite([{input:overlay,left:0,top:0}]).png(png).toBuffer();
 const proofLayers=[{input:label('ART-28 | Montagem mensal 560x420 - contrato 1920x1080',1760),left:0,top:0},{input:comp,left:16,top:58},{input:annotated,left:600,top:58},{input:noDesk,left:1184,top:58},{input:label('Montagem final (somente QA)',560),left:16,top:480},{input:label('Âncoras mão / folha / mesa',560),left:600,top:480},{input:label('Pessoa e papel antes da mesa',560),left:1184,top:480}];
 await sharp({create:{width:1760,height:530,channels:4,background:colors[0]}}).composite(proofLayers).png(png).toFile(file(base+'/ART-28-layer-proof.png'));
 const contact=[],width=1760;
 for(let i=0;i<6;i++){
  const r=p.assets[i],[w,h]=r.display_size_px,x=i%3*586,y=Math.floor(i/3)*600;
  contact.push({input:label(r.id,586),left:x,top:y});
  for(let j=0;j<2;j++){const ch=250,sy=y+42+j*270;contact.push({input:{create:{width:570,height:ch,channels:4,background:colors[j]}},left:x+8,top:sy});const im=await render(r,Math.round(w*Math.min(1,230/h)),Math.round(h*Math.min(1,230/h)));const md=await sharp(im).metadata();contact.push({input:im,left:x+8+Math.floor((570-md.width)/2),top:sy+Math.floor((ch-md.height)/2)});}
 }
 await sharp({create:{width,height:1200,channels:4,background:'#E7DFED'}}).composite(contact).png(png).toFile(file(base+'/ART-28-contact-sheet.png'));
 const master=await sharp(file('Assets/Art/new/characters/CH01/ch01_canonical.png')).resize({width:360,height:540,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer();
 const actor=await render(p.assets.find(r=>r.id==='CH01_DRE'),360,480);
 await sharp({create:{width:840,height:650,channels:4,background:colors[0]}}).composite([{input:label('CH01 | Master canônico e pose DRE',840),left:0,top:0},{input:master,left:20,top:60},{input:actor,left:440,top:60},{input:label('Rosto, pele, cabelo, argolas e figurino preservados',840),left:0,top:602}]).png(png).toFile(file(base+'/ART-28-identity-comparison.png'));
 p.preview_source='art-source/editable/ART-28/preview-art28.cjs';p.delivery_preview=base+'/ART-28-layer-proof.png';p.visual_review_files=inspected.concat([base+'/ART-28-layer-proof.png',base+'/ART-28-contact-sheet.png',base+'/ART-28-identity-comparison.png']);fs.writeFileSync(file('docs/art/prompts/ART-28.json'),JSON.stringify(p,null,2)+'\n');
 console.log(JSON.stringify({previews:p.visual_review_files}));
}
main().catch(e=>{console.error(e);process.exit(1);});
