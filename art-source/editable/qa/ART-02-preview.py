"""Build QA composites and measurements only; never modify production PNGs."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[3]
OUT = PROJECT / 'art-source/previews/ART-02'
OUT.mkdir(parents=True, exist_ok=True)
MANIFEST = json.loads((PROJECT / 'docs/art/manifest.json').read_text(encoding='utf-8'))
ASSETS = [a for a in MANIFEST['assets'] if a['task_id'] == 'ART-02']
FONT = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 18)
BG = [('claro', '#F5F0E8'), ('escuro', '#24113D')]
CONTACT = Image.new('RGB', (900, 1660), '#D7D1DE')
METRICS = []

def composite(cutout, bg):
    base = Image.new('RGBA', cutout.size, bg)
    return Image.alpha_composite(base, cutout).convert('RGB')

for index, asset in enumerate(ASSETS):
    path = PROJECT / MANIFEST['asset_root'] / asset['path']
    sprite = Image.open(path)
    alpha = sprite.getchannel('A')
    histogram = alpha.histogram()
    pixels = sprite.width * sprite.height
    bbox = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
    left, top, right, bottom = bbox
    metrics = {
        'id': asset['id'], 'path': path.relative_to(PROJECT).as_posix(),
        'actual_size_px': list(sprite.size), 'mode': sprite.mode,
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'alpha_extrema': list(alpha.getextrema()),
        'transparent_fraction': histogram[0] / pixels,
        'alpha_bbox': list(alpha.getbbox()),
        'visible_bbox_alpha_gte_128': list(bbox),
        'visible_padding_ltrb_percent': [
            round(100 * left / sprite.width, 3),
            round(100 * top / sprite.height, 3),
            round(100 * (sprite.width - right) / sprite.width, 3),
            round(100 * (sprite.height - bottom) / sprite.height, 3)],
        'display_box_px': asset['display_size_px'], 'display_fit': 'contain',
        'png_preserved_byte_for_byte_from_imagegen': True,
    }
    character = asset['character_ids'][0]
    display = sprite.copy()
    display.thumbnail(tuple(asset['display_size_px']), Image.Resampling.LANCZOS)
    sheet = Image.new('RGB', (900, 820), '#D7D1DE')
    draw = ImageDraw.Draw(sheet)
    for column, (label, bg) in enumerate(BG):
        panel = Image.new('RGBA', (420, 760), bg)
        panel.alpha_composite(display, ((420-display.width)//2, (760-display.height)//2))
        sheet.paste(panel.convert('RGB'), (20 + column*440, 40))
        draw.text((20 + column*440, 8), character + ' | contain 420x760 | ' + label, font=FONT, fill='#30243C')
        CONTACT.paste(panel.convert('RGB'), (15+index*450, 50+column*820))
        ImageDraw.Draw(CONTACT).text((15+index*450, 12+column*820), character + ' | ' + label, font=FONT, fill='#30243C')
    preview = OUT / (character + '-light-dark.png')
    sheet.save(preview)
    metrics['preview'] = preview.relative_to(PROJECT).as_posix()
    height = bottom - top
    regions = [('rosto', (left, top, right, min(bottom, top+int(height*.28)))),
               ('maos', (left, top+int(height*.43), right, top+int(height*.67))),
               ('pes', (left, top+int(height*.82), right, bottom))]
    details = Image.new('RGB', (2*(right-left)+60, sum(box[3]-box[1]+44 for _, box in regions)), '#D7D1DE')
    cursor = 0
    for title, box in regions:
        cutout = sprite.crop(box)
        for column, (label, bg) in enumerate(BG):
            x = 20+column*(right-left+20)
            ImageDraw.Draw(details).text((x, cursor+4), title+' | '+label+' | 1:1', font=FONT, fill='#30243C')
            details.paste(composite(cutout, bg), (x, cursor+30))
        cursor += cutout.height+44
    detail_path = OUT / (character+'-details-1x.png')
    details.save(detail_path)
    metrics['detail_preview'] = detail_path.relative_to(PROJECT).as_posix()
    assert hashlib.sha256(path.read_bytes()).hexdigest() == metrics['sha256']
    METRICS.append(metrics)

CONTACT.save(OUT/'ART-02-contact-sheet.png')
(PROJECT/'docs/art/qa/ART-02-measurements.json').write_text(json.dumps({'task_id':'ART-02','assets':METRICS}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(METRICS, ensure_ascii=False, indent=2))
