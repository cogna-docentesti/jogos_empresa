"""ART-30 technical export/QA only; illustration pixels and alpha come from ImageGen.

No repainting, segmentation, alpha cleanup, recoloring, scene integration or gameplay edits.
"""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import sys
import uuid
from PIL import Image, ImageCms, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[3]
TID = 'ART-30'
BASE = '11d6b7a8924810a0374f3a6c6e8b0e509881300b'
PROMPTS = 'docs/art/prompts/ART-30.json'
PREVIEWS = 'art-source/previews/ART-30'
SOURCE = 'art-source/editable/ART-30/produce_art30.py'


def read(p):
    return json.loads((ROOT / p).read_text(encoding='utf-8'))


def write(p, data):
    dest = ROOT / p
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(p):
    return hashlib.sha256((ROOT / p).read_bytes()).hexdigest()


def run(*args):
    p = subprocess.run(args, cwd=ROOT, text=True, capture_output=True, encoding='utf-8')
    if p.returncode:
        raise RuntimeError(p.stdout + p.stderr)
    return p.stdout


def prerequisites():
    manifest = read('docs/art/manifest.json')
    task = next(t for t in manifest['tasks'] if t['id'] == TID)
    report = {'task_id': TID, 'passed': True, 'dependencies': []}
    for dep in task['dependencies']:
        t = next(t for t in manifest['tasks'] if t['id'] == dep)
        assert t['status'] == 'validado', dep
        validation = json.loads(run(sys.executable, 'tools/art/validate_assets.py', '--project', str(ROOT), '--task', dep))
        assert validation['passed'], dep
        assets = []
        for a in manifest['assets']:
            if a['task_id'] != dep:
                continue
            path = manifest['asset_root'] + '/' + a['path']
            digest = sha(path)
            assert digest == a['sha256'], a['id']
            assets.append({'id': a['id'], 'path': path, 'sha256': digest})
        report['dependencies'].append({'task_id': dep, 'status': t['status'], 'validator_passed': True, 'assets': assets})
    write('docs/art/qa/ART-30-prerequisites.json', report)
    print('Prerequisites verified:', len(report['dependencies']))


def alpha_metrics(im):
    a = im.getchannel('A')
    hist = a.histogram()
    edge = max(a.crop((0, 0, im.width, 1)).getextrema()[1], a.crop((0, im.height-1, im.width, im.height)).getextrema()[1], a.crop((0, 0, 1, im.height)).getextrema()[1], a.crop((im.width-1, 0, im.width, im.height)).getextrema()[1])
    return {'alpha_extrema': list(a.getextrema()), 'alpha_bbox_ltrb_px': list(a.getbbox()), 'visible_bbox_alpha128_ltrb_px': list(a.point(lambda x: 255 if x >= 128 else 0).getbbox()), 'transparent_fraction': hist[0] / (im.width*im.height), 'opaque_fraction': hist[255] / (im.width*im.height), 'outer_edge_alpha_max': edge}


def ensure_meta(path):
    dest = ROOT / path
    if dest.exists():
        return
    guid = uuid.uuid4().hex
    dest.write_text('fileFormatVersion: 2\nguid: ' + guid + '\nTextureImporter:\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  isReadable: 0\n  maxTextureSize: 1024\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  spriteMode: 1\n  spriteExtrude: 1\n  spriteMeshType: 0\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 8\n  textureShape: 1\n  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 1024\n    textureFormat: -1\n    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n    overridden: 0\n  userData: ART-30 independent event vignette; no scene integration\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')


