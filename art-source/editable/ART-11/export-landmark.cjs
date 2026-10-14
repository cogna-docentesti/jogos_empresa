/* Technical placement only: one integrated ImageGen source -> one aligned 1920x1080 RGBA layer. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const id=process.argv[2],config=read('art-source/editable/ART-11/placements.json').assets.find(a=>a.id===id);
const contract=read('docs/art/layouts/map-layout.json');
const manifest=read('docs/art/manifest.json');
const asset=manifest.assets.find(a=>a.id===id&&a.task_id==='ART-11');
const zone=contract.zones.find(z=>z.landmark_asset_id===id);
function inside(point,polygon){
  const [x,y]=point;let yes=false;
  for(let i=0,j=polygon.length-1;i<polygon.length;j=i++){
    const [xi,yi]=polygon[i],[xj,yj]=polygon[j];
    const cross=(x-xi)*(yj-yi)-(y-yi)*(xj-xi);
    if(Math.abs(cross)<1e-6&&x>=Math.min(xi,xj)&&x<=Math.max(xi,xj)&&y>=Math.min(yi,yj)&&y<=Math.max(yi,yj))return true;
    if(((yi>y)!==(yj>y))&&(x<(xj-xi)*(y-yi)/(yj-yi)+xi))yes=!yes;
  }
  return yes;
}
async function main(){
  if(!asset||!zone||!config)throw new Error('Missing ART-11 asset/zone/placement.');
  const output=path.join(root,manifest.asset_root,asset.path);
  if(fs.existsSync(output)&&!process.argv.includes('--replace-generated'))throw new Error('Output exists: preserve validated art.');
  if(asset.status==='validado')throw new Error('Never replace validated art.');
  const input=path.isAbsolute(config.source_path)?config.source_path:path.join(root,config.source_path),meta=await sharp(input).metadata();
  if(meta.channels!==4)throw new Error('Source has no real alpha; repair via ImageGen.');
  const {data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let minX=info.width,minY=info.height,maxX=-1,maxY=-1;
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>=128){
    minX=Math.min(minX,x);minY=Math.min(minY,y);maxX=Math.max(maxX,x);maxY=Math.max(maxY,y);
  }
  if(maxX<0)throw new Error('Invisible source.');
  const visible=[minX,minY,maxX+1,maxY+1],anchor=config.source_ground_anchor_px,target=zone.building_ground_anchor_px;
  const [ex,ey,ew,eh]=zone.building_envelope_px;
  let scale=Math.min(config.max_visible_size_px[0]/(visible[2]-visible[0]),config.max_visible_size_px[1]/(visible[3]-visible[1]));
  const transform=(p,s)=>[target[0]+(p[0]-anchor[0])*s,target[1]+(p[1]-anchor[1])*s];
  const valid=s=>{
    const a=transform([visible[0],visible[1]],s),b=transform([visible[2],visible[3]],s);
    return a[0]>=ex&&a[1]>=ey&&b[0]<=ex+ew&&b[1]<=ey+eh
      &&config.source_ground_polygons_px.every(poly=>poly.every(p=>inside(transform(p,s),zone.polygon_px)));
  };
  let steps=0;while(!valid(scale)&&steps++<1500)scale*=0.998;
  if(!valid(scale)||scale<0.015)throw new Error('No acceptable uniform placement; review source footprint.');
  // Integer raster dimensions are the closest representation of one uniform scale.
  const width=Math.round(meta.width*scale),height=Math.round(meta.height*scale);
  const rasterScale=[width/meta.width,height/meta.height];
  const left=Math.round(target[0]-anchor[0]*rasterScale[0]),top=Math.round(target[1]-anchor[1]*rasterScale[1]);
  const source=await sharp(input).resize({width,height,kernel:'lanczos3'}).toColourspace('srgb').ensureAlpha().png().toBuffer();
  const cropLeft=Math.max(0,-left),cropTop=Math.max(0,-top);
  const cropWidth=Math.min(width-cropLeft,1920-Math.max(0,left)),cropHeight=Math.min(height-cropTop,1080-Math.max(0,top));
  if(cropWidth<=0||cropHeight<=0)throw new Error('Source outside frame.');
  const composited=await sharp(source).extract({left:cropLeft,top:cropTop,width:cropWidth,height:cropHeight}).png().toBuffer();
  fs.mkdirSync(path.dirname(output),{recursive:true});
  await sharp({create:{width:1920,height:1080,channels:4,background:{r:0,g:0,b:0,alpha:0}}})
    .composite([{input:composited,left:Math.max(0,left),top:Math.max(0,top)}])
    .toColourspace('srgb').withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(output);
  const polygonFinal=config.source_ground_polygons_px.map(poly=>poly.map(([x,y])=>[left+x*rasterScale[0],top+y*rasterScale[1]]));
  const envelopeFinal=[left+visible[0]*rasterScale[0],top+visible[1]*rasterScale[1],left+visible[2]*rasterScale[0],top+visible[3]*rasterScale[1]];
  if(!polygonFinal.every(poly=>poly.every(p=>inside(p,zone.polygon_px))))throw new Error('Raster rounding moved a ground point outside the zone.');
  const finalMeta=await sharp(output).metadata();
  if(finalMeta.width!==1920||finalMeta.height!==1080||finalMeta.channels!==4)throw new Error('Invalid export.');
  const record={id,catalog_id:asset.catalog_id,input_path:input,input_sha256:hash(fs.readFileSync(input)),input_size_px:[meta.width,meta.height],
    output_path:`${manifest.asset_root}/${asset.path}`,output_sha256:hash(fs.readFileSync(output)),actual_size_px:[1920,1080],mode:'RGBA',color_space:'sRGB',
    source_visible_bbox_alpha_128_px:visible,source_ground_anchor_px:anchor,source_ground_polygons_px:config.source_ground_polygons_px,
    target_ground_anchor_px:target,nominal_uniform_scale:scale,raster_scale_due_to_rounding:rasterScale,raster_translation_px:[left,top],
    final_visible_bbox_px:envelopeFinal,final_ground_polygons_px:polygonFinal,
    frame_edge_clip_px:[cropLeft,cropTop,width-cropLeft-cropWidth,height-cropTop-cropHeight],
    contract:'docs/art/layouts/map-layout.json',contract_sha256:hash(fs.readFileSync(path.join(root,'docs/art/layouts/map-layout.json'))),
    zone_polygon_px:zone.polygon_px,building_envelope_px:zone.building_envelope_px,pivot:[0,1],position_px:[0,0],scale:1,
    operation:'Uniform technical resampling and placement of isolated source; preserve drawn art and original alpha. No paint, mask, background removal, warp or independent x/y stretch.',
    footprint_note:config.footprint_note,renderer_versions:sharp.versions};
  const targetRecord=path.join(root,`docs/art/qa/ART-11-export-${id}.json`);
  fs.writeFileSync(targetRecord,JSON.stringify(record,null,2)+'\n','utf8');
  console.log(JSON.stringify({id,actual_size_px:[1920,1080],uniform_scale:scale,visible_bbox:envelopeFinal,sha256:record.output_sha256}));
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
