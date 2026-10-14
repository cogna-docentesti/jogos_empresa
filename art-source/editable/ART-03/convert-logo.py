"""Reuse the official palette PNG without changing any RGBA pixel."""
from pathlib import Path
import hashlib
import json
from PIL import Image, PngImagePlugin

project = Path(__file__).resolve().parents[3]
source_path = 'Assets/Art/Sprites/Identification/logo-cogna.png'
output_path = 'Assets/Art/new/shared/brand/logo_cogna.png'
target = project / output_path
if target.exists():
    raise SystemExit('Existing production logo will not be overwritten.')
with Image.open(project / source_path) as source:
    rgba = source.convert('RGBA')
    if rgba.size != (512, 512):
        raise SystemExit('Official logo needs size review; do not stretch it.')
    target.parent.mkdir(parents=True, exist_ok=True)
    metadata = PngImagePlugin.PngInfo()
    metadata.add(b'sRGB', b'\x00')
    rgba.save(target, pnginfo=metadata, compress_level=9)
with Image.open(target) as exported:
    assert exported.mode == 'RGBA'
    assert exported.tobytes() == rgba.tobytes()

hash_file = lambda p: hashlib.sha256((project / p).read_bytes()).hexdigest()
record_path = project / 'docs/art/prompts/ART-03.json'
record = json.loads(record_path.read_text(encoding='utf-8'))
record['assets'].append({
    'id': 'BRAND_COGNA_LOGO', 'output_path': output_path,
    'source_editable': None, 'origin': source_path,
    'origin_sha256': hash_file(source_path), 'sha256': hash_file(output_path),
    'references': [{'path': source_path, 'sha256': hash_file(source_path)}],
    'final_prompt': 'Reutilizar o logo oficial existente sem redesenhar, recolorir, recortar ou reinterpretar. Converter PNG paletizado com tRNS para RGBA 8-bit sRGB em 512×512, preservando todos os pixels RGBA e a transparência. Sem moldura ou slogan adicional. Display contain 120×120, pivot central.',
    'tool': 'Pillow: Image.convert(RGBA), PNG export', 'model': None,
    'actual_size_px': [512, 512], 'mode': 'RGBA', 'color_space': 'sRGB',
    'rgba_pixels_equal_to_origin': True, 'alpha_equal_to_origin': True,
    'nine_slice_lbrt_px': [0, 0, 0, 0], 'pivot': [0.5, 0.5], 'status': 'gerado'
})
record['logo_converter_source'] = 'art-source/editable/ART-03/convert-logo.py'
record_path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

manifest_path = project / 'docs/art/manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
by_id = {a['id']: a for a in record['assets']}
for asset in manifest['assets']:
    if asset['task_id'] != 'ART-03':
        continue
    produced = by_id[asset['id']]
    asset.update({
        'status': 'gerado', 'source': produced.get('origin', 'SVG editável; rasterização determinística sharp/librsvg'),
        'sha256': produced['sha256'], 'actual_size_px': produced['actual_size_px'],
        'generation_prompt': produced['final_prompt'], 'generation_tool': produced['tool'],
        'generation_model': None, 'generation_model_note': 'Lote geométrico ou reutilização de marca; nenhum modelo generativo.',
        'production_record': 'docs/art/prompts/ART-03.json'
    })
    if asset['id'] == 'UI_SCRIM':
        # A solid pixel is a full-screen overlay, not an aspect-preserved object.
        asset['display_fit'] = 'stretch'
        asset['display_fit_note'] = 'Pixel sólido expandido para preencher a caixa inteira; não se aplica contain a um scrim de 1×1.'
    if asset['id'] == 'BRAND_COGNA_LOGO':
        asset['references'] = [source_path]
for task in manifest['tasks']:
    if task['id'] == 'ART-03':
        task['status'] = 'gerado'
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
tasks_path = project / 'docs/art/tasks.json'
tasks = json.loads(tasks_path.read_text(encoding='utf-8'))
for task in tasks:
    if task['id'] == 'ART-03':
        task['status'] = 'gerado'
tasks_path.write_text(json.dumps(tasks, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'converted_logo': output_path, 'rgba_pixels_equal_to_origin': True, 'generated_assets': len(by_id)}))
