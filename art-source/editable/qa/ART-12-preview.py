"""ART-12 export, placement and visual QA evidence. Preview compositing only."""
from pathlib import Path
import json,hashlib,sys,io,itertools
from PIL import Image,ImageDraw,ImageFont,ImageCms
import numpy as np
sys.path.insert(0,r'C:/Users/Pichau/.codex/art-runtime/python')
from jsonschema import Draft202012Validator
R=Path(__file__).resolve().parents[3];P=R/'art-source/previews/ART-12';P.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,d):(R/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def rel(p):return p.relative_to(R).as_posix()
def inside(point,polygon):
 x,y=point;yes=False
 for i,(xi,yi) in enumerate(polygon):
  xj,yj=polygon[i-1]
  if ((yi>y)!=(yj>y)) and x<(xj-xi)*(y-yi)/(yj-yi)+xi:yes=not yes
 return yes
def frame(path,pos):
 out=Image.new('RGBA',(1920,1080));out.paste(Image.open(R/path).convert('RGBA'),tuple(pos));return out
def backdrop(im,color):
 b=Image.new('RGBA',im.size,color);b.alpha_composite(im);return b.convert('RGB')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)
layout=read('docs/art/layouts/map-layout.json');m=read('docs/art/manifest.json');exports=read('docs/art/qa/ART-12-exports.json')['assets']
schema_errors=[e.message for e in Draft202012Validator(read('docs/art/schemas/map-layout.schema.json')).iter_errors(layout)]
base=Image.open(R/layout['canonical_master']['path']).convert('RGBA')
for layer in sorted(layout['layers'],key=lambda l:l['order']):
 if layer['production_task']=='ART-11':base.alpha_composite(frame(layer['asset_path'],layer['position_px']))
foreground=next(e for e in exports if e['id']=='MAP_FOREGROUND');fg=frame(foreground['output_path'],foreground['position_px']);fa=np.asarray(fg)[:,:,3]
site=layout['central_site'];env=site['building_envelope_px'];restaurants=[e for e in exports if e['id']!='MAP_FOREGROUND']
results=[];previews=[];rendered={};rasters={}
names={'MAP_REST_LANCHES':'Lanches','MAP_REST_FRANCES':'Frances','MAP_REST_JAPONES':'Japones','MAP_FOREGROUND':'Vegetacao e banco'}
for e in exports:
 im=Image.open(R/e['output_path']);arr=np.asarray(im);alpha=arr[:,:,3]
 raster=Image.open(R/e['raster_path']);rasters[e['id']]=np.asarray(raster)
 rx,ry,rw,rh=e['raster_crop_rect_px'];rebuilt=Image.new('RGBA',raster.size);rebuilt.paste(im.crop((2,2,im.width-2,im.height-2)),(rx,ry))
 ra=np.asarray(raster);rb=np.asarray(rebuilt);mask=ra[:,:,3]>0
 alpha_equal=bool(np.array_equal(ra[:,:,3],rb[:,:,3]));visible_rgba_equal=bool(np.array_equal(ra[mask],rb[mask]))
 transparent_rgb_diff=int(np.count_nonzero(ra[~mask]!=rb[~mask]))
 edges_clear=bool(np.all(alpha[:2,:]==0) and np.all(alpha[-2:,:]==0) and np.all(alpha[:,:2]==0) and np.all(alpha[:,-2:]==0))
 points_inside=all(inside(p,site['polygon_px']) for p in e['footprint_master_px'])
 visible=e['visible_bbox_master_px'];x,y,w,h=visible
 envelope_ok=e['id']=='MAP_FOREGROUND' or (x>=env[0] and y>=env[1] and x+w<=env[0]+env[2] and y+h<=env[1]+env[3])
 try:profile=ImageCms.getProfileName(ImageCms.ImageCmsProfile(io.BytesIO(im.info['icc_profile']))).strip()
 except Exception:profile=None
 detail=Image.new('RGB',(im.width*2+32,im.height+48),'#F5F0E8');d=ImageDraw.Draw(detail)
 for idx,color in enumerate(['#F5F0E8','#24113D']):detail.paste(backdrop(im,color),(idx*(im.width+16),40))
 d.text((8,8),names[e['id']]+' | '+str(im.width)+' x '+str(im.height)+' px',font=font,fill='#24113D')
 detailpath=P/(e['id']+'-native-light-dark.png');detail.save(detailpath);previews.append(rel(detailpath))
 layer=frame(e['output_path'],e['position_px']);rendered[e['id']]=layer
 result={'id':e['id'],'path':e['output_path'],'actual_size_px':list(im.size),'position_px':e['position_px'],'bytes':(R/e['output_path']).stat().st_size,
  'sha256':sha(e['output_path']),'rgba':im.mode=='RGBA','srgb_profile':profile,'has_real_zero_alpha':bool(np.any(alpha==0)),
  'alpha_extrema':[int(alpha.min()),int(alpha.max())],'transparent_padding_2px':edges_clear,'alpha_reconstruction_equal':alpha_equal,
  'all_nonzero_alpha_rgba_reconstructed_equal':visible_rgba_equal,'transparent_rgb_difference_count':transparent_rgb_diff,
  'ground_polygon_inside_central_site':points_inside,'visible_envelope_inside_contract':envelope_ok,
  'anchor_rounding_error_px':e['anchor_rounding_error_px'],'detail_preview':rel(detailpath)}
 if e['id']!='MAP_FOREGROUND':
  dx,dy=site['door_anchor_px'];result['foreground_door_clear_radius_px']=16
  result['foreground_door_clear']=bool(np.all(fa[dy-16:dy+17,dx-16:dx+17]==0))
  result['foreground_occluded_visible_pixels']=int(np.count_nonzero((fa>=128)&(np.asarray(layer)[:,:,3]>=128)))
  assembly=base.copy();assembly.alpha_composite(layer);assembly.alpha_composite(fg)
  for color,label in [('#F5F0E8','light'),('#24113D','dark')]:
   full=P/(e['id']+'-assembly-'+label+'.png');backdrop(assembly,color).save(full);previews.append(rel(full))
   display=Image.new('RGBA',(1280,848),color);display.alpha_composite(assembly.resize((1280,720),Image.Resampling.LANCZOS),(0,64))
   displaypath=P/(e['id']+'-display-'+label+'.png');display.convert('RGB').save(displaypath);previews.append(rel(displaypath))
  annotated=backdrop(assembly,'#F5F0E8');draw=ImageDraw.Draw(annotated)
  draw.polygon([tuple(p) for p in site['polygon_px']],outline='#26776A',width=3)
  draw.polygon([tuple(p) for p in e['footprint_master_px']],outline='#C55457',width=2)
  draw.rectangle((env[0],env[1],env[0]+env[2],env[1]+env[3]),outline='#6D28D9',width=2)
  draw.ellipse((dx-5,dy-5,dx+5,dy+5),fill='#6D28D9',outline='#F5F0E8',width=2)
  draw.text((dx+8,dy-18),'Porta (1280,642)',font=small,fill='#24113D')
  crop=annotated.crop((935,395,1420,752));ap=P/(e['id']+'-placement.png');crop.save(ap);previews.append(rel(ap))
  result['placement_preview']=rel(ap)
 result['passed']=all([result['rgba'],profile and 'srgb' in profile.lower(),result['has_real_zero_alpha'],edges_clear,
  alpha_equal,visible_rgba_equal,points_inside,envelope_ok,result.get('foreground_door_clear',True),
  result.get('foreground_occluded_visible_pixels',1)>0,max(abs(n) for n in e['anchor_rounding_error_px'])<=0.5])
 results.append(result)
