"""Read-only QA of tintable white icon masters and 32/40/48 px previews."""
from pathlib import Path
import hashlib
import json
import math
from PIL import Image, ImageDraw, ImageFont

PROJECT=Path(__file__).resolve().parents[3]
manifest=json.loads((PROJECT/'docs/art/manifest.json').read_text(encoding='utf-8'))
production=json.loads((PROJECT/'docs/art/prompts/ART-06.json').read_text(encoding='utf-8'))
assets=[a for a in manifest['assets'] if a['task_id']=='ART-06']
records={a['id']:a for a in production['assets']}
PREVIEW=PROJECT/'art-source/previews/ART-06'
PREVIEW.mkdir(parents=True,exist_ok=True)
LIGHT,DARK,GROUND='#F5F0E8','#24113D','#D9D2E0'
FONT=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
SMALL=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)
TITLE=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',24)
R=Image.Resampling.LANCZOS

def tint(im,color):
    out=Image.new('RGBA',im.size,color);out.putalpha(im.getchannel('A'));return out

def on_background(im,color):
    out=Image.new('RGBA',im.size,color);out.alpha_composite(im);return out.convert('RGB')

def native_sizes(draw_target,im,x,y,color,background):
    draw_target.paste(Image.new('RGB',(256,80),background),(x,y))
    for col,size in enumerate([32,40,48]):
        little=im.resize((size,size),R)
        if color!='#FFFFFF':little=tint(little,color)
        layer=Image.new('RGBA',(80,80),background)
        layer.alpha_composite(little,((80-size)//2,(80-size)//2))
        draw_target.paste(layer.convert('RGB'),(x+8+col*80,y))

images={}
measurements=[]
details=[]
for a in assets:
    with Image.open(PROJECT/manifest['asset_root']/a['path']) as source:
        im=source.copy()
    assert im.mode=='RGBA' and im.size==(256,256) and im.info.get('icc_profile')
    alpha=im.getchannel('A');bbox=alpha.getbbox();hist=alpha.histogram()
    assert alpha.getextrema()==(0,255) and alpha.getpixel((0,0))==0
    assert all(rgb==(255,255,255) for r,g,b,opacity in im.getdata() if opacity>0 for rgb in [(r,g,b)]), 'Master has non-white visible pixels'
    padding=[bbox[0],256-bbox[3],256-bbox[2],bbox[1]]
    assert min(padding)>=math.ceil(256*a['padding_percent']/100), 'Insufficient clear margin'
    assert a['pivot']==[.5,.5] and a['nine_slice_lbrt_px']==[0,0,0,0] and a['display_fit']=='contain'
    images[a['id']]=im
    dark_tint=tint(im,DARK)
    assert dark_tint.getchannel('A').tobytes()==alpha.tobytes()
    detail=Image.new('RGB',(560,500),GROUND);draw=ImageDraw.Draw(detail)
    draw.text((16,10),a['id']+' | '+records[a['id']]['name'],fill=DARK,font=FONT)
    draw.text((16,40),'Master branco | export 1:1',fill=DARK,font=SMALL)
    draw.text((288,40),'Tint ameixa de QA | export 1:1',fill=DARK,font=SMALL)
    detail.paste(on_background(im,DARK),(16,64))
    detail.paste(on_background(dark_tint,LIGHT),(288,64))
    draw.text((16,338),'32 / 40 / 48 px | branco',fill=DARK,font=SMALL)
    draw.text((288,338),'32 / 40 / 48 px | tint de QA',fill=DARK,font=SMALL)
    native_sizes(detail,im,16,366,'#FFFFFF',DARK)
    native_sizes(detail,im,288,366,DARK,LIGHT)
    draw.text((16,466),'Fundos, labels e tint só no preview; PNG final é branco e transparente.',fill=DARK,font=SMALL)
    detail_path=f"art-source/previews/ART-06/{a['id']}-export-and-display.png"
    detail.save(PROJECT/detail_path);details.append(detail)
    measurements.append({
        'id':a['id'],'actual_size_px':[256,256],'mode':im.mode,'color_space':'sRGB with embedded profile',
        'sha256':hashlib.sha256((PROJECT/manifest['asset_root']/a['path']).read_bytes()).hexdigest(),
        'alpha_sha256':hashlib.sha256(alpha.tobytes()).hexdigest(),'alpha_extrema':list(alpha.getextrema()),
        'alpha_bbox':list(bbox),'transparent_fraction':hist[0]/65536,
        'clear_padding_lbrt_px':padding,'minimum_padding_percent':min(padding)/256*100,
        'visible_rgb_colors':[[255,255,255]],'tint_alpha_preserved':True,
        'display_size_px':[40,40],'checked_display_sizes_px':[32,40,48],
        'pivot':a['pivot'],'preview':detail_path
    })

assert len({m['sha256'] for m in measurements})==18, 'Duplicate icon raster'
assert len({m['alpha_sha256'] for m in measurements})==18, 'Duplicate symbol silhouette'
holes={
    'ICON_PIN':[(128,104)],'ICON_EQUIPMENT':[(128,128)],
    'ICON_CORPORATE':[(88,72),(128,112),(168,152)],'ICON_HOME':[(128,190)],
    'ICON_SETTINGS':[(88,64),(168,128),(112,192)]
}
for aid,points in holes.items():
    assert all(images[aid].getchannel('A').getpixel(p)==0 for p in points), 'Internal cutout is not transparent'
assert all(images['ICON_RESTAURANT'].getchannel('A').getpixel(p)==255 for p in [(62,48),(86,48),(110,48),(86,192),(186,192)]), 'Incomplete restaurant cutlery'

for page in range(6):
    review=Image.new('RGB',(1680,500),GROUND)
    for col in range(3):review.paste(details[page*3+col],(col*560,0))
    review.save(PREVIEW/f'ART-06-review-{page+1:02}.png')

sheet=Image.new('RGB',(1728,1220),GROUND);draw=ImageDraw.Draw(sheet)
draw.text((24,16),'ART-06 | Ícones de navegação e HUD',fill=DARK,font=TITLE)
draw.text((24,52),'18 masters brancos tintáveis | visão geral + leitura em 32, 40 e 48 px | fundos e tint apenas na QA',fill=DARK,font=FONT)
for i,a in enumerate(assets):
    x=16+(i%6)*288;y=100+(i//6)*366
    im=images[a['id']]
    draw.text((x,y),records[a['id']]['name'],fill=DARK,font=FONT)
    draw.text((x,y+24),a['id'],fill=DARK,font=SMALL)
    layer=Image.new('RGBA',(256,144),DARK)
    little=im.resize((128,128),R);layer.alpha_composite(little,(64,8))
    sheet.paste(layer.convert('RGB'),(x,y+48))
    native_sizes(sheet,im,x,y+200,'#FFFFFF',DARK)
    native_sizes(sheet,im,x,y+282,DARK,LIGHT)
sheet.save(PREVIEW/'ART-06-contact-sheet.png')
(PROJECT/'docs/art/qa/ART-06-measurements.json').write_text(json.dumps({
    'task_id':'ART-06','assets':measurements,'unique_symbols':18,
    'true_alpha_internal_cutouts':holes,'tint_master_policy':'White masters only. Plum-tinted versions exist solely in QA previews.',
    'review_pages':[f'art-source/previews/ART-06/ART-06-review-{i:02}.png' for i in range(1,7)]
},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'task_id':'ART-06','measured_assets':18,'unique_symbols':18,'white_tintable_masters':True,
                  'min_clear_padding_px':min(min(a['clear_padding_lbrt_px']) for a in measurements),
                  'checked_display_sizes_px':[32,40,48],'preview_count':25}))
