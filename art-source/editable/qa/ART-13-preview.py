"""Measure states, 9-slice, canonical polygon placement and joint map composition."""
from pathlib import Path
import json,hashlib,sys,io,itertools,xml.etree.ElementTree as ET
from PIL import Image,ImageDraw,ImageFont,ImageCms
import numpy as np
sys.path.insert(0,r'C:/Users/Pichau/.codex/art-runtime/python')
from jsonschema import Draft202012Validator
R=Path(__file__).resolve().parents[3];P=R/'art-source/previews/ART-13';P.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,d):(R/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rel(p):return p.relative_to(R).as_posix()
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def bg(im,color):
 out=Image.new('RGBA',im.size,color);out.alpha_composite(im);return out.convert('RGB')
def image(path):return Image.open(R/path).convert('RGBA')
def at(im,pos):
 frame=Image.new('RGBA',(1920,1080));frame.paste(im,tuple(round(n) for n in pos));return frame
def tint(im,color):
 out=Image.new('RGBA',im.size,color);out.putalpha(im.getchannel('A'));return out
def sliced(im,size,border=24):
 w,h=im.size;dw,dh=size;xs=[0,border,w-border,w];ys=[0,border,h-border,h];dx=[0,border,dw-border,dw];dy=[0,border,dh-border,dh]
 out=Image.new('RGBA',size)
 for y in range(3):
  for x in range(3):
   tile=im.crop((xs[x],ys[y],xs[x+1],ys[y+1]));tw,th=dx[x+1]-dx[x],dy[y+1]-dy[y]
   if tile.size!=(tw,th):tile=tile.resize((tw,th),Image.Resampling.BILINEAR)
   out.paste(tile,(dx[x],dy[y]))
 return out
def point_inside(x,y,poly):
 yes=np.zeros(np.broadcast(x,y).shape,dtype=bool)
 for i,(xi,yi) in enumerate(poly):
  xj,yj=poly[i-1]
  if yj==yi:continue
  yes ^= ((yi>y)!=(yj>y)) & (x < (xj-xi)*(y-yi)/(yj-yi)+xi)
 return yes
def boundary_distance(x,y,poly):
 distance=np.full(x.shape,float('inf'))
 for i,(ax,ay) in enumerate(poly):
  bx,by=poly[i-1];vx,vy=bx-ax,by-ay;t=np.clip(((x-ax)*vx+(y-ay)*vy)/(vx*vx+vy*vy),0,1)
  distance=np.minimum(distance,np.hypot(x-ax-t*vx,y-ay-t*vy))
 return distance
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)
nativefont=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',20)
layout=read('docs/art/layouts/map-layout.json');contract=read('docs/art/layouts/map-selection-compact.json');prod=read('docs/art/prompts/ART-13.json');m=read('docs/art/manifest.json')
records=prod['assets'];lookup={r['id']:r for r in records};imgs={r['id']:image(r['output_path']) for r in records};results=[];previews=[]
schema_errors=[e.message for e in Draft202012Validator(read('docs/art/schemas/map-layout.schema.json')).iter_errors(layout)]
for r in records:
 im=Image.open(R/r['output_path']);arr=np.asarray(im);alpha=arr[:,:,3];source=ET.parse(R/r['source_editable']).getroot();tags={el.tag.split('}')[-1] for el in source.iter()}
 profile=ImageCms.getProfileName(ImageCms.ImageCmsProfile(io.BytesIO(im.info['icc_profile']))).strip()
 result={'id':r['id'],'size_px':list(im.size),'bytes':(R/r['output_path']).stat().st_size,'sha256':sha(r['output_path']),
  'source_sha256':sha(r['source_editable']),'rgba':im.mode=='RGBA','srgb_profile':profile,'true_alpha_zero':bool(np.any(alpha==0)),
  'alpha_extrema':[int(alpha.min()),int(alpha.max())],'no_embedded_content_or_effects':not bool(tags & {'text','image','filter','foreignObject','linearGradient','radialGradient'}),
  'svg_size_matches':source.attrib['width']==str(im.width) and source.attrib['height']==str(im.height),
  'sha256_matches_manifest':r['sha256']==next(a['sha256'] for a in m['assets'] if a['id']==r['id'])}
 if r['design']['family']=='overlay':
  poly=layout['central_site']['polygon_px'] if r['id']=='OVERLAY_CENTER' else next(z['polygon_px'] for z in layout['zones'] if z['catalog_id']==r['catalog_id'])
  expected=' '.join(','.join(map(str,p)) for p in poly);polygons=[el for el in source.iter() if el.tag.endswith('polygon')]
  result['polygon_source_matches_canonical']=all(el.attrib['points']==expected for el in polygons) and len(polygons)==3
  result['svg_viewbox_matches_master_crop']=list(map(float,source.attrib['viewBox'].split()))==list(map(float,r['crop_rect_px']))
  result['padding_2px_clear']=bool(np.all(alpha[:2,:]==0) and np.all(alpha[-2:,:]==0) and np.all(alpha[:,:2]==0) and np.all(alpha[:,-2:]==0))
  yy,xx=np.nonzero(alpha);xx=xx+r['position_px'][0]+.5;yy=yy+r['position_px'][1]+.5;outside=~point_inside(xx,yy,poly)
  distance=boundary_distance(xx[outside],yy[outside],poly) if outside.any() else np.array([0.])
  result['max_antialias_extent_outside_polygon_px']=float(distance.max());result['confined_to_polygon_coverage']=bool(distance.max()<=.75)
  result['no_opaque_pixels']=bool(alpha.max()<200)
  result['canonical_position_matches_layer']=r['position_px']==next(l['position_px'] for l in layout['layers'] if l['asset_id']==r['id'])
  result['position_px']=r['position_px']
 else:
  result['outer_edges_clear']=bool(np.all(alpha[0,:]==0) and np.all(alpha[-1,:]==0) and np.all(alpha[:,0]==0) and np.all(alpha[:,-1]==0))
  if r['design']['family']=='label':
   display=sliced(im,(260,76));da=np.asarray(display);result['nine_slice_corner_pixels_preserved']=all(np.array_equal(np.asarray(im)[sy:sy+24,sx:sx+24],da[dy:dy+24,dx:dx+24]) for sx,sy,dx,dy in [(0,0,0,0),(232,0,236,0),(0,104,0,52),(232,104,236,52)])
   result['flat_center_no_seam']=bool(np.all(da[26:50,26:234]==da[38,130]))
   display.save(P/(r['id']+'-9slice-260x76.png'))
 for color,label in [('#F5F0E8','light'),('#24113D','dark')]:
  detail=P/(r['id']+'-native-'+label+'.png');bg(im,color).save(detail);previews.append(rel(detail))
 checks=[v for k,v in result.items() if isinstance(v,bool)];result['passed']=all(checks) and 'srgb' in profile.lower();results.append(result)
