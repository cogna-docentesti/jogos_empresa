"""Technical export and inspection proofs for the single ART-38 ImageGen sprite.

Artwork and transparency originate in integrated ImageGen. This script performs
only a uniform 3/4 resample, transparent padding, sRGB tagging, import metadata,
and QA composites. It does not repaint or segment the generated character.
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
import uuid
from PIL import Image, ImageCms, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
TASK = 'ART-38'
BASE = '4a7143c54c10e4d457b50ffed03891b4debdbfdc'
FINAL = 'Assets/Art/new/characters/CH01/ch01_guiding_question.png'
MASTER = 'Assets/Art/new/characters/CH01/ch01_canonical.png'
SOURCE = 'art-source/editable/ART-38/generations/ch01_guiding_question-01.png'
PRODUCER = 'art-source/editable/ART-38/produce_art38.py'
PREVIEW = 'art-source/previews/ART-38'
PROMPTS = 'docs/art/prompts/ART-38.json'
BACKGROUNDS = ['#F5F0E8', '#24113D']


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8'))


def write(path, value):
    dest = ROOT / path
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, encoding='utf-8').strip()


def alpha_metrics(im):
    alpha = im.getchannel('A')
    hist = alpha.histogram()
    w, h = im.size
    edges = [(0, 0, w, 1), (0, h-1, w, h), (0, 0, 1, h), (w-1, 0, w, h)]
    bbox = alpha.getbbox()
    return {
        'alpha_extrema': list(alpha.getextrema()),
        'alpha_bbox_ltrb_px': list(bbox),
        'visible_bbox_alpha128_ltrb_px': list(alpha.point(lambda x: 255 if x >= 128 else 0).getbbox()),
        'transparent_fraction': hist[0] / (w*h),
        'opaque_fraction': hist[255] / (w*h),
        'outer_edge_alpha_max': max(alpha.crop(e).getextrema()[1] for e in edges),
        'transparent_margins_ltrb_px': [bbox[0], bbox[1], w-bbox[2], h-bbox[3]],
    }


def composite(im, bg):
    out = Image.new('RGBA', im.size, bg)
    out.alpha_composite(im)
    return out.convert('RGB')


def contain(im, box=(420, 760)):
    scale = min(box[0] / im.width, box[1] / im.height)
    size = (round(im.width*scale), round(im.height*scale))
    out = Image.new('RGBA', box)
    out.alpha_composite(im.resize(size, Image.Resampling.LANCZOS),
                        ((box[0]-size[0])//2, (box[1]-size[1])//2))
    return out


def save_preview(name, im):
    path = ROOT / PREVIEW / name
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path)
    return PREVIEW + '/' + name


def export():
    assert git('rev-parse', 'HEAD') == BASE
    assert git('branch', '--show-current') == 'dev/new-screens'
    prereq = read('docs/art/qa/ART-38-prerequisites.json')
    for a in prereq['baseline_assets']:
        assert sha(a['path']) == a['sha256'], a['id']
    m = read('docs/art/manifest.json')
    a = next(a for a in m['assets'] if a['task_id'] == TASK)
    assert a['id'] == 'CH01_GUIDING' and a['status'] == 'planejado'
    assert not (ROOT / FINAL).exists(), 'Do not overwrite an existing final.'
    im = Image.open(ROOT / SOURCE)
    assert im.mode == 'RGBA' and im.size == (1024, 1536)
    out = Image.new('RGBA', (1024, 1536))
    out.alpha_composite(im.resize((768, 1152), Image.Resampling.LANCZOS), (128, 192))
    profile = ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
    out.save(ROOT / FINAL, icc_profile=profile)
    metrics = alpha_metrics(out)
    assert metrics['outer_edge_alpha_max'] == 0
    l, t, r, b = metrics['transparent_margins_ltrb_px']
    assert min(l, r) >= 103 and min(t, b) >= 154
    meta = (ROOT / 'Assets/Art/new/characters/CH01/ch01_concerned.png.meta').read_text(encoding='utf-8')
    meta = re.sub(r'^guid: [0-9a-f]+$', 'guid: ' + uuid.uuid4().hex, meta, flags=re.M)
    meta = re.sub(r'^    spriteID: [0-9a-f]+$', '    spriteID: ' + uuid.uuid4().hex, meta, flags=re.M)
    meta = re.sub(r'^  userData:.*$', '  userData: ART-38 CH01 documentary guiding pose; no scene integration', meta, flags=re.M)
    assert not (ROOT / (FINAL + '.meta')).exists()
    (ROOT / (FINAL + '.meta')).write_text('\n'.join(line.rstrip() for line in meta.splitlines())+'\n', encoding='utf-8')
    p = read(PROMPTS)
    g = p['generations'][0]
    assert g['source_sha256'] == sha(SOURCE)
    g['selected'] = True
    refs = [MASTER, 'art-source/references/concepts/03-gestao.png']
    record = {
        'id': a['id'], 'path': FINAL, 'status': 'gerado',
        'actual_size_px': list(out.size), 'mode': out.mode, 'sha256': sha(FINAL),
        'source_path': SOURCE, 'source_size_px': list(im.size), 'source_sha256': sha(SOURCE),
        'tool': 'image_gen.imagegen', 'model': None,
        'references': refs, 'reference_sha256': {r: sha(r) for r in refs},
        'character_ids': ['CH01'], 'canonical_asset': MASTER, 'canonical_sha256': sha(MASTER),
        'alpha': metrics,
        'technical_export': {
            'tool': 'Pillow', 'uniform_scale': 0.75,
            'operation': 'Uniform full-frame 3/4 Lanczos reduction, transparent padding and sRGB profile. Generated RGBA preserved; no crop, repaint, segmentation or alpha cleanup.',
            'source_size_px': [1024, 1536], 'resampled_size_px': [768, 1152],
            'transparent_offset_px': [128, 192], 'output_size_px': [1024, 1536],
            'producer_source': PRODUCER,
        },
        'display_size_px': [420, 760], 'display_fit': 'contain',
        'display_raster_size_px': [420, 630], 'display_offset_px': [0, 65],
        'pivot': [0.5, 0.5], 'nine_slice_lbrt_px': [0, 0, 0, 0],
        'qa': {'technical': False, 'visual': False, 'identity': False},
    }
    p['assets'] = [record]
    write(PROMPTS, p)
    a.update(status='gerado', source='Canonical CH01 master; new pose via integrated image_gen.imagegen',
             references=refs, sha256=record['sha256'], actual_size_px=[1024, 1536],
             generation_prompt=g['prompt'], generation_tool='image_gen.imagegen', generation_model=None,
             generation_model_note=p['model_note'], production_record=PROMPTS,
             source_raster=SOURCE, technical_export=record['technical_export'])
    write('docs/art/manifest.json', m)
    print('Exported one independent 1024x1536 RGBA sprite; prior 269 PNG hashes preserved.')


def proofs():
    im = Image.open(ROOT / FINAL).convert('RGBA')
    master = Image.open(ROOT / MASTER).convert('RGBA')
    paths = []
    for bg, name in zip(BACKGROUNDS, ['light', 'dark']):
        paths.append(save_preview('CH01_GUIDING-native-' + name + '.png', composite(im, bg)))
    display = contain(im)
    board = Image.new('RGB', (840, 760))
    for i, bg in enumerate(BACKGROUNDS):
        board.paste(composite(display, bg), (420*i, 0))
    paths.append(save_preview('CH01_GUIDING-display-light-dark.png', board))
    board = Image.new('RGB', (1680, 800))
    draw = ImageDraw.Draw(board)
    for i, (sprite, bg, label) in enumerate([
        (master, BACKGROUNDS[0], 'CH01 canonical / light'),
        (im, BACKGROUNDS[0], 'CH01 guiding / light'),
        (master, BACKGROUNDS[1], 'CH01 canonical / dark'),
        (im, BACKGROUNDS[1], 'CH01 guiding / dark'),
    ]):
        board.paste(composite(contain(sprite), bg), (420*i, 40))
        draw.rectangle((420*i, 0, 420*(i+1)-1, 39), fill=bg)
        draw.text((420*i+16, 12), label, fill='#30243C' if i < 2 else '#F5F0E8')
    paths.append(save_preview('CH01-identity-comparison.png', board))
    # Native 1:1 crops from final and master; two backgrounds, no resampling.
    board = Image.new('RGB', (1360, 720))
    draw = ImageDraw.Draw(board)
    samples = [
        (master, (370, 210, 650, 430), 'Canonical face'),
        (im, (380, 220, 660, 440), 'Guiding face'),
        (im, (590, 470, 830, 650), 'Open palm'),
        (im, (335, 710, 500, 880), 'Resting hand'),
    ]
    for row, bg in enumerate(BACKGROUNDS):
        draw.rectangle((0, row*360, 1360, (row+1)*360), fill=bg)
        for col, (sprite, box, label) in enumerate(samples):
            draw.text((col*340+16, row*360+20), label+' / 1:1', fill='#30243C' if row == 0 else '#F5F0E8')
            crop = composite(sprite, bg).crop(box)
            board.paste(crop, (col*340+16, row*360+60))
    paths.append(save_preview('CH01_GUIDING-details-1x.png', board))
    # Correctly proportioned diagnostic enlargement of the raw generated hand.
    raw = Image.open(ROOT / SOURCE)
    board = Image.new('RGB', (1040, 560))
    for i, bg in enumerate(BACKGROUNDS):
        crop = composite(raw, bg).crop((680, 370, 940, 650)).resize((520, 560), Image.Resampling.LANCZOS)
        board.paste(crop, (i*520, 0))
    save_preview('CH01_GUIDING-initial-hand-detail.png', board)
    write('docs/art/qa/ART-38-preview-index.json', {
        'task_id': TASK, 'previews': paths,
        'backgrounds': BACKGROUNDS, 'native_proofs': '1024x1536 per background',
        'display_box_px': [420, 760], 'contained_raster_px': [420, 630],
        'display_offset_px': [0, 65], 'display_fit': 'contain',
        'purpose': 'Asset inspection and identity comparison only; no proposed final UI.',
    })
    print('Created five final inspection proofs plus two raw generation diagnostics.')


def validate():
    for selector, output in [(['--task', TASK], 'ART-38-technical.json'),
                             (['--plan'], 'ART-38-plan-validation.json')]:
        r = subprocess.run([sys.executable, 'tools/art/validate_assets.py', '--project', str(ROOT), *selector],
                           cwd=ROOT, capture_output=True, encoding='utf-8')
        report = json.loads(r.stdout)
        write('docs/art/qa/' + output, report)
        assert r.returncode == 0 and report['passed'], report
    print('Task and plan validation passed.')


if __name__ == '__main__':
    {'export': export, 'proofs': proofs, 'validate': validate}[sys.argv[1]]()
