"""Measure original ART-09 PNGs; all compositing and resizing here is QA only."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageOps

PROJECT=Path(__file__).resolve().parents[3]
manifest=json.loads((PROJECT/'docs/art/manifest.json').read_text(encoding='utf-8'))
production=json.loads((PROJECT/'docs/art/prompts/ART-09.json').read_text(encoding='utf-8'))
assets=[a for a in manifest['assets'] if a['task_id']=='ART-09']
records={a['id']:a for a in production['assets']}
by_id={a['id']:a for a in assets}
PREVIEW=PROJECT/'art-source/previews/ART-09'
PREVIEW.mkdir(parents=True,exist_ok=True)
LIGHT,DARK,GROUND='#F5F0E8','#24113D','#D9D2E0'
FONT=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
SMALL=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)
TITLE=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',24)
R=Image.Resampling.LANCZOS
GROUPS={
    'hud':['UI_HUD_PILL','UI_KPI_TILE'],
    'progress':['UI_PROGRESS_TRACK','UI_PROGRESS_FILL','UI_NAV_UNDERLINE'],
    'pill':['UI_PILL_NORMAL','UI_PILL_SELECTED','UI_PILL_DISABLED'],
    'toast':['UI_TOAST_NEUTRAL','UI_TOAST_SUCCESS','UI_TOAST_WARNING'],
    'table_rows':['UI_TABLE_HEADER','UI_TABLE_ROW_NEUTRAL','UI_TABLE_ROW_SUBTOTAL','UI_TABLE_ROW_POSITIVE','UI_TABLE_ROW_NEGATIVE'],
    'table_total':['UI_TABLE_TOTAL_COLUMN'],
    'chart':['UI_CHART_PANEL']
}

def background(im,color):
    out=Image.new('RGBA',im.size,color);out.alpha_composite(im);return out.convert('RGB')

def sliced(im,size,borders,pixel_caps=False):
    l,b,r,t=borders;w,h=im.size;W,H=size
    assert W>l+r and H>b+t
    sx,sy=[0,l,w-r,w],[0,t,h-b,h]
    tx,ty=[0,l,W-r,W],[0,t,H-b,H]
    out=Image.new('RGBA',size)
    for row in range(3):
        for col in range(3):
            part=im.crop((sx[col],sy[row],sx[col+1],sy[row+1]))
            target=(tx[col+1]-tx[col],ty[row+1]-ty[row])
            if part.size!=target:part=part.resize(target,R)
            out.paste(part,(tx[col],ty[row]))
    corners=[((0,0,l,t),(0,0,l,t)),((w-r,0,w,t),(W-r,0,W,t)),
             ((0,h-b,l,h),(0,H-b,l,H)),((w-r,h-b,w,h),(W-r,H-b,W,H))]
    assert all(im.crop(s).tobytes()==out.crop(d).tobytes() for s,d in corners)
    if pixel_caps:
        assert borders==[1,1,1,1]
        assert all(out.getpixel(p)[3]==0 for p in [(0,0),(W-1,0),(0,H-1),(W-1,H-1)])
        assert all(out.getpixel(p)[3]==255 for p in [(1,0),(W-2,0),(0,1),(W-1,H-2)])
    else:
        for s,d in [((l-1,0,l,h),(l,0,l+1,h)),((w-r-1,0,w-r,h),(w-r,0,w-r+1,h)),
                    ((0,t-1,w,t),(0,t,w,t+1)),((0,h-b-1,w,h-b),(0,h-b,w,h-b+1))]:
            assert im.crop(s).tobytes()==im.crop(d).tobytes(), 'Slice cut crosses nonuniform geometry'
    return out

def single_display(im,aid):
    w,h=im.size
    out=Image.new('RGB',(max(560,w+48),h*2+128),GROUND);draw=ImageDraw.Draw(out)
    for i,(label,color) in enumerate([('Claro',LIGHT),('Escuro',DARK)]):
        y=16+i*(h+64)
        draw.text((24,y),aid+' | '+label+' | display 1:1',fill=DARK,font=FONT)
        out.paste(background(im,color),(24,y+32))
    return out

exported={}
shown={}
details=[]
measurements=[]
for a in assets:
    with Image.open(PROJECT/manifest['asset_root']/a['path']) as source:im=source.copy()
    d=records[a['id']]['design'];alpha=im.getchannel('A')
    assert im.mode=='RGBA' and im.size==tuple(a['export_size_px']) and im.info.get('icc_profile')
    assert alpha.getextrema()==(0,255) and alpha.getpixel((0,0))==0
    expected=tuple(int(d['fill'][i:i+2],16) for i in [1,3,5])
    assert im.getpixel((im.width//2,im.height//2))==(*expected,255)
    assert a['pivot']==[.5,.5] and a['display_fit']=='sliced'
    native=sliced(im,tuple(a['display_size_px']),a['nine_slice_lbrt_px'],d.get('shape')=='pixel_caps')
    exported[a['id']]=im;shown[a['id']]=native
    display_path=f"art-source/previews/ART-09/{a['id']}-display-light-dark.png"
    detail_path=f"art-source/previews/ART-09/{a['id']}-export-1x-light-dark.png"
    single_display(native,a['id']).save(PROJECT/display_path)
    detail=Image.new('RGB',(560,400),GROUND);draw=ImageDraw.Draw(detail)
    draw.text((16,12),a['id'],fill=DARK,font=FONT)
    draw.text((16,40),d['name'],fill=DARK,font=SMALL)
    draw.text((16,64),f"Export {im.width}×{im.height} px | 1:1 | claro / escuro",fill=DARK,font=SMALL)
    for x,color in [(16,LIGHT),(288,DARK)]:
        tile=Image.new('RGBA',(256,256),color)
        tile.alpha_composite(im,((256-im.width)//2,(256-im.height)//2))
        detail.paste(tile.convert('RGB'),(x,88))
    draw.text((16,360),'Display '+str(a['display_size_px'])+' | 9-slice '+str(a['nine_slice_lbrt_px']),fill=DARK,font=SMALL)
    detail.save(PROJECT/detail_path);details.append(detail)
    measurements.append({
        'id':a['id'],'name':d['name'],'family':d['family'],'state':d.get('state'),'part':d.get('part'),
        'actual_size_px':list(im.size),'mode':im.mode,'color_space':'sRGB with embedded profile',
        'sha256':hashlib.sha256((PROJECT/manifest['asset_root']/a['path']).read_bytes()).hexdigest(),
        'alpha_sha256':hashlib.sha256(alpha.tobytes()).hexdigest(),'alpha_bbox':list(alpha.getbbox()),
        'alpha_extrema':list(alpha.getextrema()),'transparent_fraction':alpha.histogram()[0]/(im.width*im.height),
        'center_rgba':list(im.getpixel((im.width//2,im.height//2))),
        'display_size_px':a['display_size_px'],'pivot':a['pivot'],'display_fit':a['display_fit'],
        'nine_slice_lbrt_px':a['nine_slice_lbrt_px'],'nine_slice_corners_pixel_equal':True,
        'nine_slice_uniform_cut_strips':True if d.get('shape')!='pixel_caps' else None,
        'pixel_caps_preserved':True if d.get('shape')=='pixel_caps' else None,
        'display_preview':display_path,'detail_preview':detail_path
    })

assert len(measurements)==18 and len({m['sha256'] for m in measurements})==18
family_checks=[]
for family in ['pill','toast','table_row']:
    members=[a for a in assets if records[a['id']]['family']==family]
    first=members[0]
    deltas=[ImageChops.difference(exported[a['id']].getchannel('A'),exported[first['id']].getchannel('A')).getextrema()[1] for a in members]
    native_deltas=[ImageChops.difference(shown[a['id']].getchannel('A'),shown[first['id']].getchannel('A')).getextrema()[1] for a in members]
    assert max(deltas)==0 and max(native_deltas)==0
    assert all(a['pivot']==first['pivot'] and a['nine_slice_lbrt_px']==first['nine_slice_lbrt_px'] for a in members)
    family_checks.append({'family':family,'asset_ids':[a['id'] for a in members],
        'max_export_alpha_delta':max(deltas),'max_display_alpha_delta':max(native_deltas),
        'bounds_and_pivot_identical':True,'states_distinct':len({records[a['id']]['sha256'] for a in members})==len(members)})
assert exported['UI_PROGRESS_TRACK'].getchannel('A').tobytes()==exported['UI_PROGRESS_FILL'].getchannel('A').tobytes()

for page in range(6):
    out=Image.new('RGB',(1680,400),GROUND)
    for col in range(3):out.paste(details[page*3+col],(col*560,0))
    out.save(PREVIEW/f'ART-09-review-{page+1:02}.png')

family_previews=[]
for family,ids in GROUPS.items():
    maxw=max(shown[aid].width for aid in ids)
    vertical=maxw>680
    blockh=sum(shown[aid].height+56 for aid in ids)
    width=max(800,maxw+48 if vertical else 2*maxw+80)
    out=Image.new('RGB',(width,blockh*2+140 if vertical else blockh+100),GROUND);draw=ImageDraw.Draw(out)
    draw.text((24,16),'ART-09 | '+family+' | display 1:1 por 9-slice',fill=DARK,font=FONT)
    for i,(label,color) in enumerate([('Claro',LIGHT),('Escuro',DARK)]):
        x=24 if vertical else 24+i*(maxw+32)
        y=52+i*(blockh+40) if vertical else 52
        draw.text((x,y),label,fill=DARK,font=SMALL);y+=28
        for aid in ids:
            im=shown[aid]
            draw.text((x,y),aid,fill=DARK,font=SMALL)
            out.paste(background(im,color),(x,y+24))
            y+=im.height+56
    rel=f'art-source/previews/ART-09/{family}-display-1x.png'
    out.save(PROJECT/rel);family_previews.append({'family':family,'display':rel})

# Demonstrate independent progress pieces, without writing composed production sprites.
rig=Image.new('RGB',(1080,380),GROUND);draw=ImageDraw.Draw(rig)
draw.text((24,16),'ART-09 | Montagem de progresso e sublinhado | display 1:1',fill=DARK,font=TITLE)
for col,color in enumerate([LIGHT,DARK]):
    x=24+col*536
    for row,(label,width) in enumerate([('Zero',0),('Metade',240),('Cheio',480)]):
        y=80+row*72
        draw.text((x,y-26),label,fill=DARK,font=FONT)
        layer=Image.new('RGBA',(480,16),color)
        layer.alpha_composite(shown['UI_PROGRESS_TRACK'])
        if width:
            fill=sliced(exported['UI_PROGRESS_FILL'],(width,16),[6,6,6,6])
            layer.alpha_composite(fill)
        rig.paste(layer.convert('RGB'),(x,y))
    rig.paste(background(shown['UI_NAV_UNDERLINE'],DARK),(x,320))
draw.text((24,350),'Fundos e labels existem apenas na QA; fill e trilho são arquivos independentes.',fill=DARK,font=SMALL)
rig.save(PREVIEW/'progress-assembly-1x.png')
for width in [13,16,240,480]:sliced(exported['UI_PROGRESS_FILL'],(width,16),[6,6,6,6])

sheet=Image.new('RGB',(1368,1560),GROUND);draw=ImageDraw.Draw(sheet)
draw.text((24,16),'ART-09 | Indicadores do HUD e componentes da DRE',fill=DARK,font=TITLE)
draw.text((24,54),'18 PNGs + 18 SVGs | visão geral; labels e fundos apenas na QA | versões 1:1 nos previews individuais',fill=DARK,font=FONT)
for i,a in enumerate(assets):
    x=16+(i%3)*456;y=100+(i//3)*240
    draw.text((x,y),records[a['id']]['design']['name'],fill=DARK,font=FONT)
    draw.text((x,y+24),a['id'],fill=DARK,font=SMALL)
    little=ImageOps.contain(shown[a['id']],(424,76),R)
    for j,color in enumerate([LIGHT,DARK]):
        layer=Image.new('RGBA',(424,80),color)
        layer.alpha_composite(little,((424-little.width)//2,(80-little.height)//2))
        sheet.paste(layer.convert('RGB'),(x,y+48+j*84))
sheet.save(PREVIEW/'ART-09-contact-sheet.png')
(PROJECT/'docs/art/qa/ART-09-measurements.json').write_text(json.dumps({
    'task_id':'ART-09','assets':measurements,'state_families':family_checks,
    'progress_track_fill_alpha_identical':True,'progress_checked_fill_widths_px':[0,13,16,240,480],
    'nav_caps_note':'Four clear 1×1 source corners remain clear at 240×4; adjacent pixels remain fully opaque.',
    'family_previews':family_previews,'assembly_preview':'art-source/previews/ART-09/progress-assembly-1x.png',
    'review_pages':[f'art-source/previews/ART-09/ART-09-review-{i:02}.png' for i in range(1,7)]
},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'task_id':'ART-09','measured_assets':18,'state_families':3,'max_state_alpha_delta':0,
    'nine_slice_corners_preserved':True,'progress_track_fill_alpha_identical':True,'preview_count':51}))