states=[]
for family,ids in [('pin',['UI_PIN_NORMAL','UI_PIN_SELECTED','UI_PIN_DISABLED']),('label',['UI_MAP_LABEL_NORMAL','UI_MAP_LABEL_SELECTED','UI_MAP_LABEL_DISABLED'])]:
 masks=[np.asarray(imgs[id])[:,:,3] for id in ids]
 states.append({'family':family,'identical_alpha_masks':all(np.array_equal(masks[0],mask) for mask in masks[1:]),
  'equal_bounds_pivot_and_slice':len({(tuple(lookup[id]['actual_size_px']),tuple(lookup[id]['pivot']),tuple(lookup[id]['nine_slice_lbrt_px'])) for id in ids})==1})
# Standalone exact nominal display sizes, shared geometry and independently composed tinted icons.
components=Image.new('RGB',(1000,520),'#F5F0E8');d=ImageDraw.Draw(components)
for col,color in enumerate(['#F5F0E8','#24113D']):
 d.rectangle((col*500,0,col*500+499,519),fill=color);ink='#24113D' if col==0 else '#F5F0E8'
 d.text((col*500+18,10),'ART-13 | display nominal | fundos e contornos',font=font,fill=ink)
 for row,state in enumerate(['normal','selected','disabled']):
  sx=col*500;y=65+row*145;c=contract['marker_states'][state]
  pin=imgs['UI_PIN_'+state.upper()].resize((80,80),Image.Resampling.LANCZOS);components.paste(bg(pin,color),(sx+30,y))
  label=sliced(imgs['UI_MAP_LABEL_'+state.upper()],(260,76));tip=tint(imgs['UI_MAP_LABEL_TIP'],c['fill']).resize((20,20),Image.Resampling.LANCZOS)
  joined=Image.new('RGBA',(260,94));joined.alpha_composite(tip,(120,68));joined.alpha_composite(label);components.paste(bg(joined,color),(sx+140,y))
  d.text((sx+30,y+98),state+' | 80x80 / 260x76 / 20x20',font=small,fill=ink)
 cp=P/'ART-13-components-display.png';components.save(cp);previews.append(rel(cp))
