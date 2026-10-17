"""Acquire pinned Google Fonts, build real static faces, and audit their binaries.

Python 3.10 + requirements-font02.txt. No OS fonts or Unity scene writes.
See docs/ui-update/font-tasks/FONT-02-download-e-licencas.md for commands.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import platform
import sys
from urllib.parse import quote
from urllib.request import Request, urlopen

import fontTools
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ROOT = Path(__file__).resolve().parents[2]
BASE = Path('Assets/TextMesh Pro/Fonts/Concept')
OUT = ROOT / 'output/ui-update/fonts/FONT-02'
MANIFEST = ROOT / 'docs/ui-update/font-sources.json'
FAMILIES = [
    dict(family='Roboto', slug='roboto', stem='Roboto',
         revision='1c627bfa375fc51cf86fabeca4f6e08a95f0aa5c',
         filename='Roboto[wdth,wght].ttf', weights=[400, 700], width=100),
    dict(family='Roboto Condensed', slug='robotocondensed', stem='RobotoCondensed',
         revision='69d0549e8fc7ad72e1531734c81237be401ec4fd',
         filename='RobotoCondensed[wght].ttf', weights=[400, 500, 600, 700]),
]
STYLES = {400: 'Regular', 500: 'Medium', 600: 'SemiBold', 700: 'Bold'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def dump(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def name(font, key):
    return font['name'].getDebugName(key)


def metadata(font):
    return dict(names={str(k): name(font, k) for k in [0, 1, 2, 3, 4, 5, 6, 13, 14, 16, 17]},
                weight=font['OS/2'].usWeightClass, width_class=font['OS/2'].usWidthClass,
                italic_angle=font['post'].italicAngle, revision=font['head'].fontRevision,
                fs_type=font['OS/2'].fsType,
                axes=[dict(tag=a.axisTag, minimum=a.minValue, default=a.defaultValue, maximum=a.maxValue)
                      for a in font['fvar'].axes] if 'fvar' in font else [],
                glyphs=len(font.getGlyphOrder()), codepoints=len(font.getBestCmap()))


def outline_hash(font):
    glyphs = font.getGlyphSet()
    digest = hashlib.sha256()
    for glyph in font.getGlyphOrder():
        pen = DecomposingRecordingPen(glyphs)
        glyphs[glyph].draw(pen)
        digest.update(repr((glyph, pen.value)).encode())
    return digest.hexdigest()


def canonical_names(font, family, style):
    """Keep explicit faces readable by Unity/FreeType, including Medium/SemiBold.

    Only naming/style bits are normalized; geometry is supplied by the instancer.
    Preserve upstream copyright, version, license and designer records.
    """
    values = {1: family, 2: style, 3: f'{family.replace(" ", "")}-{style};{name(font, 5)}',
              4: f'{family} {style}', 6: f'{family.replace(" ", "")}-{style}',
              16: family, 17: style}
    table = font['name']
    for key, value in values.items():
        table.removeNames(nameID=key)
        table.setName(value, key, 3, 1, 0x409)
        table.setName(value, key, 1, 0, 0)
    # All faces are upright; only the actual 700 face has the Bold style bit.
    font['OS/2'].fsSelection &= ~((1 << 0) | (1 << 5) | (1 << 6))
    font['OS/2'].fsSelection |= (1 << 5) if style == 'Bold' else (1 << 6)
    font['head'].macStyle = 1 if style == 'Bold' else 0


def build(source, spec, weight):
    font = TTFont(io.BytesIO(source), recalcTimestamp=False)
    axes = {'wght': weight}
    if 'width' in spec:
        axes['wdth'] = spec['width']
    font = instantiateVariableFont(font, axes, inplace=True, optimize=True, updateFontNames=True)
    canonical_names(font, spec['family'], STYLES[weight])
    # Deterministic upstream timestamps, never the machine clock.
    font.recalcTimestamp = False
    result = io.BytesIO()
    font.save(result, reorderTables=True)
    return result.getvalue(), axes


def acquire():
    records = []
    for spec in FAMILIES:
        folder = BASE / 'Sources' / spec['stem']
        for remote, local in [(spec['filename'], spec['filename']), ('OFL.txt', 'OFL.txt'),
                              ('METADATA.pb', 'METADATA.pb.txt'), ('upstream_info.md', 'upstream_info.md')]:
            url = f'https://raw.githubusercontent.com/google/fonts/{spec["revision"]}/ofl/{spec["slug"]}/{quote(remote)}'
            with urlopen(Request(url, headers={'User-Agent': 'Cogna-FONT-02'}), timeout=45) as response:
                data, final = response.read(), response.url
                record = dict(path=(folder / local).as_posix(), requested_url=url, final_url=final,
                              repository='https://github.com/google/fonts', revision=spec['revision'],
                              family=spec['family'], sha256=sha(data), bytes=len(data))
            target = ROOT / record['path']
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            records.append(record)
            print('Acquired', record['path'], flush=True)
    dump(OUT / 'acquisition.json', records)


def load_sources():
    previous = json.loads(MANIFEST.read_text(encoding='utf-8')) if MANIFEST.exists() else None
    records = previous['official_files'] if previous else json.loads((OUT / 'acquisition.json').read_text(encoding='utf-8'))
    for row in records:
        assert sha((ROOT / row['path']).read_bytes()) == row['sha256'], row['path']
    return records


def audit(generate):
    assert fontTools.__version__ == '4.60.1', 'Use requirements-font02.txt for byte-identical output'
    records = load_sources()
    corpus = json.loads((ROOT / 'output/ui-update/fonts/FONT-01/glyph-corpus.json').read_text(encoding='utf-8'))
    actual = {int(c[2:], 16) for c in corpus['actual_codepoints']}
    primary = {c if isinstance(c, int) else int(c[2:], 16) for c in corpus['primary_codepoints']}
    sources, faces, reuse = [], [], []
    for spec in FAMILIES:
        source_path = BASE / 'Sources' / spec['stem'] / spec['filename']
        source = (ROOT / source_path).read_bytes()
        font = TTFont(io.BytesIO(source), recalcTimestamp=False)
        license_path = source_path.parent / 'OFL.txt'
        license_text = (ROOT / license_path).read_text(encoding='utf-8')
        assert 'SIL OPEN FONT LICENSE Version 1.1' in license_text
        assert not any('Reserved Font Name' in line for line in license_text.split('PREAMBLE')[0].splitlines())
        source_meta = metadata(font)
        sources.append(dict(path=source_path.as_posix(), sha256=sha(source), **source_meta,
                            license=license_path.as_posix(), license_id='OFL-1.1',
                            copyright=license_text.splitlines()[0]))
        research = ROOT / f'output/ui-update/font-identification/{spec["stem"]}.ttf'
        reuse.append(dict(path=research.relative_to(ROOT).as_posix(), sha256=sha(research.read_bytes()),
                          matches_official_bytes=research.read_bytes() == source,
                          decision='Official pinned copy under Assets; research path is never a production dependency.'))
        for weight in spec['weights']:
            static = BASE / 'Static' / f'{spec["stem"]}-{STYLES[weight]}.ttf'
            generated, axes = build(source, spec, weight)
            if generate:
                (ROOT / static).parent.mkdir(parents=True, exist_ok=True)
                (ROOT / static).write_bytes(generated)
            data = (ROOT / static).read_bytes()
            assert data == generated, f'Non-reproducible static file: {static}'
            result = TTFont(io.BytesIO(data), recalcTimestamp=False, checkChecksums=2)
            meta = metadata(result)
            assert meta['axes'] == [] and 'gvar' not in result and 'HVAR' not in result
            assert meta['weight'] == weight and name(result, 2) == STYLES[weight]
            assert name(result, 1) == spec['family'] and meta['italic_angle'] == 0
            assert meta['fs_type'] == 0
            # Compare every decomposed outline with a fresh unrenamed official instance.
            reference = instantiateVariableFont(TTFont(io.BytesIO(source), recalcTimestamp=False), axes, inplace=True)
            reference.recalcTimestamp = False
            reference_bytes = io.BytesIO()
            reference.save(reference_bytes, reorderTables=True)
            reference = TTFont(io.BytesIO(reference_bytes.getvalue()), recalcTimestamp=False)
            outlines = outline_hash(result)
            assert outlines == outline_hash(reference), 'Naming changed geometry'
            assert result['hmtx'].metrics == reference['hmtx'].metrics
            cmap = result.getBestCmap()
            missing_actual = sorted(actual - set(cmap))
            missing_primary = sorted(primary - set(cmap))
            # Source coverage is reported, never silently substituted. FONT-03 owns fallbacks.
            sample = {c: dict(glyph=cmap[ord(c)], advance=result['hmtx'].metrics[cmap[ord(c)]][0],
                               bounds=list(result['glyf'][cmap[ord(c)]].__dict__.get(k, 0) for k in ['xMin','yMin','xMax','yMax']))
                      for c in 'AHMãoç0123'}
            faces.append(dict(path=static.as_posix(), source=source_path.as_posix(), axes_applied=axes,
                              sha256=sha(data), bytes=len(data), **meta,
                              outline_sha256=outlines, outlines_match_official_instance=True,
                              metrics_match_official_instance=True, reproducible_bytes=True,
                              missing_actual=[f'U+{c:04X}' for c in missing_actual],
                              missing_primary=[f'U+{c:04X}' for c in missing_primary], samples=sample))
            print('Verified', static.name, meta['names']['2'], len(cmap), 'codepoints', flush=True)
    assert len({f['sha256'] for f in faces}) == 6
    assert len({f['outline_sha256'] for f in faces}) == 6, 'Duplicate face geometry'
    example = ROOT / 'Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf'
    reuse.append(dict(path=example.relative_to(ROOT).as_posix(), sha256=sha(example.read_bytes()),
                      **metadata(TTFont(example)), decision='Preserved sample version 2.137; production uses coherent official 3.x pair.'))
    report = dict(schema_version=1, task='FONT-02', date='2026-10-17',
                  scope='Six upright source faces only; TMP generation/integration remains FONT-03 onward.',
                  official_files=records, variable_sources=sources, static_faces=faces, existing_audit=reuse,
                  generation=dict(script='tools/ui/font02_sources.py', python=platform.python_version(),
                                  fonttools=fontTools.__version__, requirements='tools/ui/requirements-font02.txt',
                                  naming='Explicit family/style IDs 1/2/3/4/6/16/17; upright OS/2 and head style bits. Geometry unchanged.',
                                  timestamps='Upstream head timestamps preserved; recalcTimestamp=False',
                                  verify='Regenerates all six in memory; byte comparison; every decomposed glyph outline and hmtx compared to official instances.'),
                  coverage=dict(actual_count=len(actual), planned_primary_count=len(primary),
                                note='Missing planned symbols require explicit FONT-03 fallback; no silent system font or Inter Tight substitution.'),
                  italic_required=False, production_dependencies_outside_assets=[])
    if generate:
        dump(MANIFEST, report)
    else:
        expected = json.loads(MANIFEST.read_text(encoding='utf-8'))
        assert report['static_faces'] == expected['static_faces'], 'Manifest differs from audited binaries'
        dump(OUT / 'binary-verification.json', dict(passed=True, face_count=6, unique_outline_hashes=6,
             all_bytes_reproduced=True, all_outlines_and_metrics_match=True, fonttools=fontTools.__version__,
             faces=[dict(path=f['path'], sha256=f['sha256'], missing_actual=f['missing_actual'],
                         missing_primary=f['missing_primary']) for f in faces]))


def specimen():
    from PIL import Image, ImageDraw, ImageFont
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    canvas = Image.new('RGB', (1600, 950), '#F5F0E8')
    draw = ImageDraw.Draw(canvas)
    label_font = ImageFont.load_default(size=23)
    draw.text((36, 20), 'FONT-02 | Static production faces | Same 46 px size / scale 1', font=label_font, fill='#24113D')
    samples = []
    text = 'Jogos de Empresa | Gestão 2026 R$ 1.234,56'
    for i, face in enumerate(manifest['static_faces']):
        y = 80 + i * 140
        font = ImageFont.truetype(str(ROOT / face['path']), 46)
        draw.text((36, y), face['names']['1'] + ' ' + face['names']['2'] + ' | ' + str(face['weight']), font=label_font, fill='#65566F')
        draw.text((36, y + 36), text, font=font, fill='#30243C')
        mask = Image.new('L', (1500, 95))
        ImageDraw.Draw(mask).text((0, 0), text, font=font, fill=255)
        samples.append(dict(path=face['path'], text=text, pixel_size=46, advance=font.getlength(text),
                            ink_area=sum(mask.getdata()) / 255))
    for family in ['Roboto', 'Roboto Condensed']:
        areas = [s['ink_area'] for s, f in zip(samples, manifest['static_faces']) if f['names']['1'] == family]
        assert areas == sorted(areas) and len(set(areas)) == len(areas)
    canvas.save(OUT / 'static-specimen.png')
    dump(OUT / 'specimen-metrics.json', dict(passed=True, scale=1, size=46, samples=samples,
         weight_ink_area_strictly_increases=True,
         note='TTF source specimen using FreeType/Pillow, not a TMP atlas or production Game View approval.'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['fetch', 'generate', 'verify', 'specimen'])
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    if args.action == 'fetch':
        acquire()
    elif args.action == 'specimen':
        specimen()
    else:
        audit(args.action == 'generate')
