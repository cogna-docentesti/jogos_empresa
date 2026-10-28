"""Read-only pixel/contract checks and light/dark, display, footprint and edge previews."""
from pathlib import Path
import hashlib, json, math, sys
from PIL import Image, ImageDraw, ImageFont, ImageChops
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
# Optional private art runtime; do not alter the project's Python dependencies.
private_runtime = Path('C:/Users/Pichau/.codex/art-runtime/python')
if private_runtime.is_dir():
    sys.path.insert(0, str(private_runtime))
from jsonschema import Draft202012Validator

def read(p):
    return json.loads((ROOT / p).read_text(encoding='utf-8-sig'))
def write(p, value):
    (ROOT / p).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', size)
def composite(image, background):
    result = Image.new('RGBA', image.size, background)
    result.alpha_composite(image)
    return result.convert('RGB')
def save(image, name):
    image.save(PREVIEW / name)
    return 'art-source/previews/ART-10/' + name
def mask(polygon):
    result = Image.new('L', (1920, 1080), 0)
    ImageDraw.Draw(result).polygon([tuple(p) for p in polygon], fill=255)
    return np.array(result) > 0
def bounds_contains(rect, points):
    x, y, w, h = rect
    return all(x <= px <= x+w and y <= py <= y+h for px, py in points)

PREVIEW = ROOT / 'art-source/previews/ART-10'
PREVIEW.mkdir(parents=True, exist_ok=True)
contract = read('docs/art/layouts/map-layout.json')
schema = read('docs/art/schemas/map-layout.schema.json')
Draft202012Validator.check_schema(schema)
schema_errors = [e.message for e in Draft202012Validator(schema).iter_errors(contract)]
path = ROOT / 'Assets/Art/new/location/map/map_base.png'
before_hash = hashlib.sha256(path.read_bytes()).hexdigest()
image = Image.open(path).convert('RGBA')
alpha = np.array(image.getchannel('A'))
pixel_count = image.width * image.height
expected_ids = {x['id'] for x in read('docs/art/catalogos.json')['Locations']}
polygons = [(z['catalog_id'], z['polygon_px']) for z in contract['zones']]
polygons.append(('center', contract['central_site']['polygon_px']))
masks = {ident: mask(p) for ident, p in polygons}
coverage = sum(m.astype(np.uint8) for m in masks.values())
overlap_pixels = int(np.count_nonzero(coverage > 1))
zone_metrics = []
for ident, polygon in polygons:
    m = masks[ident]
    assert len(polygon) >= 3 and all(0 <= x < 1920 and 0 <= y < 1080 for x,y in polygon)
    xs, ys = zip(*polygon)
    # Integrated ImageGen retains near-opaque interior alpha (250..254).
    # Preserve it; reject footprints entering actual cutout edges or exterior.
    assert np.all(alpha[m] >= 250), ident + ': plot intersects a cutout edge/outside pixels'
    zone = next((z for z in contract['zones'] if z['catalog_id'] == ident), contract['central_site'])
    anchor = zone['building_ground_anchor_px']
    assert m[anchor[1], anchor[0]], ident + ': ground anchor outside polygon'
    assert bounds_contains(zone['building_envelope_px'], polygon), ident + ': footprint exceeds envelope'
    zone_metrics.append({'catalog_id': ident, 'polygon_area_px': int(np.count_nonzero(m)),
                         'polygon_bounds_px': [min(xs), min(ys), max(xs)-min(xs), max(ys)-min(ys)],
                         'all_plot_pixels_near_opaque': True,
                         'plot_alpha_min_max': [int(alpha[m].min()), int(alpha[m].max())],
                         'ground_anchor_inside': True})

