/* Lossless technical crop of approved full-frame PNGs. Artwork, scale and alpha remain unchanged. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const write=(p,d)=>{const target=path.join(root,p);fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,JSON.stringify(d,null,2)+'\n','utf8');};
async function main(){
  const snapshot=read('docs/art/qa/ART-11-compact-baseline.json');
  const layout=read('docs/art/layouts/archive/map-layout-art11-fullframe.json');
  const records=[];
  for(const asset of snapshot.fullframe_assets){
    const input=path.join(root,'Assets/Art/new',asset.path),beforeHash=hash(fs.readFileSync(input));
    if(beforeHash!==asset.sha256)throw new Error('Validated source changed: '+asset.id);
    const {data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    if(info.width!==1920||info.height!==1080||info.channels!==4)throw new Error('Unexpected full-frame source.');
    let x0=info.width,y0=info.height,x1=0,y1=0;
    // Include EVERY nonzero-alpha pixel, including weak residual edge pixels.
    for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>0){
      x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x+1);y1=Math.max(y1,y+1);
    }
    if(x1<=x0||y1<=y0)throw new Error('Invisible source.');
    const alphaBounds=[x0,y0,x1,y1],padding=2;
    x0=Math.max(0,x0-padding);y0=Math.max(0,y0-padding);x1=Math.min(info.width,x1+padding);y1=Math.min(info.height,y1+padding);
    const width=x1-x0,height=y1-y0;
    const relative='location/landmarks/compact/'+path.basename(asset.path),output=path.join(root,'Assets/Art/new',relative);
    if(fs.existsSync(output))throw new Error('Compact output exists; do not overwrite reviewed art.');
    fs.mkdirSync(path.dirname(output),{recursive:true});
    await sharp(input).extract({left:x0,top:y0,width,height}).keepMetadata()
      .png({palette:false,compressionLevel:9,adaptiveFiltering:true}).toFile(output);
    const final=await sharp(output).metadata();
    if(final.width!==width||final.height!==height||final.channels!==4||!final.icc)throw new Error('Invalid compact export.');
    const zone=layout.zones.find(z=>z.landmark_asset_id===asset.id),s=layout.display_transform.uniform_scale;
    const position=[x0,y0],ground=zone.building_ground_anchor_px;
    records.push({id:asset.id,catalog_id:asset.catalog_id,source_path:'Assets/Art/new/'+asset.path,source_sha256:beforeHash,
      source_size_px:[1920,1080],source_bytes:fs.statSync(input).size,
      output_path:'Assets/Art/new/'+relative,path:relative,sha256:hash(fs.readFileSync(output)),output_bytes:fs.statSync(output).size,
      actual_size_px:[width,height],mode:'RGBA',color_space:'sRGB',crop_rect_px:[x0,y0,width,height],source_alpha_bbox:alphaBounds,
      padding_px:padding,position_px:position,pivot:[0,1],scale:1,master_size_px:[1920,1080],coordinate_origin:'top_left_y_down',
      ground_anchor_master_px:ground,ground_anchor_local_px:[ground[0]-x0,ground[1]-y0],
      ground_polygons_master_px:read('docs/art/qa/ART-11-export-'+asset.id+'.json').final_ground_polygons_px,
      unity_master_rect:{anchor_min:[0,1],anchor_max:[0,1],pivot:[0,1],anchored_position_px:[x0,-y0],size_delta_px:[width,height]},
      viewport_display_rect:{top_left_px:[x0*s,layout.display_transform.viewport_local_offset_px[1]+y0*s],size_px:[width*s,height*s],
        unity_anchored_position_px:[x0*s,-(layout.display_transform.viewport_local_offset_px[1]+y0*s)]},
      canvas_display_rect:{top_left_px:[layout.display_transform.offset_px[0]+x0*s,layout.display_transform.offset_px[1]+y0*s],size_px:[width*s,height*s]},
      center_pivot_master_option:{anchor_min:[0,1],anchor_max:[0,1],pivot:[0.5,0.5],anchored_position_px:[x0+width/2,-(y0+height/2)]},
      rgba8_bytes_before:1920*1080*4,rgba8_bytes_after:width*height*4,status:'gerado'});
  }
  const sum=k=>records.reduce((total,a)=>total+a[k],0);
  const result={task_id:'ART-11',revision:'compact',date:'2026-10-14',status:'gerado',tool:'sharp PNG lossless crop',
    original_art_tool:'image_gen.imagegen',new_image_generation:false,model:null,
    request:'Exportar apenas as construções, removendo o canvas vazio, e registrar a posição exata de encaixe no mapa.',
    operation:'Extract only; no resize, redraw, mask, alpha cleanup, quantization or new ImageGen call. Preserve all nonzero-alpha pixels and a 2px transparent border.',
    source_production_record:'docs/art/prompts/ART-11.json',source_contract:'docs/art/layouts/archive/map-layout-art11-fullframe.json',
    export_source:'art-source/editable/ART-11/crop-compact.cjs',renderer_versions:sharp.versions,assets:records,
    summary:{png_bytes_before:sum('source_bytes'),png_bytes_after:sum('output_bytes'),rgba8_bytes_before:sum('rgba8_bytes_before'),rgba8_bytes_after:sum('rgba8_bytes_after'),
      texel_reduction_percent:100*(1-sum('rgba8_bytes_after')/sum('rgba8_bytes_before')),
      memory_note:'RGBA8 uncompressed pixel capacity, without mipmaps. Actual app memory/build size depends on import/platform/atlas settings; integration is not applied.'},
    integration_performed:false};
  write('docs/art/prompts/ART-11-compact.json',result);
  write('docs/art/layouts/map-landmarks-compact.json',{schema_version:1,task_id:'ART-11',revision:'compact',date:'2026-10-14',
    master_size_px:[1920,1080],coordinate_origin:'top_left_y_down',rect_semantics:'[x,y,width,height] in master pixels. Position is the top-left of the cropped texture.',
    display_transform:layout.display_transform,pivot:[0,1],scale:1,assets:records,
    assembly_rule:'Place each compact PNG at position_px, at native size, under the same 1920x1080 map parent. Then apply the shared display_transform once to the parent.',
    unity_rule:'For a master-space RectTransform with top-left anchors and pivot [0,1], anchoredPosition=[x,-y] and sizeDelta=texture size. Values for the 1280x848 viewport are separately recorded; do not mix coordinate spaces.',
    source_contract:'docs/art/layouts/archive/map-layout-art11-fullframe.json',integration_performed:false});
  console.log(JSON.stringify(result.summary));
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