# Native marker sheet (includes empty center and separate tip).
for color,label in [('#F5F0E8','light'),('#24113D','dark')]:
 overlay_sheet=Image.new('RGBA',(1300,1230),color);draw=ImageDraw.Draw(overlay_sheet);ink='#24113D' if label=='light' else '#F5F0E8'
 for i,r in enumerate(r for r in records if r['design']['family']=='overlay'):
  x=i%2*650;y=i//2*410;im=imgs[r['id']];overlay_sheet.alpha_composite(im,(x+(650-im.width)//2,y+40))
  draw.text((x+16,y+12),r['id']+' | '+str(im.width)+'x'+str(im.height)+' px',font=font,fill=ink)
 overlay_path=P/('ART-13-overlays-native-'+label+'.png');overlay_sheet.convert('RGB').save(overlay_path);previews.append(rel(overlay_path))
sheet=Image.new('RGB',(900,970),'#F5F0E8');d=ImageDraw.Draw(sheet)
for i,state in enumerate(['normal','selected','disabled']):
 x=i*300;d.text((x+20,15),state,font=font,fill='#24113D');sheet.paste(bg(imgs['UI_PIN_'+state.upper()],'#F5F0E8'),(x+20,55))
 sheet.paste(bg(imgs['UI_MAP_LABEL_'+state.upper()],'#F5F0E8'),(x+20,335))
 d.text((x+20,480),'SVG / RGBA | centro vazio',font=small,fill='#24113D')
sheet.paste(bg(imgs['UI_MAP_LABEL_TIP'],'#F5F0E8'),(25,545));d.text((105,562),'Ponteira independente branca tintavel',font=font,fill='#24113D')
sheet=sheet.crop((0,0,900,650));npth=P/'ART-13-markers-native.png';sheet.save(npth);previews.append(rel(npth))
icon_manifest={a['id']:a for a in m['assets'] if a['task_id']=='ART-06'}
def assemble(selected,restaurant='MAP_REST_LANCHES',center=False,markers=True):
 out=image(layout['canonical_master']['path'])
 for l in sorted(layout['layers'],key=lambda l:l['order']):
  aid=l['asset_id']
  if l['production_task']=='ART-13':
   if aid==selected or (aid=='OVERLAY_CENTER' and center):out.alpha_composite(at(imgs[aid],l['position_px']))
  elif l['production_task']=='ART-11' or (l['production_task']=='ART-12' and aid in [restaurant,'MAP_FOREGROUND']):out.alpha_composite(at(image(l['asset_path']),l['position_px']))
 if markers:
  for node in contract['marker_instance_proposal']['nodes']:
   state='selected' if node['overlay_asset_id']==selected else 'normal';c=contract['marker_states'][state]
   pin=imgs['UI_PIN_'+state.upper()].resize((80,80),Image.Resampling.LANCZOS);origin=node['pin_top_left_master_px'];out.alpha_composite(at(pin,origin))
   icon=tint(image(m['asset_root']+'/'+icon_manifest[node['icon_asset_id']]['path']),c['icon']).resize((30,30),Image.Resampling.LANCZOS)
   out.alpha_composite(at(icon,[origin[0]+25,origin[1]+18]))
   label=sliced(imgs['UI_MAP_LABEL_'+state.upper()],(260,76));label_origin=node['label_top_left_master_px'];tip=tint(imgs['UI_MAP_LABEL_TIP'],c['fill']).resize((20,20),Image.Resampling.LANCZOS)
   out.alpha_composite(at(tip,[label_origin[0]+120,label_origin[1]+68]));out.alpha_composite(at(label,label_origin))
   text=next(z['name'] for z in layout['zones'] if z['catalog_id']==node['catalog_id']);draw=ImageDraw.Draw(out);bounds=draw.textbbox((0,0),text,font=nativefont)
   draw.text((round(label_origin[0]+130-(bounds[2]-bounds[0])/2),round(label_origin[1]+38-(bounds[3]-bounds[1])/2-bounds[1])),text,font=nativefont,fill=c['icon'])
 return out
