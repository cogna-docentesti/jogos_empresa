"""Read-only ART-11 pixel/placement QA and native-scale light/dark assembly previews."""
from pathlib import Path
import hashlib, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[3]
PREVIEW=ROOT/'art-source/previews/ART-11';PREVIEW.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def font(n):return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',n)
def save(im,name):im.save(PREVIEW/name);return 'art-source/previews/ART-11/'+name
def composite(im,bg):
    out=Image.new('RGBA',im.size,bg);out.alpha_composite(im);return out.convert('RGB')
def mask(poly):
    im=Image.new('L',(1920,1080),0);ImageDraw.Draw(im).polygon([tuple(p) for p in poly],fill=255);return np.array(im)>0
def inside(point,poly):
    x,y=point;result=False
    for i in range(len(poly)):
        xi,yi=poly[i];xj,yj=poly[i-1]
        cross=(x-xi)*(yj-yi)-(y-yi)*(xj-xi)
        if abs(cross)<1e-6 and min(xi,xj)<=x<=max(xi,xj) and min(yi,yj)<=y<=max(yi,yj):return True
        if (yi>y)!=(yj>y) and x<(xj-xi)*(y-yi)/(yj-yi)+xi:result=not result
    return result

m=read('docs/art/manifest.json');contract=read('docs/art/layouts/map-layout.json')
base=Image.open(ROOT/'Assets/Art/new/location/map/map_base.png').convert('RGBA')
selected=[a for a in m['assets'] if a['task_id']=='ART-11'];assert len(selected)==5
original={a['id']:sha(ROOT/m['asset_root']/a['path']) for a in selected}
contract_hash=sha(ROOT/'docs/art/layouts/map-layout.json')
central=mask(contract['central_site']['polygon_px'])
zone_by_id={z['landmark_asset_id']:z for z in contract['zones']}
rgb=np.array(base)[:,:,:3].astype(int)
grass=(rgb[:,:,1]>=rgb[:,:,0]-3)&(rgb[:,:,2]<rgb[:,:,1]-20)
images={};coverage=np.zeros((1080,1920),np.uint8);records=[]

