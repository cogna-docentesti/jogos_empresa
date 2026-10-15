// QA previews only. All final sprites stay separate and unchanged.
const fs=require('node:fs'),path=require('node:path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..'),base='art-source/previews/ART-29',file=p=>path.join(root,p);
const read=p=>JSON.parse(fs.readFileSync(file(p),'utf8')),write=(p,d)=>fs.writeFileSync(file(p),JSON.stringify(d,null,2)+'\n');
const p=read('docs/art/prompts/ART-29.json'),monthly=read('docs/art/prompts/ART-28.json'),c=read('docs/art/layouts/dre-layout.json');
const records=[...p.assets,...monthly.assets],colors=['#F5F0E8','#24113D'],png={compressionLevel:9,palette:false};
const label=(s,w,col=colors[1],size=18)=>Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="'+w+'" height="40"><text x="12" y="27" font-family="Segoe UI,Arial,sans-serif" font-size="'+size+'" fill="'+col+'">'+s+'</text></svg>');
const render=(r,w,h)=>sharp(file(r.output_path)).resize({width:w,height:h,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer();
function layer(id,x,y,scale,order){return {asset_id:id,position_px:[x,y],scale,order,pivot:[0.5,0.5]};}
function point(l,native){const r=records.find(r=>r.id===l.asset_id);return l.position_px.map((v,i)=>v+(native[i]-r.actual_size_px[i]*l.pivot[i])*l.scale);}
function quarterly(){
 const woman=layer('CH01_DISCUSSING',1460,316,0.46875,20),mentor=layer('CH04_DISCUSSING',1710,296,0.46875,21);
 const handNative=[646,514],hand=point(woman,handNative),paperNative=[209,169],paper=layer('DRE_PROP_REPORT_BLANK',0,0,0.375,10);
 paper.position_px=hand.map((v,i)=>v-(paperNative[i]-128)*paper.scale);
 const layers=[layer('DRE_ENVIRONMENT',1600,330,0.546875,0),paper,woman,mentor,layer('DRE_DESK',1600,490,0.546875,30),layer('DRE_PROP_COFFEE_CUP',1440,441,0.375,40),layer('DRE_PROP_CALCULATOR',1800,438,0.375,50)];
 const desk=layers.find(l=>l.asset_id==='DRE_DESK');
 const defs={ch01_hand:{asset_id:woman.asset_id,native_px:handNative,purpose:'Right edge of independent report meets empty pinch grip; fingers render in front of paper.'},paper_grip:{asset_id:paper.asset_id,native_px:paperNative,purpose:'Same exact canvas point as ch01_hand.'},ch04_gesture_hand:{asset_id:mentor.asset_id,native_px:[492,552],purpose:'Open explanatory hand above desk.'},ch04_rest_hand:{asset_id:mentor.asset_id,native_px:[415,716],purpose:'Lower hand contour meets the rear tabletop edge with complete fingers visible.'},desk_front_edge:{asset_id:desk.asset_id,native_px:[512,241],purpose:'Desk above seated thighs; shared untouched ART-28 prop.'},desk_back_edge:{asset_id:desk.asset_id,native_px:[512,81],purpose:'Rear tabletop depth guide.'}};
 return {task_id:'ART-29',status:'gerado',canvas_px:[1920,1080],coordinate_origin:c.coordinate_origin,camera:{...c.camera,orientation_description:'Frontal with slight descending desk view; CH01 on left looks right and CH04 on right looks left, following approved quarterly conversation.'},viewport_px:[1320,120,560,420],clip_rect_px:[1320,120,560,420],layers,anchors:Object.fromEntries(Object.entries(defs).map(([k,v])=>[k,point(layers.find(l=>l.asset_id===v.asset_id),v.native_px)])),anchor_definitions:defs,occlusion_masks:[],occlusion_strategy:'Paper behind CH01 fingers; both people behind desk so seated thighs and lower torso are hidden by tabletop. Same viewport clips lower desk legs. No bitmap masks or modifications.',display_rules:'Uniform scale only, contain/preserveAspect. Complete PNGs remain separate. Viewport crop only in composed QA.',reference:'art-source/references/concepts/05-dre-trimestral.png',shared_assets_unchanged:['DRE_ENVIRONMENT','DRE_DESK','DRE_PROP_REPORT_BLANK','DRE_PROP_COFFEE_CUP','DRE_PROP_CALCULATOR'],proof_source:'art-source/editable/ART-29/preview-art29.cjs',proof:base+'/ART-29-layer-proof.png',integration_performed:false};
}
async function pair(r,native){
 const [w,h]=native?r.actual_size_px:r.display_size_px,cw=w+32,ch=h+64,items=[],im=await render(r,w,h);
 for(let i=0;i<2;i++)items.push({input:{create:{width:cw,height:ch,channels:4,background:colors[i]}},left:i*cw,top:0},{input:label(r.id+' '+w+'x'+h,cw,i?colors[0]:colors[1],16),left:i*cw,top:0},{input:im,left:i*cw+16,top:48});
 const dest=base+'/'+r.id+(native?'-native-light-dark':'-display-light-dark')+'.png';
 await sharp({create:{width:cw*2,height:ch,channels:4,background:colors[0]}}).composite(items).png(png).toFile(file(dest));return dest;
}
async function composed(v,omit=[]){
 const pad=256,[vx,vy,w,h]=v.viewport_px||c.monthly_viewport_px,items=[];
 for(const l of [...v.layers].sort((a,b)=>a.order-b.order)){
  if(omit.includes(l.asset_id))continue;
  const r=records.find(r=>r.id===l.asset_id),[nw,nh]=r.actual_size_px,sw=Math.round(nw*l.scale),sh=Math.round(nh*l.scale);
  items.push({input:await render(r,sw,sh),left:Math.round(l.position_px[0]-nw*l.scale*l.pivot[0]-vx)+pad,top:Math.round(l.position_px[1]-nh*l.scale*l.pivot[1]-vy)+pad});
 }
 const b=await sharp({create:{width:w+pad*2,height:h+pad*2,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite(items).png(png).toBuffer();
 return sharp(b).extract({left:pad,top:pad,width:w,height:h}).png(png).toBuffer();
}
async function main(){
 fs.mkdirSync(file(base),{recursive:true});const inspected=[];
 c.quarterly=quarterly();c.updated_by_tasks=['ART-28','ART-29'];
 write('docs/art/layouts/dre-layout.json',c);
 for(const r of p.assets){r.native_preview=await pair(r,true);r.display_preview=await pair(r,false);inspected.push(r.native_preview,r.display_preview);}
 const comp=await composed(c.quarterly),noDesk=await composed(c.quarterly,['DRE_DESK','DRE_PROP_COFFEE_CUP','DRE_PROP_CALCULATOR']),monthlyComp=await composed(c);
 await sharp(comp).png(png).toFile(file(base+'/ART-29-quarterly-composite.png'));
 await sharp(monthlyComp).png(png).toFile(file(base+'/ART-29-monthly-regression.png'));
 const marks=['ch01_hand','ch04_gesture_hand','ch04_rest_hand','desk_front_edge'];
 const overlay=Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="560" height="420">'+marks.map((k,i)=>{const [x,y]=c.quarterly.anchors[k].map((v,j)=>v-[1320,120][j]);return '<circle cx="'+x+'" cy="'+y+'" r="4" fill="#F5F0E8" stroke="#6D28D9" stroke-width="2"/><text x="'+(x+(i===0?-100:-60))+'" y="'+(y-10)+'" font-family="Segoe UI" font-size="11" fill="#24113D" stroke="#F5F0E8" stroke-width="2" paint-order="stroke">'+['mão / papel','gesto mentor','mão mentor','borda frontal'][i]+'</text>';}).join('')+'</svg>');
 const annotated=await sharp(comp).composite([{input:overlay,left:0,top:0}]).png(png).toBuffer();
 const proofItems=[{input:label('ART-29 | Montagem trimestral 560x420 - contrato 1920x1080',1760),left:0,top:0}];
 [comp,annotated,noDesk].forEach((input,i)=>{proofItems.push({input,left:16+i*584,top:58},{input:label(['Conversa final (somente QA)','Âncoras e ordem de camadas','Duas pessoas e papel antes da mesa'][i],560),left:16+i*584,top:480});});
 await sharp({create:{width:1760,height:530,channels:4,background:colors[0]}}).composite(proofItems).png(png).toFile(file(base+'/ART-29-layer-proof.png'));
 const identity=[];
 for(let i=0;i<2;i++){
  const r=p.assets[i],x=i*820;
  identity.push({input:label(r.id+' | master e pose de conversa',820),left:x,top:0},{input:await sharp(file(r.canonical_identity.path)).resize({width:360,height:540,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png(png).toBuffer(),left:x+20,top:60},{input:await render(r,360,480),left:x+420,top:100});
 }
 await sharp({create:{width:1640,height:640,channels:4,background:colors[0]}}).composite(identity).png(png).toFile(file(base+'/ART-29-identity-comparison.png'));
 const canvas=await sharp({create:{width:1920,height:1080,channels:4,background:'#E7DFED'}}).composite([{input:label('ART-29 | Posição da ilustração no canvas 1920x1080 (somente QA)',1100),left:24,top:24},{input:comp,left:1320,top:120}]).png(png).toBuffer();
 await sharp(canvas).toFile(file(base+'/ART-29-canvas-proof.png'));
 p.preview_source='art-source/editable/ART-29/preview-art29.cjs';p.delivery_preview=base+'/ART-29-layer-proof.png';
 p.visual_review_files=inspected.concat([p.delivery_preview,base+'/ART-29-identity-comparison.png',base+'/ART-29-canvas-proof.png',base+'/ART-29-monthly-regression.png']);write('docs/art/prompts/ART-29.json',p);
 console.log(JSON.stringify({previews:p.visual_review_files,quarterly:c.quarterly.anchors},null,2));
}
main().catch(e=>{console.error(e);process.exit(1);});