def export():
    m = read('docs/art/manifest.json')
    p = read(PROMPTS)
    generations = [g for g in p['generations'] if g['selected']]
    assert len(generations) == 5
    p['assets'] = []
    srgb = ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
    for a in [a for a in m['assets'] if a['task_id'] == TID]:
        assert a['status'] == 'planejado'
        g = next(g for g in generations if g['asset_id'] == a['id'])
        src = ROOT / g['source_path']
        dest_rel = m['asset_root'] + '/' + a['path']
        dest = ROOT / dest_rel
        if dest.exists():
            # Only retry this producer's uncommitted, unvalidated technical exports.
            tracked = subprocess.run(['git','cat-file','-e', BASE+':'+dest_rel], cwd=ROOT, capture_output=True)
            assert tracked.returncode != 0, 'Preserve assets present in reserved base: '+dest_rel
        src.parent.mkdir(parents=True, exist_ok=True)
        src.write_bytes(Path(g['origin_path']).read_bytes())
        im = Image.open(src)
        assert im.mode == 'RGBA'
        g['source_size_px'] = list(im.size)
        g['source_sha256'] = sha(g['source_path'])
        target = tuple(a['export_size_px'])
        # Keep a transparent safety margin, including faint generated contact shadows.
        inner = (target[0]-32, target[1]-32)
        small = ImageOps.contain(im, inner, Image.Resampling.LANCZOS)
        offset = ((target[0]-small.width)//2, (target[1]-small.height)//2)
        out = Image.new('RGBA', target, (0, 0, 0, 0))
        out.paste(small, offset)
        dest.parent.mkdir(parents=True, exist_ok=True)
        out.save(dest, compress_level=9, icc_profile=srgb)
        ensure_meta(dest_rel + '.meta')
        for folder in [dest.parent, dest.parent.parent]:
            if folder.name in ['illustrations', 'events'] and not folder.with_suffix('.meta').exists():
                folder.with_suffix('.meta').write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')
        metrics = alpha_metrics(out)
        assert metrics['transparent_fraction'] > 0 and metrics['outer_edge_alpha_max'] == 0
        rec = {'id': a['id'], 'path': dest_rel, 'status': 'gerado', 'actual_size_px': list(out.size), 'mode': out.mode, 'sha256': sha(dest_rel), 'source_path': g['source_path'], 'source_size_px': list(im.size), 'source_sha256': g['source_sha256'], 'tool': 'image_gen.imagegen', 'model': None, 'references': g['references'], 'reference_sha256': {ref: sha(ref) for ref in g['references']}, 'catalog_source': a['source'], 'catalog_source_sha256': sha(a['source']), 'character_ids': [], 'identity_qa': 'not_applicable_no_people', 'alpha': metrics, 'technical_export': {'tool': 'Pillow', 'operation': 'Uniform Lanczos reduction of the full source frame, preserving generated RGBA; centered transparent safety padding. No crop, repaint, background removal, alpha cleanup or independent x/y scaling.', 'source_size_px': list(im.size), 'resampled_size_px': list(small.size), 'transparent_offset_px': list(offset), 'output_size_px': list(out.size), 'producer_source': SOURCE}, 'display_size_px': a['display_size_px'], 'display_fit': 'contain', 'pivot': a['pivot'], 'nine_slice_lbrt_px': a['nine_slice_lbrt_px']}
        p['assets'].append(rec)
        a.update(status='gerado', actual_size_px=list(out.size), sha256=rec['sha256'], generation_prompt=g['prompt'], production_method='imagegen', generation_tool='image_gen.imagegen', generation_model=None, source_image=g['source_path'], source_image_sha256=g['source_sha256'], production_record=PROMPTS, qa_record='docs/art/qa/ART-30.json', qa={'technical': False, 'visual': False, 'identity': None})
    task = next(t for t in m['tasks'] if t['id'] == TID)
    task.update(status='gerado', production_record=PROMPTS, qa_record='docs/art/qa/ART-30.json', delivery_preview=PREVIEWS+'/ART-30-contact-sheet.png')
    tasks = read('docs/art/tasks.json')
    next(t for t in tasks if t['id'] == TID).update(task)
    p['producer_source'] = SOURCE
    write('docs/art/manifest.json', m)
    write('docs/art/tasks.json', tasks)
    write(PROMPTS, p)
    print(json.dumps([{'id': a['id'], 'source': a['source_size_px'], 'final': a['actual_size_px'], 'alpha': a['alpha']} for a in p['assets']], indent=2))


def previews():
    m = read('docs/art/manifest.json')
    assets = [a for a in m['assets'] if a['task_id'] == TID]
    folder = ROOT / PREVIEWS
    folder.mkdir(parents=True, exist_ok=True)
    rows = []
    for a in assets:
        im = Image.open(ROOT / m['asset_root'] / a['path'])
        for label, size in [('native', (768,512)), ('display', (640,426))]:
            row = Image.new('RGB', (size[0]*2, size[1]+32), '#F5F0E8')
            d = ImageDraw.Draw(row)
            d.text((12,8), a['catalog_id']+' / '+label+' / contain', fill='#30243C')
            contained = ImageOps.contain(im, size, Image.Resampling.LANCZOS)
            for i,bg in enumerate(['#F5F0E8','#24113D']):
                canvas = Image.new('RGBA',size,bg)
                canvas.alpha_composite(contained, ((size[0]-contained.width)//2,(size[1]-contained.height)//2))
                row.paste(canvas, (i*size[0],32))
            row.save(folder / (a['catalog_id']+'-'+label+'-light-dark.png'))
            if label == 'display':
                rows.append(row)
    sheet = Image.new('RGB',(1280,sum(row.height for row in rows)),'#F5F0E8')
    y = 0
    for row in rows:
        sheet.paste(row,(0,y)); y += row.height
    sheet.save(folder / 'ART-30-contact-sheet.png')
    # Preliminary QA files belong to this producer and are superseded by named final proofs.
    for n in [0,1]:
        old = folder / ('initial-'+str(n)+'.png')
        if old.exists():
            old.unlink()
    print('Created 10 individual QA previews and contact sheet')


def validate():
    for args, dest in [(['--task', TID], 'ART-30-technical.json'), (['--plan'], 'ART-30-plan-validation.json')]:
        data = json.loads(run(sys.executable,'tools/art/validate_assets.py','--project',str(ROOT),*args))
        assert data['passed']
        write('docs/art/qa/'+dest,data)
    print('Task and plan validators passed')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['prerequisites','export','previews','validate'])
    action = parser.parse_args().action
    globals()[action]()
