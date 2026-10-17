"""FONT-03 pinned fallback sources and preservation checks (fontTools from FONT-02)."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
from urllib.parse import quote
from urllib.request import Request, urlopen

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from font02_sources import canonical_names, metadata

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/ui-update/fonts/FONT-03'
BASE = ROOT / 'Assets/TextMesh Pro/Fonts/Concept'
REV = '1c627bfa375fc51cf86fabeca4f6e08a95f0aa5c'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def baseline():
    OUT.mkdir(parents=True, exist_ok=True)
    assert not (OUT / 'baseline.json').exists(), 'Do not replace the initial baseline'
    files = subprocess.check_output(['git', 'ls-files', '-co', '--exclude-standard', '-z'], cwd=ROOT).decode().split('\0')
    records = {p: sha((ROOT / p).read_bytes()) for p in sorted(set(files)) if p and (ROOT / p).is_file() and not p.startswith('output/ui-update/fonts/FONT-03/')}
    write(OUT / 'baseline.json', {'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip(), 'files': records})
    for args, name in [(['status', '--short'], 'initial-status.txt'), (['diff', '--binary'], 'initial-tracked.patch'), (['diff', '--cached', '--binary'], 'initial-staged.patch')]:
        (OUT / name).write_bytes(subprocess.check_output(['git', *args], cwd=ROOT))
    print(f'Baseline: {len(records)} files')

def fetch():
    folder = BASE / 'Sources/NotoSans'
    folder.mkdir(parents=True, exist_ok=True)
    rows = []
    for name in ['NotoSans[wdth,wght].ttf', 'OFL.txt', 'METADATA.pb']:
        url = f'https://raw.githubusercontent.com/google/fonts/{REV}/ofl/notosans/{quote(name)}'
        data = urlopen(Request(url, headers={'User-Agent': 'Cogna-FONT-03'}), timeout=60).read()
        (folder / name).write_bytes(data)
        rows.append(dict(path=str((folder / name).relative_to(ROOT)).replace('\\', '/'), url=url, sha256=sha(data), bytes=len(data)))
    write(OUT / 'fallback-acquisition.json', rows)
    symbols = BASE / 'Sources/NotoSansMath'
    symbols.mkdir(parents=True, exist_ok=True)
    for name in ['NotoSansMath-Regular.ttf', 'OFL.txt', 'METADATA.pb']:
        url = f'https://raw.githubusercontent.com/google/fonts/{REV}/ofl/notosansmath/{quote(name)}'
        data = urlopen(Request(url, headers={'User-Agent': 'Cogna-FONT-03'}), timeout=60).read()
        (symbols / name).write_bytes(data)
        rows.append(dict(path=str((symbols / name).relative_to(ROOT)).replace('\\', '/'), url=url, sha256=sha(data), bytes=len(data)))
    write(OUT / 'fallback-acquisition.json', rows)
    generate(False)

def generate(verify):
    source = BASE / 'Sources/NotoSans/NotoSans[wdth,wght].ttf'
    corpus = json.loads((ROOT / 'output/ui-update/fonts/FONT-01/glyph-corpus.json').read_text(encoding='utf-8-sig'))
    primary = TTFont(BASE / 'Static/Roboto-Regular.ttf').getBestCmap()
    missing = sorted(set(corpus['primary_codepoints']) - set(primary))
    source_font = TTFont(source)
    remaining = set(missing) - set(source_font.getBestCmap())
    print('Fallback missing:', [f'U+{c:04X}' for c in sorted(remaining)])
    symbols = TTFont(BASE / 'Sources/NotoSansMath/NotoSansMath-Regular.ttf')
    assert not remaining - set(symbols.getBestCmap()), 'Pinned fallbacks must cover the declared gap'
    rows = []
    for weight, style in [(400, 'Regular'), (700, 'Bold')]:
        instance = instantiateVariableFont(TTFont(source, recalcTimestamp=False), {'wdth': 100, 'wght': weight}, inplace=False)
        canonical_names(instance, 'Noto Sans', style)
        stream = io.BytesIO()
        instance.recalcTimestamp = False
        instance.save(stream)
        data = stream.getvalue()
        target = BASE / f'Static/NotoSans-{style}.ttf'
        if verify:
            assert target.read_bytes() == data, target
        else:
            target.write_bytes(data)
        rows.append(dict(path=str(target.relative_to(ROOT)).replace('\\', '/'), sha256=sha(data), axes_applied={'wdth': 100, 'wght': weight}, **metadata(instance)))
    write(OUT / 'fallback-binaries.json', dict(passed=True, source_revision=REV, source_sha256=sha(source.read_bytes()), codepoints=missing, latin_codepoints=sorted(set(missing)-remaining), symbols_codepoints=sorted(remaining), count=len(missing), faces=rows, reproducible=True))

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=['baseline', 'fetch', 'generate', 'verify'])
    mode = parser.parse_args().mode
    if mode == 'baseline': baseline()
    elif mode == 'fetch': fetch()
    else: generate(mode == 'verify')