for index,a in enumerate(selected,1):
    path=ROOT/m['asset_root']/a['path'];image=Image.open(path);assert image.size==(1920,1080) and image.mode=='RGBA'
    assert image.info.get('icc_profile');image=image.convert('RGBA');alpha=np.array(image.getchannel('A'))
    zone=zone_by_id[a['id']];export=read('docs/art/qa/ART-11-export-'+a['id']+'.json')
    assert export['output_sha256']==original[a['id']]==a['sha256']
    assert export['contract_sha256']==contract_hash
    assert export['position_px']==[0,0] and export['pivot']==[0,1] and export['scale']==1
    assert abs(export['raster_scale_due_to_rounding'][0]-export['raster_scale_due_to_rounding'][1])<1e-12
    visible=alpha>=128;ys,xs=np.nonzero(visible);bbox=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]
    x,y,w,h=zone['building_envelope_px']
    assert x<=bbox[0] and y<=bbox[1] and bbox[2]<=x+w and bbox[3]<=y+h,(a['id'],bbox)
    assert not np.any(alpha[central]>=16),a['id']+' overlaps central site'
    assert all(alpha[py,px]==0 for px,py in [(0,0),(1919,0),(0,1079),(1919,1079)])
    assert np.any(alpha==0) and np.any(visible)
    coverage+=visible.astype(np.uint8)
    ground=[]
    for polygon in export['final_ground_polygons_px']:
        assert all(inside(p,zone['polygon_px']) for p in polygon),a['id']+' ground outside zone'
        gm=mask(polygon);fraction=float(np.mean(grass[gm]))
        # All physical bases are on the green plot; compare ground color, not roof coverage.
        assert fraction>0.99,(a['id'],'ground intersects paving',fraction)
        ground.append({'polygon_px':polygon,'ground_on_green_plot_fraction':fraction})
    images[a['id']]=image
    detail_box=[max(0,bbox[0]-16),max(0,bbox[1]-16),min(1920,bbox[2]+16),min(1080,bbox[3]+16)]
    crop=image.crop(tuple(detail_box))
    detail=Image.new('RGB',(crop.width*2+48,crop.height+72),'#DDD5E4');d=ImageDraw.Draw(detail)
    d.text((16,12),a['id']+' | construção em 1:1 | marfim / ameixa',font=font(18),fill='#24113D')
    detail.paste(composite(crop,'#F5F0E8'),(16,52));detail.paste(composite(crop,'#24113D'),(32+crop.width,52))
    detail_path=save(detail,a['id']+'-detail-1x.png')
    for name,bg in [('light','#F5F0E8'),('dark','#24113D')]:
        save(composite(image,bg),a['id']+'-export-1x-'+name+'.png')
        display=Image.new('RGBA',(1280,848),bg)
        display.alpha_composite(image.resize((1280,720),Image.Resampling.LANCZOS),(0,64))
        save(display.convert('RGB'),a['id']+'-display-1x-'+name+'.png')
    page=Image.new('RGB',(1440,1440),'#DDD5E4');d=ImageDraw.Draw(page)
    d.text((24,20),'ART-11 | '+a['id']+' | '+a['catalog_id'],font=font(28),fill='#24113D')
    d.text((24,60),'Export 1920x1080 RGBA, pivot [0,1]; viewport 1280x848 com contain uniforme.',font=font(19),fill='#30243C')
    page.paste(detail,(24,106))
    assembly=Image.new('RGBA',(1920,1080),'#F5F0E8');assembly.alpha_composite(base);assembly.alpha_composite(image)
    page.paste(assembly.convert('RGB').resize((1344,756),Image.Resampling.LANCZOS),(48,615))
    review_path=save(page,'ART-11-review-'+str(index).zfill(2)+'.png')
    records.append({'id':a['id'],'catalog_id':a['catalog_id'],'actual_size_px':[1920,1080],'mode':'RGBA','srgb_profile_present':True,
        'sha256':original[a['id']],'alpha_extrema':[int(alpha.min()),int(alpha.max())],
        'transparent_fraction':float(np.mean(alpha==0)),'opaque_fraction':float(np.mean(alpha==255)),
        'visible_fraction_alpha_128':float(np.mean(visible)),'visible_bbox_alpha_128_px':bbox,
        'alpha_bbox':image.getchannel('A').getbbox(),'visible_inside_envelope':True,'ground_inside_zone':True,
        'ground_metrics':ground,'central_site_clear_alpha_16':True,'uniform_transform':True,
        'source_size_px':export['input_size_px'],'source_original_alpha_preserved':True,
        'display_map_size_px':[1280,720],'display_box_px':[1280,848],'display_offset_local_px':[0,64],
        'detail_preview':detail_path,'review_page':review_path,
        'light_display_preview':'art-source/previews/ART-11/'+a['id']+'-display-1x-light.png',
        'dark_display_preview':'art-source/previews/ART-11/'+a['id']+'-display-1x-dark.png'})

overlap=int(np.count_nonzero(coverage>1));assert overlap==0
assert len(next(a for a in records if a['id']=='MAP_CONDOMINIUM')['ground_metrics'])==6
previews={}
layer_order={a['asset_id']:a['order'] for a in contract['layers']}
for name,bg in [('light','#F5F0E8'),('dark','#24113D')]:
    assembly=Image.new('RGBA',(1920,1080),bg);assembly.alpha_composite(base)
    for a in sorted(selected,key=lambda a:layer_order[a['id']]):assembly.alpha_composite(images[a['id']])
    previews['assembly_master_'+name]=save(assembly.convert('RGB'),'ART-11-assembly-master-'+name+'.png')
    display=Image.new('RGBA',(1280,848),bg);display.alpha_composite(assembly.resize((1280,720),Image.Resampling.LANCZOS),(0,64))
    previews['assembly_display_'+name]=save(display.convert('RGB'),'ART-11-assembly-display-'+name+'.png')

