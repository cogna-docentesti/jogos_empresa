// Technical export only: uniform resampling, original alpha, tight bounds plus 2px padding.
// Art and facade changes are exclusively produced by integrated ImageGen.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
async function run(){
 const config=read('art-source/editable/ART-12/placements.json'),m=read('docs/art/manifest.json');
 const records=[];
 for(const c of config.assets){
  const a=m.assets.find(a=>a.id===c.id&&a.task_id==='ART-12');if(!a)throw Error('Wrong scope');
  const dest=path.join(root,m.asset_root,a.path);
  if(a.status==='validado')throw Error('Never overwrite validated art: '+dest);
  if(fs.existsSync(dest)&&!(a.status==='gerado'&&process.argv.includes('--replace-generated')))throw Error('Existing art: '+dest);
  const input=path.join(root,c.source_path),meta=await sharp(input).metadata();
  if(!meta.hasAlpha||meta.width!==meta.height)throw Error('Expected square RGBA source');
  const scale=c.raster_side_px/meta.width;
  const anchor=c.source_anchor_normalized.map(n=>n*meta.width);
  const origin=c.target_anchor_px.map((n,i)=>Math.round(n-anchor[i]*scale));
  const resized=await sharp(input).resize({width:c.raster_side_px,height:c.raster_side_px,kernel:'lanczos3'})
   .toColourspace('srgb').withIccProfile('srgb').png({palette:false,compressionLevel:9,adaptiveFiltering:true}).toBuffer();
  const {data,info}=await sharp(resized).raw().toBuffer({resolveWithObject:true});
  let minX=info.width,minY=info.height,maxX=-1,maxY=-1;let vminX=info.width,vminY=info.height,vmaxX=-1,vmaxY=-1;
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
   const alpha=data[(y*info.width+x)*4+3];
   if(alpha>0){minX=Math.min(minX,x);minY=Math.min(minY,y);maxX=Math.max(maxX,x);maxY=Math.max(maxY,y);}
   if(alpha>=128){vminX=Math.min(vminX,x);vminY=Math.min(vminY,y);vmaxX=Math.max(vmaxX,x);vmaxY=Math.max(vmaxY,y);}
  }
  if(maxX<0)throw Error('Invisible image');
  const crop={left:minX,top:minY,width:maxX-minX+1,height:maxY-minY+1},padding=2;
  fs.mkdirSync(path.dirname(dest),{recursive:true});
  await sharp(resized).extract(crop).extend({top:padding,bottom:padding,left:padding,right:padding,background:{r:0,g:0,b:0,alpha:0}})
   .keepMetadata().png({palette:false,compressionLevel:9,adaptiveFiltering:true}).toFile(dest);
  const size=[crop.width+4,crop.height+4],position=[origin[0]+crop.left-2,origin[1]+crop.top-2];
  if(position[0]<0||position[1]<0||position[0]+size[0]>1920||position[1]+size[1]>1080)throw Error('Outside master');
  const transformedAnchor=anchor.map((n,i)=>origin[i]+n*scale);
  const footprint=(c.source_ground_polygon_normalized||[]).map(p=>p.map((n,i)=>origin[i]+n*meta.width*scale));
  // Save the untrimmed raster outside Assets for exact reconstruction proof.
  const rasterPath='art-source/previews/ART-12/rasters/'+c.id+'.png';fs.mkdirSync(path.dirname(path.join(root,rasterPath)),{recursive:true});
  fs.writeFileSync(path.join(root,rasterPath),resized);
  const r={id:c.id,source_path:c.source_path,source_sha256:hash(fs.readFileSync(input)),source_size_px:[meta.width,meta.height],
   output_path:(m.asset_root+'/'+a.path),path:a.path,sha256:hash(fs.readFileSync(dest)),actual_size_px:size,output_bytes:fs.statSync(dest).size,
   nominal_uniform_scale:scale,raster_size_px:[c.raster_side_px,c.raster_side_px],raster_origin_px:origin,raster_path:rasterPath,raster_sha256:hash(resized),
   raster_crop_rect_px:[crop.left,crop.top,crop.width,crop.height],crop_rect_px:[...position,...size],position_px:position,pivot:[0,1],scale:1,
   transparent_padding_px:padding,visible_bbox_master_px:[origin[0]+vminX,origin[1]+vminY,vmaxX-vminX+1,vmaxY-vminY+1],
   registration_anchor_role:c.registration_anchor_role,source_anchor_px:anchor,target_anchor_px:c.target_anchor_px,
   raster_anchor_px:transformedAnchor,anchor_rounding_error_px:transformedAnchor.map((n,i)=>n-c.target_anchor_px[i]),
   footprint_master_px:footprint,common_ground_anchor_master_px:c.common_ground_anchor_master_px||null,
   local_ground_anchor_px:c.common_ground_anchor_master_px?c.common_ground_anchor_master_px.map((n,i)=>n-position[i]):null,
   local_registration_anchor_px:c.target_anchor_px.map((n,i)=>n-position[i]),layer_order:c.layer_order,
   color_space:'sRGB',mode:'RGBA',alpha:'Original ImageGen alpha preserved; no masking or threshold cleanup. Crop includes every nonzero alpha pixel.',
   operation:'One uniform resample of complete source canvas; integer placement; lossless tight crop and 2px transparent padding. No paint, warp, alpha removal or independent axis stretch.',
   versions:sharp.versions};
  records.push(r);console.log(JSON.stringify({id:c.id,size,position,bytes:r.output_bytes,anchor_error:r.anchor_rounding_error_px}));
 }
 fs.mkdirSync(path.join(root,'docs/art/qa'),{recursive:true});
 fs.writeFileSync(path.join(root,'docs/art/qa/ART-12-exports.json'),JSON.stringify({task_id:'ART-12',assets:records},null,2)+'\n');
}
run().catch(e=>{console.error(e);process.exitCode=1;});