# Native-size contact sheets on each review background; no display stretching.
for color,label in [('#F5F0E8','light'),('#24113D','dark')]:
 sheet=Image.new('RGB',(940,760),color);d=ImageDraw.Draw(sheet);ink='#24113D' if label=='light' else '#F5F0E8'
 for i,e in enumerate(exports):
  x=(i%2)*470;y=(i//2)*380;im=Image.open(R/e['output_path']);sheet.paste(backdrop(im,color),(x+(470-im.width)//2,y+50))
  d.text((x+22,y+12),names[e['id']]+' | '+str(im.width)+'x'+str(im.height)+' px',font=font,fill=ink)
  d.text((x+22,y+340),'XY '+str(e['position_px'])+' | '+format(e['output_bytes']/1000,'.1f')+' KB',font=small,fill=ink)
 cp=P/('ART-12-contact-'+label+'.png');sheet.save(cp);previews.append(rel(cp))
# Structural comparisons use solid silhouette; harmless near-transparent source residue is reported separately.
comparison=[]
for a,b in itertools.combinations(restaurants,2):
 maska=rasters[a['id']][:,:,3]>=128;maskb=rasters[b['id']][:,:,3]>=128
 intersection=int(np.count_nonzero(maska&maskb));union=int(np.count_nonzero(maska|maskb));iou=intersection/union
 comparison.append({'variants':[a['id'],b['id']],'visible_silhouette_iou':iou,'raster_origin_equal':a['raster_origin_px']==b['raster_origin_px'],
  'uniform_scale_equal':a['nominal_uniform_scale']==b['nominal_uniform_scale'],'door_registration_equal':a['raster_anchor_px']==b['raster_anchor_px'],
  'ground_footprint_equal':a['footprint_master_px']==b['footprint_master_px'],'passed':iou>=0.99})
summary={'png_count':len(exports),'png_bytes':sum(e['output_bytes'] for e in exports),'total_texels':sum(e['actual_size_px'][0]*e['actual_size_px'][1] for e in exports)}
summary.update({'rgba8_capacity_bytes':summary['total_texels']*4,'fullframe_equivalent_texels':4*1920*1080,
 'texel_reduction_percent':100*(1-summary['total_texels']/(4*1920*1080)),
 'memory_note':'Capacidade RGBA8 sem compressão/mipmaps; memória real de execução e build dependem de import/plataforma/atlas.'})
report={'task_id':'ART-12','date':'2026-10-14','passed':all(r['passed'] for r in results) and all(c['passed'] for c in comparison) and not schema_errors,
 'assets':results,'schema_valid':not schema_errors,'schema_errors':schema_errors,'variant_comparisons':comparison,'summary':summary,
 'previews':previews,'active_contract_sha256':sha('docs/art/layouts/map-layout.json'),
 'reconstruction_note':'Alfa inteiro e todos os canais de pixels com alfa não zero preservados após recorte; RGB invisível de pixels com alfa zero não é conteúdo visual.',
 'visual_qa_required':True,'integration_performed':False}
write('docs/art/qa/ART-12-measurements.json',report)
print(json.dumps({'passed':report['passed'],'assets':[{k:r[k] for k in ['id','passed','actual_size_px','visible_envelope_inside_contract','ground_polygon_inside_central_site','all_nonzero_alpha_rgba_reconstructed_equal']} for r in results],
 'variant_comparisons':comparison,'summary':summary,'schema_errors':schema_errors}));assert report['passed']
