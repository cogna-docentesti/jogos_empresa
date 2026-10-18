from pathlib import Path
import json

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
FONTS = PROJECT / 'Assets/TextMesh Pro'


def font_file(name, weight):
    choices = {
        'Roboto Condensed': (ROOT / 'RobotoCondensed.ttf', [weight]),
        'Roboto': (ROOT / 'Roboto.ttf', [weight, 100]),
        'Roboto SemiCondensed': (ROOT / 'Roboto.ttf', [weight, 87.5]),
        'Barlow Semi Condensed': (ROOT / f'BarlowSemiCondensed-{"Bold" if weight >= 600 else "Regular"}.ttf', None),
        'Barlow': (ROOT / f'Barlow-{"Bold" if weight >= 600 else "Regular"}.ttf', None),
        'Source Sans 3': (ROOT / 'SourceSans3.ttf', [weight]),
        'Inter Tight': (FONTS / 'Fonts/InterTight-VariableFont_wght.ttf', [weight]),
        'Oswald Bold': (FONTS / 'Examples & Extras/Fonts/Oswald-Bold.ttf', None),
    }
    return choices[name]


def render(text, family, weight):
    path, axes = font_file(family, weight)
    font = ImageFont.truetype(str(path), 180)
    if axes:
        font.set_variation_by_axes(axes)
    bounds = font.getbbox(text)
    mask = Image.new('L', (bounds[2] - bounds[0] + 8, bounds[3] - bounds[1] + 8), 0)
    ImageDraw.Draw(mask).text((4 - bounds[0], 4 - bounds[1]), text, fill=255, font=font)
    return mask.crop(mask.getbbox())


families = ['Roboto Condensed', 'Roboto', 'Roboto SemiCondensed', 'Barlow Semi Condensed', 'Barlow', 'Source Sans 3', 'Inter Tight', 'Oswald Bold']
hub = Image.open(PROJECT / 'art-source/references/concepts/01-hub.png').convert('RGB')
dre = Image.open(PROJECT / 'art-source/references/concepts/04-dre-mensal.png').convert('RGB')
specimens = [
    ('brand', 'Jogos de Empresa', hub, (111, 18, 440, 61), 700, True),
    ('heading', 'Demonstrativo do mês', dre, (111, 311, 424, 346), 700, False),
    ('body', 'Receita de vendas', dre, (71, 371, 268, 400), 400, False),
    ('button', 'VER RESULTADOS', hub, (1330, 770, 1501, 799), 700, True),
]
report = []
label_font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 23)
small_font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 16)
for key, text, image, box, weight, light in specimens:
    crop = image.crop(box)
    mask = crop.convert('L').point(lambda x: 255 if (x > 210 if light else x < 105) else 0)
    bounds = mask.getbbox()
    mask = mask.crop(bounds)
    crop = crop.crop(bounds)
    ink_h = 58
    original_width = mask.width / mask.height * ink_h
    board = Image.new('RGB', (1300, 1100), '#f6f4f0')
    draw = ImageDraw.Draw(board)
    draw.text((24, 16), f'Concept x fontes reais | {text}', fill='#23113d', font=label_font)
    for i, family in enumerate(['CONCEPT'] + families):
        y = 70 + i * 112
        draw.text((24, y + 10), family, fill='#23113d', font=label_font)
        if family == 'CONCEPT':
            fitted = crop.resize((round(original_width), ink_h), Image.Resampling.LANCZOS)
            board.paste(fitted, (355, y))
        else:
            rendered = render(text, family, weight)
            rendered_width = rendered.width / rendered.height * ink_h
            fitted = rendered.resize((round(rendered_width), ink_h), Image.Resampling.LANCZOS)
            board.paste('#23113d', (355, y), fitted)
            ratio = rendered_width / original_width
            draw.text((24, y + 43), f'largura relativa: {ratio:.3f}', fill='#5d5369', font=small_font)
            report.append({'sample': key, 'font': family, 'width_ratio_at_same_ink_height': ratio})
        draw.line((348 + original_width, y - 5, 348 + original_width, y + 69), fill='#ba9ccc', width=1)
        draw.line((20, y + 90, 1280, y + 90), fill='#ddd8df', width=1)
    board.save(ROOT / f'comparison-{key}.png')
(ROOT / 'width-comparison.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('Created four comparison images and width-comparison.json')