assert image.size == (1920, 1080)
assert not schema_errors, schema_errors
assert overlap_pixels == 0
assert {z['catalog_id'] for z in contract['zones']} == expected_ids
assert {h['catalog_id'] for h in contract['hit_areas']} == expected_ids
assert all(h['polygon_px'] == next(z['polygon_px'] for z in contract['zones'] if z['catalog_id'] == h['catalog_id']) for h in contract['hit_areas'])
assert bounds_contains(contract['central_site_bounds_px'], contract['central_site']['polygon_px'])
assert all(layer['position_px'] == [0, 0] and layer['pivot'] == [0, 1] and layer['scale'] == 1 for layer in contract['layers'])
assert contract['canonical_master']['sha256'] == before_hash
assert all(alpha[y, x] == 0 for x,y in [(0,0),(1919,0),(0,1079),(1919,1079)])
assert 0 < np.count_nonzero(alpha == 0) < pixel_count and np.count_nonzero(alpha >= 250) > pixel_count / 2

previews = {}
for name, bg in [('light', '#F5F0E8'), ('dark', '#24113D')]:
    previews['export_' + name] = save(composite(image, bg), 'MAP_BASE-export-1x-' + name + '.png')
    # 2/3 is the same uniform scale as the canvas contract; here offset is local to the viewport.
    display = Image.new('RGBA', (1280, 848), bg)
    display.alpha_composite(image.resize((1280,720), Image.Resampling.LANCZOS), (0,64))
    previews['display_' + name] = save(display.convert('RGB'), 'MAP_BASE-display-1x-' + name + '.png')