joint=[]
for r in records:
 if r['design']['family']!='overlay':continue
 out=assemble(r['id'],center=r['id']=='OVERLAY_CENTER');
 for color,label in [('#F5F0E8','light'),('#24113D','dark')]:
  display=Image.new('RGBA',(1280,848),color);display.alpha_composite(out.resize((1280,720),Image.Resampling.LANCZOS),(0,64))
  dp=P/(r['id']+'-map-display-'+label+'.png');display.convert('RGB').save(dp);previews.append(rel(dp))
  if label=='light':joint.append((r['id'],display.convert('RGB')))
 # Inspect ground realce and its canonical coordinates in a local crop, without baked buildings in asset.
 clean=assemble(r['id'],center=r['id']=='OVERLAY_CENTER',markers=False);x,y,w,h=r['crop_rect_px']
 cx=max(0,x-25);cy=max(0,y-150);zoom=bg(clean,'#F5F0E8').crop((cx,cy,min(1920,x+w+25),min(1080,y+h+25)))
 zp=P/(r['id']+'-ground-composition.png');zoom.save(zp);previews.append(rel(zp))
montage=Image.new('RGB',(1280,1360),'#F5F0E8');d=ImageDraw.Draw(montage)
for i,(id,im) in enumerate(joint):
 x=(i%2)*640;y=(i//2)*450;montage.paste(im.resize((640,424),Image.Resampling.LANCZOS),(x,y+25));d.text((x+12,y+3),id,font=small,fill='#24113D')
mp=P/'ART-13-map-selections.png';montage.save(mp);previews.append(rel(mp))
capacity=sum(r['actual_size_px'][0]*r['actual_size_px'][1] for r in records);overlay_texels=sum(r['actual_size_px'][0]*r['actual_size_px'][1] for r in records if r['design']['family']=='overlay')
summary={'png_count':13,'svg_count':13,'png_bytes':sum(r['output_bytes'] for r in records),'total_texels':capacity,'rgba8_capacity_bytes':capacity*4,
 'overlay_texels':overlay_texels,'fullframe_overlay_texels':6*1920*1080,'overlay_texel_reduction_percent':100*(1-overlay_texels/(6*1920*1080)),
 'memory_note':'Capacidade teórica RGBA8 sem mipmaps/compressão; build e memória real dependem do import/plataforma/atlas.'}
report={'task_id':'ART-13','date':'2026-10-14','passed':all(r['passed'] for r in results) and all(s['identical_alpha_masks'] and s['equal_bounds_pivot_and_slice'] for s in states) and not schema_errors,
 'assets':results,'state_comparisons':states,'schema_valid':not schema_errors,'schema_errors':schema_errors,'summary':summary,'previews':previews,
 'active_contract_sha256':sha('docs/art/layouts/map-layout.json'),'preview_native_content_note':'Ícones ART-06 e nomes só na prova de composição; os 13 PNGs finais contêm apenas fundos/contornos/realces.',
 'visual_qa_required':True,'integration_performed':False}
write('docs/art/qa/ART-13-measurements.json',report)
print(json.dumps({'passed':report['passed'],'assets':[{k:r[k] for k in ['id','passed']} for r in results],'state_comparisons':states,'summary':summary,'schema_errors':schema_errors}));assert report['passed']
