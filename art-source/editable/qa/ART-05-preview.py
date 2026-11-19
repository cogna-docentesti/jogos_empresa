"""Measure ART-05 files and create QA previews without editing production pixels."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont, ImageOps, ImageChops

PROJECT=Path(__file__).resolve().parents[3]
manifest=json.loads((PROJECT/'docs/art/manifest.json').read_text(encoding='utf-8'))
production=json.loads((PROJECT/'docs/art/prompts/ART-05.json').read_text(encoding='utf-8'))
assets=[a for a in manifest['assets'] if a['task_id']=='ART-05']
records={a['id']:a for a in production['assets']}
PREVIEW=PROJECT/'art-source/previews/ART-05'
PREVIEW.mkdir(parents=True,exist_ok=True)
LIGHT,DARK,GROUND='#F5F0E8','#24113D','#D9D2E0'
FONT=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
SMALL=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',15)
TITLE=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',24)
R=Image.Resampling.LANCZOS
GROUPS={
    'input':('Campos',['Normal','Foco','Inválido','Desabilitado'],2),
    'tab':('Abas',['Normal','Selecionada','Hover','Pressionada','Desabilitada'],5),
    'radio':('Bases de rádio',['Off','On','Desabilitado'],3),
    'checkbox':('Bases de checkbox',['Off','On','Desabilitado'],3),
    'toggle':('Trilhos de toggle',['Off','On','Desabilitado'],3),
    'slider':('Peças do slider',['Trilho','Preenchimento','Puxador'],1),
    'stepper':('Base do stepper',['Base vazia'],1),
    'scrollbar':('Peças da scrollbar',['Trilho','Puxador'],2)
}

def source(a):
    with Image.open(PROJECT/manifest['asset_root']/a['path']) as im:
        return im.copy()

def sliced(im,size,borders):
    l,b,r,t=borders;w,h=im.size;W,H=size
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
    assert all(im.crop(a).tobytes()==out.crop(b).tobytes() for a,b in corners)
    for a,b in [((l-1,0,l,h),(l,0,l+1,h)),((w-r-1,0,w-r,h),(w-r,0,w-r+1,h)),
                ((0,t-1,w,t),(0,t,w,t+1)),((0,h-b-1,w,h-b),(0,h-b,w,h-b+1))]:
        assert im.crop(a).tobytes()==im.crop(b).tobytes(), 'Stretch cut crosses nonuniform geometry'
    return out

def display(a,override_size=None):
    im=exported[a['id']]
    size=tuple(override_size or a['display_size_px'])
    if a['display_fit']=='sliced':return sliced(im,size,a['nine_slice_lbrt_px'])
    fitted=ImageOps.contain(im,size,R)
    out=Image.new('RGBA',size)
    out.alpha_composite(fitted,((size[0]-fitted.width)//2,(size[1]-fitted.height)//2))
    return out

def on_background(im,color):
    out=Image.new('RGBA',im.size,color);out.alpha_composite(im);return out.convert('RGB')

def pair(im):
    w,h=im.size;out=Image.new('RGB',(w*2+48,h+64),GROUND);draw=ImageDraw.Draw(out)
    for i,(label,color) in enumerate([('Claro',LIGHT),('Escuro',DARK)]):
        x=16+i*(w+16);draw.text((x,10),label,fill=DARK,font=FONT)
        out.paste(on_background(im,color),(x,48))
    return out

exported={a['id']:source(a) for a in assets}
shown={a['id']:display(a) for a in assets}
measurements=[]
for a in assets:
    im=exported[a['id']];d=records[a['id']]['design'];alpha=im.getchannel('A')
    assert im.mode=='RGBA' and im.size==tuple(a['export_size_px']) and im.info.get('icc_profile')
    assert alpha.getpixel((0,0))==0 and alpha.getextrema()==(0,255)
    expected=tuple(int(d['fill'][i:i+2],16) for i in [1,3,5])
    assert im.getpixel((im.width//2,im.height//2))==(*expected,255)
    display_path=f"art-source/previews/ART-05/{a['id']}-display-light-dark.png"
    detail_path=f"art-source/previews/ART-05/{a['id']}-export-1x-light-dark.png"
    pair(shown[a['id']]).save(PROJECT/display_path);pair(im).save(PROJECT/detail_path)
    measurements.append({
        'id':a['id'],'family':d['family'],'state':d.get('state'),'part':d.get('part'),
        'path':f"{manifest['asset_root']}/{a['path']}",'actual_size_px':list(im.size),'mode':im.mode,
        'color_space':'sRGB with embedded profile',
        'sha256':hashlib.sha256((PROJECT/manifest['asset_root']/a['path']).read_bytes()).hexdigest(),
        'alpha_sha256':hashlib.sha256(alpha.tobytes()).hexdigest(),'alpha_bbox':list(alpha.getbbox()),
        'alpha_extrema':list(alpha.getextrema()),'transparent_fraction':alpha.histogram()[0]/(im.width*im.height),
        'center_rgba':list(im.getpixel((im.width//2,im.height//2))),
        'display_size_px':a['display_size_px'],'display_fit':a['display_fit'],'pivot':a['pivot'],
        'nine_slice_lbrt_px':a['nine_slice_lbrt_px'],
        'nine_slice_corners_pixel_equal':True if a['display_fit']=='sliced' else None,
        'nine_slice_uniform_cut_strips':True if a['display_fit']=='sliced' else None,
        'display_preview':display_path,'detail_preview':detail_path
    })

family_checks=[]
for family in ['input','tab','radio','checkbox','toggle']:
    members=[a for a in assets if records[a['id']]['family']==family]
    first=members[0]
    base_alpha=exported[first['id']].getchannel('A');base_display_alpha=shown[first['id']].getchannel('A')
    export_deltas=[ImageChops.difference(exported[a['id']].getchannel('A'),base_alpha).getextrema()[1] for a in members]
    display_deltas=[ImageChops.difference(shown[a['id']].getchannel('A'),base_display_alpha).getextrema()[1] for a in members]
    # Different inner stroke widths may change rounding by one alpha level at
    # antialiased corner pixels. Bounds and geometric outline must remain fixed.
    assert max(export_deltas)<=1 and max(display_deltas)<=1
    assert all(exported[a['id']].getchannel('A').getbbox()==base_alpha.getbbox() for a in members)
    assert all(shown[a['id']].getchannel('A').getbbox()==base_display_alpha.getbbox() for a in members)
    assert all(a['pivot']==first['pivot'] and a['export_size_px']==first['export_size_px'] for a in members)
    assert len({exported[a['id']].tobytes() for a in members})==len(members)
    family_checks.append({'family':family,'state_count':len(members),'export_alpha_identical':max(export_deltas)==0,
                          'display_alpha_identical':max(display_deltas)==0,'max_export_alpha_delta':max(export_deltas),
                          'max_display_alpha_delta':max(display_deltas),'bounds_and_pivot_identical':True,'states_distinct':True})
assert exported['UI_SLIDER_TRACK'].getchannel('A').tobytes()==exported['UI_SLIDER_FILL'].getchannel('A').tobytes()

def family_sheet(family,mode):
    title,labels,columns=GROUPS[family]
    members=[a for a in assets if records[a['id']]['family']==family]
    images=[(shown if mode=='display' else exported)[a['id']] for a in members]
    maxw=max(180,max(im.width for im in images));maxh=max(48,max(im.height for im in images))
    cellw=maxw+32;cellh=maxh*2+78
    rows=(len(members)+columns-1)//columns
    heading=title+' | '+('exibição 1:1' if mode=='display' else 'exports 1:1')
    width=max(columns*cellw+16,TITLE.getbbox(heading)[2]+32)
    out=Image.new('RGB',(width,rows*cellh+72),GROUND);draw=ImageDraw.Draw(out)
    draw.text((16,12),heading,fill=DARK,font=TITLE)
    for i,(a,label,im) in enumerate(zip(members,labels,images)):
        x=16+(i%columns)*cellw;y=64+(i//columns)*cellh
        draw.text((x,y),label+f' | {im.width}×{im.height}',fill=DARK,font=FONT)
        for row,color in enumerate([LIGHT,DARK]):
            cell=Image.new('RGBA',(maxw,maxh),color)
            cell.alpha_composite(im,((maxw-im.width)//2,(maxh-im.height)//2))
            out.paste(cell.convert('RGB'),(x,y+34+row*(maxh+8)))
    out.save(PREVIEW/f'{family}-{mode}-1x.png')
    return out

display_sheets={}
for family in GROUPS:
    display_sheets[family]=family_sheet(family,'display');family_sheet(family,'export')

# Assembly QA only: no composed widget is saved into Assets/Art/new.
lookup={a['id']:a for a in assets}
rig=Image.new('RGBA',(1040,880),LIGHT);draw=ImageDraw.Draw(rig)
draw.text((24,16),'Montagem de QA | peças independentes',fill=DARK,font=TITLE)
for i,value in enumerate([0,.5,1]):
    x,y=44,110+i*100
    draw.text((24,y-34),f'Slider: {int(value*100)}% — valor apenas nesta QA',fill=DARK,font=FONT)
    rig.alpha_composite(shown['UI_SLIDER_TRACK'],(x,y))
    width=round(680*value)
    if width:rig.alpha_composite(display(lookup['UI_SLIDER_FILL'],[width,16]),(x,y))
    rig.alpha_composite(shown['UI_SLIDER_THUMB'],(x+width-20,y-12))
for i,state in enumerate(['OFF','ON','DISABLED']):
    x,y=24+i*240,470
    draw.text((x,y-32),'Toggle '+state.lower(),fill=DARK,font=FONT)
    rig.alpha_composite(shown['UI_TOGGLE_'+state],(x,y))
    center=154 if state=='ON' else 26
    rig.alpha_composite(shown['UI_SLIDER_THUMB'],(x+center-20,y+6))
draw.text((24,570),'Puxador reutilizado: UI_SLIDER_THUMB; não integra os trilhos dos PNGs.',fill=DARK,font=FONT)
draw.text((24,620),'Radio e checkbox: somente bases; marcadores permanecem separados.',fill=DARK,font=FONT)
draw.text((800,70),'Scrollbar',fill=DARK,font=FONT)
rig.alpha_composite(shown['UI_SCROLLBAR_TRACK'],(836,110))
rig.alpha_composite(shown['UI_SCROLLBAR_THUMB'],(838,280))
rig.convert('RGB').save(PREVIEW/'controls-assembly-1x.png')

sheet=Image.new('RGB',(1440,2140),GROUND);draw=ImageDraw.Draw(sheet)
draw.text((24,18),'ART-05 | Controles, campos e seleção',fill=DARK,font=TITLE)
draw.text((24,58),'24 PNGs + 24 SVGs | Visão geral de QA; labels e fundos não integram os sprites',fill=DARK,font=FONT)
placements=[('input',24,110,680,390),('tab',736,110,680,390),
            ('radio',24,520,680,220),('checkbox',736,520,680,220),
            ('toggle',24,760,680,240),('stepper',736,760,680,240),
            ('slider',24,1020,680,1080),('scrollbar',736,1020,680,1080)]
for family,x,y,w,h in placements:
    thumb=ImageOps.contain(display_sheets[family],(w,h),R)
    sheet.paste(thumb,(x,y))
sheet.save(PREVIEW/'ART-05-contact-sheet.png')
(PROJECT/'docs/art/qa/ART-05-measurements.json').write_text(json.dumps({
    'task_id':'ART-05','assets':measurements,'state_families':family_checks,
    'slider_track_fill_alpha_identical':True,'assembly_preview':'art-source/previews/ART-05/controls-assembly-1x.png'
},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'task_id':'ART-05','measured_assets':24,'state_families':5,'state_bounds_identical':True,
                  'max_state_alpha_delta':max(f['max_export_alpha_delta'] for f in family_checks),
                  'nine_slice_corners_preserved':True,'slider_track_fill_alpha_identical':True,'preview_count':66}))