annotated = composite(image, '#F5F0E8').convert('RGBA')
overlay = Image.new('RGBA', image.size, (0,0,0,0))
draw = ImageDraw.Draw(overlay)
colors = ['#6D28D9', '#C55457', '#26776A', '#946022', '#354985', '#AB3FA2']
for index, (ident, polygon) in enumerate(polygons):
    color = colors[index]
    rgb = tuple(int(color[i:i+2],16) for i in (1,3,5))
    draw.polygon([tuple(p) for p in polygon], fill=rgb+(52,), outline=rgb+(255,), width=4)
    zone = next((z for z in contract['zones'] if z['catalog_id'] == ident), contract['central_site'])
    x,y = zone['building_ground_anchor_px']
    draw.line((x-10,y,x+10,y), fill=rgb+(255,), width=3)
    draw.line((x,y-10,x,y+10), fill=rgb+(255,), width=3)
    lx,ly = zone['label_anchor_px']
    text = ident + ' | label'
    bbox = draw.textbbox((0,0), text, font=font(22))
    tw = bbox[2]-bbox[0]
    draw.rounded_rectangle((lx-tw//2-12,ly-18,lx+tw//2+12,ly+18),radius=5,fill=(245,240,232,242),outline=rgb+(255,),width=2)
    draw.text((lx-tw//2,ly-15),text,fill=rgb+(255,),font=font(22))
    for px,py in polygon:
        draw.ellipse((px-4,py-4,px+4,py+4), fill=rgb+(255,))
        draw.text((px+8,py-22),f'{px},{py}', fill=rgb+(255,),font=font(17))
annotated.alpha_composite(overlay)
previews['contract'] = save(annotated.convert('RGB'), 'map-layout-annotated.png')

# Inspect native-scale edges and bridge detail on both QA backgrounds.
regions = [('river-top', [390,0,750,220]), ('university-edge', [40,210,320,460]),
           ('bridge', [710,160,945,350]), ('exterior-bottom', [940,910,1250,1080])]
edge_sheet = Image.new('RGB', (1450,760), '#DDD5E4')
d = ImageDraw.Draw(edge_sheet)
d.text((20,15),'MAP_BASE | detalhes 1:1 | marfim / ameixa',font=font(24),fill='#24113D')
left = 20
for name, rect in regions:
    crop = image.crop(tuple(rect))
    d.text((left,65),name,font=font(19),fill='#24113D')
    edge_sheet.paste(composite(crop,'#F5F0E8'),(left,98))
    edge_sheet.paste(composite(crop,'#24113D'),(left,390))
    left += crop.width + 16
previews['edges'] = save(edge_sheet, 'MAP_BASE-edges-1x.png')

sheet = Image.new('RGB', (1440,1660), '#DDD5E4')
d = ImageDraw.Draw(sheet)
d.text((28,18),'ART-10 | Geografia e contrato do mapa',font=font(29),fill='#24113D')
d.text((28,61),'1 PNG separado + coordenadas | 1920x1080 RGBA | polígonos e labels somente na QA',font=font(19),fill='#30243C')
for index, name in enumerate(['light','dark']):
    thumb = Image.open(PREVIEW / ('MAP_BASE-display-1x-' + name + '.png')).resize((640,424), Image.Resampling.LANCZOS)
    x=28+index*704
    sheet.paste(thumb,(x,108))
    d.text((x,542),name + ' | contain uniforme dentro de 1280x848',font=font(17),fill='#30243C')
sheet.paste(annotated.convert('RGB').resize((1344,756),Image.Resampling.LANCZOS),(48,597))
d.text((48,1368),'Polígonos: university, bank, store, condominium, marketing e implantação central.',font=font(21),fill='#24113D')
d.text((48,1407),'Cruzes: chão da construção futura. Pílulas: âncoras para texto nativo futuro.',font=font(20),fill='#30243C')
d.text((48,1446),'As marcações são apenas prova de QA; o PNG de produção contém somente geografia.',font=font(20),fill='#30243C')
d.text((48,1495),'Prédios ART-11, restaurantes/foreground ART-12 e realces ART-13 continuam planejados.',font=font(20),fill='#30243C')
previews['contact_sheet'] = save(sheet, 'ART-10-contact-sheet.png')

with Image.open(path) as probe:
    profile_present = bool(probe.info.get('icc_profile'))
assert before_hash == hashlib.sha256(path.read_bytes()).hexdigest()
write('docs/art/qa/ART-10-measurements.json', {
    'task_id':'ART-10', 'date':'2026-10-14', 'passed':True,
    'schema_valid':True, 'schema':'docs/art/schemas/map-layout.schema.json',
    'schema_validator':'jsonschema Draft202012Validator', 'schema_errors':[],
    'actual_size_px':list(image.size), 'mode':'RGBA', 'srgb_profile_present':profile_present,
    'sha256':before_hash, 'alpha_extrema':[int(alpha.min()),int(alpha.max())],
    'transparent_fraction':float(np.count_nonzero(alpha==0)/pixel_count),
    'opaque_fraction':float(np.count_nonzero(alpha==255)/pixel_count),
    'near_opaque_fraction_alpha_250_or_above':float(np.count_nonzero(alpha>=250)/pixel_count),
    'partial_alpha_fraction':float(np.count_nonzero((alpha>0)&(alpha<255))/pixel_count),
    'alpha_bbox':image.getchannel('A').getbbox(), 'corners_transparent':True,
    'nonoverlapping_polygons':True, 'overlap_pixels':overlap_pixels,
    'zone_metrics':zone_metrics, 'central_bounds_contain_footprint':True,
    'hit_area_ids_match_catalog':True, 'hit_areas_match_zone_polygons':True,
    'uniform_layer_origin_scale_pivot':True, 'display_map_size_px':[1280,720],
    'display_viewport_px':[1280,848], 'display_local_offset_px':[0,64],
    'contract_sha256':hashlib.sha256((ROOT/'docs/art/layouts/map-layout.json').read_bytes()).hexdigest(),
    'previews':previews, 'production_pixels_unchanged_by_qa':True,
    'alpha_note':'Alfa original preservado: os interiores dos seis lotes ficam entre 250 e 254 (98 a 99,6% de opacidade). Não foram artificialmente fixados em 255.',
    'note':'Geometric and technical checks require the accompanying visual inspection of footprint alignment and art.'
})
print(json.dumps({'task_id':'ART-10','schema_valid':True,'plot_count':len(polygons),'overlap_pixels':0,'previews':len(previews),'passed':True}))