annotated=Image.open(PREVIEW/'ART-11-assembly-master-light.png').convert('RGBA')
overlay=Image.new('RGBA',annotated.size,(0,0,0,0));d=ImageDraw.Draw(overlay)
for a in selected:
    zone=zone_by_id[a['id']];d.line([tuple(p) for p in zone['polygon_px']+[zone['polygon_px'][0]]],fill=(109,40,217,200),width=3)
    export=read('docs/art/qa/ART-11-export-'+a['id']+'.json')
    for polygon in export['final_ground_polygons_px']:
        d.polygon([tuple(p) for p in polygon],fill=(197,84,87,60),outline=(197,84,87,255),width=2)
    x,y=zone['building_ground_anchor_px'];d.ellipse((x-5,y-5,x+5,y+5),fill=(197,84,87,255))
annotated.alpha_composite(overlay)
previews['footprint_assembly']=save(annotated.convert('RGB'),'ART-11-footprint-assembly.png')

sheet=Image.new('RGB',(1440,1860),'#DDD5E4');d=ImageDraw.Draw(sheet)
d.text((28,20),'ART-11 | Construções e bairros do mapa',font=font(30),fill='#24113D')
d.text((28,64),'5 PNGs separados | 1920x1080 RGBA | mesma origem, câmera, luz e pivot do ART-10',font=font(21),fill='#30243C')
sheet.paste(Image.open(PREVIEW/'ART-11-assembly-master-light.png').resize((1344,756),Image.Resampling.LANCZOS),(48,112))
sheet.paste(Image.open(PREVIEW/'ART-11-assembly-display-dark.png').resize((640,424),Image.Resampling.LANCZOS),(48,911))
d.text((738,930),'Universidade, banco, comércio,',font=font(26),fill='#24113D')
d.text((738,976),'bairro residencial e área corporativa.',font=font(26),fill='#24113D')
d.text((738,1047),'Restaurante central reservado para ART-12.',font=font(21),fill='#30243C')
d.text((738,1090),'Cinco camadas full-frame com alfa real.',font=font(21),fill='#30243C')
d.text((738,1133),'Nenhum label, pino ou realce no bitmap.',font=font(21),fill='#30243C')
d.text((738,1176),'Footprints nos gramados; vias legíveis.',font=font(21),fill='#30243C')
sheet.paste(annotated.convert('RGB').resize((800,450),Image.Resampling.LANCZOS),(48,1370))
d.text((900,1410),'Polígonos roxos:',font=font(24),fill='#6D28D9')
d.text((900,1450),'zonas do contrato ART-10.',font=font(21),fill='#30243C')
d.text((900,1510),'Polígonos coral:',font=font(24),fill='#C55457')
d.text((900,1550),'contato físico das construções.',font=font(21),fill='#30243C')
d.text((900,1620),'As marcações aparecem',font=font(21),fill='#30243C')
d.text((900,1658),'somente na prova de QA.',font=font(21),fill='#30243C')
previews['contact_sheet']=save(sheet,'ART-11-contact-sheet.png')

assert all(sha(ROOT/m['asset_root']/a['path'])==original[a['id']] for a in selected)
report={'task_id':'ART-11','date':'2026-10-14','passed':True,'asset_count':5,'assets':records,
    'coordinate_contract':'docs/art/layouts/map-layout.json','contract_sha256':contract_hash,
    'contract_geometry_unchanged':True,'central_site_clear':True,'layers_nonoverlapping':True,'overlap_pixels_alpha_128':overlap,
    'all_ground_polygons_on_green_plots':True,'residential_house_count':6,
    'uniform_scale_origin_pivot':True,'previews':previews,'production_pixels_unchanged_by_qa':True,
    'ground_color_check_note':'A base aprovada tem gramados/arbustos verdes e pavimento marfim/cinza. Essa conferência de pixels complementa a revisão visual; não substitui arquitetura/perspectiva ou prova de montagem.',
    'alpha_note':'Preservado o alfa original da fonte. Silhuetas visíveis são medidas com alpha>=128 e terreno central com alpha>=16; pixels residuais fracos ficam registrados no alpha_bbox completo.'}
(ROOT/'docs/art/qa/ART-11-measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'task_id':'ART-11','passed':True,'asset_count':5,'overlap_pixels':overlap,'central_clear':True,'all_ground_on_green_plots':True,'preview_count':len(previews)}))
