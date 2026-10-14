"""Read-only asset measurements and button-state previews for ART-04."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

PROJECT=Path(__file__).resolve().parents[3]
manifest=json.loads((PROJECT/'docs/art/manifest.json').read_text(encoding='utf-8'))
production=json.loads((PROJECT/'docs/art/prompts/ART-04.json').read_text(encoding='utf-8'))
assets=[a for a in manifest['assets'] if a['task_id']=='ART-04']
records={a['id']:a for a in production['assets']}
PREVIEW=PROJECT/'art-source/previews/ART-04'
PREVIEW.mkdir(parents=True,exist_ok=True)
LIGHT,DARK='#F5F0E8','#24113D'
FONT=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
SMALL=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',15)
TITLE=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',26)
STATES=['normal','hover','pressed','disabled']
LABELS=['Normal','Hover','Pressionado','Desabilitado']
FAMILIES=['primary_violet','secondary_ivory','danger_coral','accent_amber','icon_button']
NAMES=['Primário violeta','Secundário marfim','Ação de risco coral','Destaque âmbar','Base para ícone']

def source(asset):
    with Image.open(PROJECT/manifest['asset_root']/asset['path']) as im:
        return im.copy()

def sliced(im,size,borders):
    l,b,r,t=borders
    w,h=im.size
    W,H=size
    sx,sy=[0,l,w-r,w],[0,t,h-b,h]
    tx,ty=[0,l,W-r,W],[0,t,H-b,H]
    result=Image.new('RGBA',size)
    for row in range(3):
        for col in range(3):
            part=im.crop((sx[col],sy[row],sx[col+1],sy[row+1]))
            target=(tx[col+1]-tx[col],ty[row+1]-ty[row])
            if part.size!=target:
                part=part.resize(target,Image.Resampling.LANCZOS)
            result.paste(part,(tx[col],ty[row]))
    corners=[((0,0,l,t),(0,0,l,t)),((w-r,0,w,t),(W-r,0,W,t)),
             ((0,h-b,l,h),(0,H-b,l,H)),((w-r,h-b,w,h),(W-r,H-b,W,H))]
    assert all(im.crop(a).tobytes()==result.crop(b).tobytes() for a,b in corners)
    # The stretch cuts must fall on uniform strips, beyond each rounded corner.
    assert im.crop((l-1,0,l,h)).tobytes()==im.crop((l,0,l+1,h)).tobytes()
    assert im.crop((w-r-1,0,w-r,h)).tobytes()==im.crop((w-r,0,w-r+1,h)).tobytes()
    assert im.crop((0,t-1,w,t)).tobytes()==im.crop((0,t,w,t+1)).tobytes()
    assert im.crop((0,h-b-1,w,h-b)).tobytes()==im.crop((0,h-b,w,h-b+1)).tobytes()
    return result

def background(im,color):
    result=Image.new('RGBA',im.size,color)
    result.alpha_composite(im)
    return result.convert('RGB')

def pair(im):
    w,h=im.size
    result=Image.new('RGB',(w*2+48,h+64),'#D9D2E0')
    draw=ImageDraw.Draw(result)
    for i,(label,color) in enumerate([('Claro',LIGHT),('Escuro',DARK)]):
        x=16+i*(w+16)
        draw.text((x,10),label,font=FONT,fill=DARK)
        result.paste(background(im,color),(x,48))
    return result

measurements=[]
exported,displayed={},{ }
for asset in assets:
    im=source(asset)
    record=records[asset['id']]
    assert im.mode=='RGBA' and im.size==tuple(asset['export_size_px'])
    assert im.info.get('icc_profile'), 'Missing sRGB profile'
    alpha=im.getchannel('A')
    assert alpha.getpixel((0,0))==0 and alpha.getpixel((im.width//2,im.height//2))==255
    expected=tuple(int(record['colors']['fill'][i:i+2],16) for i in [1,3,5])
    assert im.getpixel((im.width//2,im.height//2))[:3]==expected
    shown=sliced(im,tuple(asset['display_size_px']),asset['nine_slice_lbrt_px'])
    exported[asset['id']],displayed[asset['id']]=im,shown
    display_path=f"art-source/previews/ART-04/{asset['id']}-display-light-dark.png"
    detail_path=f"art-source/previews/ART-04/{asset['id']}-export-1x-light-dark.png"
    pair(shown).save(PROJECT/display_path)
    pair(im).save(PROJECT/detail_path)
    hist=alpha.histogram()
    measurements.append({
        'id':asset['id'],'family':record['family'],'state':record['state'],
        'path':f"{manifest['asset_root']}/{asset['path']}",
        'actual_size_px':list(im.size),'mode':im.mode,'color_space':'sRGB with embedded profile',
        'sha256':hashlib.sha256((PROJECT/manifest['asset_root']/asset['path']).read_bytes()).hexdigest(),
        'alpha_sha256':hashlib.sha256(alpha.tobytes()).hexdigest(),
        'alpha_extrema':list(alpha.getextrema()),'alpha_bbox':list(alpha.getbbox()),
        'transparent_fraction':hist[0]/(im.width*im.height),'center_rgba':list(im.getpixel((im.width//2,im.height//2))),
        'display_size_px':asset['display_size_px'],'display_fit':asset['display_fit'],
        'pivot':asset['pivot'],'nine_slice_lbrt_px':asset['nine_slice_lbrt_px'],
        'nine_slice_corners_pixel_equal':True,'nine_slice_uniform_cut_strips':True,
        'display_preview':display_path,'detail_preview':detail_path
    })

families=[]
for family in FAMILIES:
    members=[a for a in assets if records[a['id']]['family']==family]
    assert len(members)==4
    first=members[0]
    export_masks=[exported[a['id']].getchannel('A').tobytes() for a in members]
    display_masks=[displayed[a['id']].getchannel('A').tobytes() for a in members]
    assert all(mask==export_masks[0] for mask in export_masks)
    assert all(mask==display_masks[0] for mask in display_masks)
    assert all(a['pivot']==first['pivot'] and a['nine_slice_lbrt_px']==first['nine_slice_lbrt_px'] for a in members)
    assert len({exported[a['id']].getpixel((exported[a['id']].width//2,exported[a['id']].height//2)) for a in members})==4
    families.append({'family':family,'states':STATES,'export_alpha_identical':True,
                     'display_alpha_identical':True,'bounds_and_pivot_identical':True,
                     'four_distinct_center_colors':True})

sheet=Image.new('RGB',(1760,1230),'#D9D2E0')
draw=ImageDraw.Draw(sheet)
draw.text((24,18),'ART-04 | Botões sem texto e seus estados',fill=DARK,font=TITLE)
draw.text((24,58),'20 PNGs + 20 SVGs | Todos os estados no tamanho de exibição | Labels e fundos somente nesta QA',fill=DARK,font=FONT)
for col,label in enumerate(LABELS):
    draw.text((240+col*376,100),label,fill=DARK,font=FONT)

for row,(family,name) in enumerate(zip(FAMILIES,NAMES)):
    y=142+row*212
    draw.text((24,y+8),name,fill=DARK,font=FONT)
    draw.text((24,y+38),'64×64 px' if family=='icon_button' else '360×72 px',fill=DARK,font=SMALL)
    draw.text((24,y+68),'Claro / escuro',fill=DARK,font=SMALL)
    family_sheet=Image.new('RGB',(1536,292),'#D9D2E0')
    family_draw=ImageDraw.Draw(family_sheet)
    family_draw.text((16,12),name+' | tamanho de exibição 1:1',fill=DARK,font=TITLE)
    export_sheet=Image.new('RGB',(1120,388),'#D9D2E0')
    export_draw=ImageDraw.Draw(export_sheet)
    export_draw.text((12,12),name+' | exports 1:1',fill=DARK,font=TITLE)
    for col,state in enumerate(STATES):
        asset=next(a for a in assets if records[a['id']]['family']==family and records[a['id']]['state']==state)
        im=displayed[asset['id']]
        for bg_row,color in enumerate([LIGHT,DARK]):
            cell=Image.new('RGBA',(360,84),color)
            cell.alpha_composite(im,((360-im.width)//2,(84-im.height)//2))
            sheet.paste(cell.convert('RGB'),(240+col*376,y+bg_row*92))
            family_sheet.paste(cell.convert('RGB'),(16+col*376,86+bg_row*94))
            full=exported[asset['id']]
            full_cell=Image.new('RGBA',(256,128),color)
            full_cell.alpha_composite(full,((256-full.width)//2,(128-full.height)//2))
            export_sheet.paste(full_cell.convert('RGB'),(12+col*280,80+bg_row*144))
        family_draw.text((16+col*376,54),LABELS[col],fill=DARK,font=FONT)
        export_draw.text((12+col*280,52),LABELS[col],fill=DARK,font=FONT)
    family_sheet.save(PREVIEW/f'{family}-states-display-1x.png')
    export_sheet.save(PREVIEW/f'{family}-states-export-1x.png')
sheet.save(PREVIEW/'ART-04-contact-sheet.png')
(PROJECT/'docs/art/qa/ART-04-measurements.json').write_text(
    json.dumps({'task_id':'ART-04','assets':measurements,'state_families':families},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'task_id':'ART-04','measured_assets':len(assets),'families':len(families),
                  'state_alpha_identical':True,'nine_slice_corners_preserved':True,'preview_count':51}))
