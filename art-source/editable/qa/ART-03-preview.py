"""Read final ART-03 assets; write QA previews and measurements only."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont, ImageOps

PROJECT = Path(__file__).resolve().parents[3]
manifest = json.loads((PROJECT / 'docs/art/manifest.json').read_text(encoding='utf-8'))
assets = [a for a in manifest['assets'] if a['task_id'] == 'ART-03']
PREVIEW = PROJECT / 'art-source/previews/ART-03'
PREVIEW.mkdir(parents=True, exist_ok=True)
LIGHT, DARK = '#F5F0E8', '#24113D'
FONT = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18)
SMALL = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 15)
TITLE = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 28)
RESAMPLE = Image.Resampling.LANCZOS

def source(asset):
    with Image.open(PROJECT / manifest['asset_root'] / asset['path']) as im:
        return im.copy()

def sliced(im, size, borders):
    l,b,r,t = borders
    w,h = im.size
    W,H = size
    sx,sy = [0,l,w-r,w], [0,t,h-b,h]
    tx,ty = [0,l,W-r,W], [0,t,H-b,H]
    result = Image.new('RGBA', size)
    for row in range(3):
        for col in range(3):
            part = im.crop((sx[col],sy[row],sx[col+1],sy[row+1]))
            target_size = (tx[col+1]-tx[col],ty[row+1]-ty[row])
            if part.size != target_size:
                part = part.resize(target_size, RESAMPLE)
            result.paste(part, (tx[col],ty[row]))
    corners = [((0,0,l,t),(0,0,l,t)),
               ((w-r,0,w,t),(W-r,0,W,t)),
               ((0,h-b,l,h),(0,H-b,l,H)),
               ((w-r,h-b,w,h),(W-r,H-b,W,H))]
    assert all(im.crop(a).tobytes() == result.crop(b).tobytes() for a,b in corners)
    return result

def display(asset):
    im = source(asset)
    size = tuple(asset['display_size_px'])
    if asset['display_fit'] == 'sliced':
        return sliced(im, size, asset['nine_slice_lbrt_px'])
    if asset['display_fit'] in ['exact', 'stretch']:
        return im.resize(size, Image.Resampling.NEAREST if im.size == (1,1) else RESAMPLE)
    fitted = ImageOps.contain(im, size, RESAMPLE)
    result = Image.new('RGBA', size)
    result.alpha_composite(fitted, ((size[0]-fitted.width)//2,(size[1]-fitted.height)//2))
    return result

def pair(im):
    w,h = im.size
    result = Image.new('RGB', (w*2+32,h+56), '#D9D2E0')
    draw = ImageDraw.Draw(result)
    for i, color in enumerate([LIGHT,DARK]):
        layer = Image.new('RGBA', im.size, color)
        layer.alpha_composite(im)
        result.paste(layer.convert('RGB'), (i*(w+16)+8,48))
        draw.text((i*(w+16)+8,12), 'Claro' if i == 0 else 'Escuro', fill='#30243C', font=FONT)
    return result

measurements = []
for asset in assets:
    im = source(asset)
    alpha = im.getchannel('A')
    histogram = alpha.histogram()
    count = im.width*im.height
    pair(display(asset)).save(PREVIEW / f"{asset['id']}-display-light-dark.png")
    pair(im).save(PREVIEW / f"{asset['id']}-export-1x-light-dark.png")
    measurements.append({
        'id': asset['id'], 'path': f"{manifest['asset_root']}/{asset['path']}",
        'actual_size_px': list(im.size), 'mode': im.mode,
        'sha256': hashlib.sha256((PROJECT / manifest['asset_root'] / asset['path']).read_bytes()).hexdigest(),
        'alpha_extrema': list(alpha.getextrema()), 'alpha_bbox': list(alpha.getbbox()),
        'alpha_center': alpha.getpixel((im.width//2,im.height//2)),
        'alpha_top_left': alpha.getpixel((0,0)),
        'transparent_fraction': histogram[0]/count,
        'opaque_fraction': histogram[255]/count,
        'display_size_px': asset['display_size_px'], 'display_fit': asset['display_fit'],
        'nine_slice_corners_pixel_equal': True if asset['display_fit'] == 'sliced' else None,
        'display_preview': f"art-source/previews/ART-03/{asset['id']}-display-light-dark.png",
        'detail_preview': f"art-source/previews/ART-03/{asset['id']}-export-1x-light-dark.png"
    })

lookup = {a['id']: a for a in assets}
normal = display(lookup['UI_CARD_NORMAL'])
selected = normal.copy()
selected.alpha_composite(display(lookup['UI_CARD_SELECTED_OUTLINE']))
disabled = normal.copy()
# QA-only shapes verify that disabled alpha reveals the content below it.
draw = ImageDraw.Draw(disabled)
draw.rounded_rectangle((32,32,328,236), radius=12, fill='#B99AE8')
draw.rectangle((32,260,268,275), fill='#24113D')
draw.rectangle((32,297,218,308), fill='#26776A')
before_overlay = disabled.copy()
disabled.alpha_composite(display(lookup['UI_CARD_DISABLED_OVERLAY']))
rig = Image.new('RGBA',(1520,500),LIGHT)
draw = ImageDraw.Draw(rig)
for i,(label,im) in enumerate([('Normal',normal),('Selecionado',selected),('Antes do overlay',before_overlay),('Desabilitado',disabled)]):
    x = 16+i*376
    draw.text((x,10), label, fill=DARK, font=FONT)
    rig.alpha_composite(im,(x,48))
rig.convert('RGB').save(PREVIEW/'card-states-1x.png')

shadow = display(lookup['UI_PANEL_SHADOW'])
panel = display(lookup['UI_PANEL_IVORY'])
shadow.alpha_composite(panel,(6,6))
pair(shadow).save(PREVIEW/'panel-shadow-composition-1x.png')

# Every export is included, with labels only in this non-production preview.
sheet = Image.new('RGB',(1680,1800),'#D9D2E0')
draw = ImageDraw.Draw(sheet)
draw.text((24,18),'ART-03 | Barras, painéis e bases compartilhadas',fill=DARK,font=TITLE)
draw.text((24,58),'14 PNGs separados | 13 fontes SVG | preview de QA; labels não integram os sprites',fill=DARK,font=FONT)
for i,asset_id in enumerate(['UI_TOPBAR_VIOLET','UI_NAVBAR_PLUM']):
    im = source(lookup[asset_id]).resize((1632,75),RESAMPLE)
    sheet.paste(im.convert('RGB'),(24,105+i*105))
    draw.text((24,184+i*105),asset_id,fill=DARK,font=SMALL)
rest = [a for a in assets if a['id'] not in ['UI_TOPBAR_VIOLET','UI_NAVBAR_PLUM']]
for i,asset in enumerate(rest):
    x,y = 24+(i%4)*414,330+(i//4)*480
    draw.text((x,y),asset['id'].replace('UI_',''),fill=DARK,font=FONT)
    draw.text((x,y+26),f"{asset['export_size_px'][0]}×{asset['export_size_px'][1]} px | {asset['alpha_policy']}",fill=DARK,font=SMALL)
    im = source(asset)
    if im.size == (1,1):
        im = im.resize((256,256),Image.Resampling.NEAREST)
    else:
        im = ImageOps.contain(im,(360,320),RESAMPLE)
    for j,bg in enumerate([LIGHT,DARK]):
        cell = Image.new('RGBA',(390,185),bg)
        mini = ImageOps.contain(im,(360,165),RESAMPLE)
        cell.alpha_composite(mini,((390-mini.width)//2,(185-mini.height)//2))
        sheet.paste(cell.convert('RGB'),(x,y+58+j*191))
sheet.save(PREVIEW/'ART-03-contact-sheet.png')
(PROJECT/'docs/art/qa/ART-03-measurements.json').write_text(
    json.dumps({'task_id':'ART-03','assets':measurements},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'preview_count':len(assets)*2+3,'measured_assets':len(assets),'nine_slice_corners_preserved':True}))
