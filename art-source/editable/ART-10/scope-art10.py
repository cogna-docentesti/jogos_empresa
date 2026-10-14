"""Capture and audit the ART-10 production boundary; never change game files."""
from pathlib import Path
import argparse, hashlib, json, re

ROOT = Path(__file__).resolve().parents[3]
TASK = 'ART-10'
BASELINE = ROOT / 'docs/art/qa/ART-10-scope-baseline.json'

def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8-sig'))

def write(path, value):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def digest(value):
    return hashlib.sha256(value).hexdigest()

def record_hash(value):
    return digest(json.dumps(value, ensure_ascii=False, sort_keys=True).encode('utf-8'))

def scene_rects(rel):
    source = (ROOT / rel).read_text(encoding='utf-8-sig')
    blocks = {m.group(2): (m.group(1), m.group(3)) for m in re.finditer(
        r'^--- !u!(\d+) &(\d+)\r?\n(.*?)(?=^--- !u!|\Z)', source, re.M | re.S)}
    names = {}
    for ident, (kind, body) in blocks.items():
        match = re.search(r'^  m_Name: (AreaBank|AreaUniversity|AreaStore|AreaCondominium|AreaMarketing|LocationMapImage|MapArea|MapLayer|MapBackground|Node_Bank|Node_Store)$', body, re.M)
        if kind == '1' and match:
            names[ident] = match.group(1)
    results = []
    for ident, (kind, body) in blocks.items():
        obj = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
        if kind != '224' or not obj or obj.group(1) not in names:
            continue
        item = {'name': names[obj.group(1)], 'rect_transform_file_id': ident}
        for name in ['m_Father', 'm_AnchorMin', 'm_AnchorMax', 'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot']:
            match = re.search(r'^  ' + name + r': (.+)$', body, re.M)
            if match:
                item[name] = match.group(1)
        results.append(item)
    return {'scene': rel, 'sha256': digest((ROOT / rel).read_bytes()), 'rect_transforms': results}

def prepare():
    if BASELINE.exists():
        raise RuntimeError('ART-10 baseline already exists; do not replace the original snapshot.')
    manifest = read('docs/art/manifest.json')
    assert len([a for a in manifest['assets'] if a['task_id'] == TASK]) == 1
    assert next(t for t in manifest['tasks'] if t['id'] == TASK)['asset_ids'] == ['MAP_BASE']
    assert not (ROOT / 'Assets/Art/new/location/map/map_base.png').exists()
    paths = {'docs/art/characters.json', 'docs/art/tokens.json', 'docs/art/catalogos.json',
             'docs/art/sources.json', 'docs/art/schemas/map-layout.schema.json'}
    for folder in ['Assets/Scripts', 'Assets/Scenes', 'Assets/Prefabs', 'Assets/Resources', 'art-source/references']:
        paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT / folder).rglob('*') if p.is_file())
    for asset in manifest['assets']:
        if asset['status'] == 'validado':
            paths.add(manifest['asset_root'] + '/' + asset['path'])
            if asset.get('source_editable'):
                paths.add(asset['source_editable'])
    for folder in ['docs/art/prompts', 'docs/art/qa']:
        paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT / folder).glob('ART-*.json') if not p.name.startswith(TASK))
    write('docs/art/qa/ART-10-scope-baseline.json', {
        'task_id': TASK, 'date': '2026-10-14',
        'other_asset_records_sha256': record_hash([a for a in manifest['assets'] if a['task_id'] != TASK]),
        'other_task_records_sha256': record_hash([t for t in manifest['tasks'] if t['id'] != TASK]),
        'other_tasks_index_sha256': record_hash([t for t in read('docs/art/tasks.json') if t['id'] != TASK]),
        'protected_files': {p: digest((ROOT / p).read_bytes()) for p in sorted(paths)}
    })
    write('docs/art/qa/ART-10-scene-snapshot.json', {
        'task_id': TASK, 'date': '2026-10-14', 'scene_modified': False,
        'coordinate_note': 'Valores crus de RectTransform; não representam pixels do novo master nem uma transformação já aplicada.',
        'scenes': [scene_rects(p) for p in ['Assets/Scenes/GameScene.unity', 'Assets/Scenes/LocationSelectionScene.unity']]
    })
    print(json.dumps({'task': TASK, 'baseline_created': True, 'protected_file_count': len(paths)}))

def audit():
    baseline, manifest = read('docs/art/qa/ART-10-scope-baseline.json'), read('docs/art/manifest.json')
    files = {p: (ROOT / p).is_file() and digest((ROOT / p).read_bytes()) == sha for p, sha in baseline['protected_files'].items()}
    checks = {
        'other_asset_records_unchanged': record_hash([a for a in manifest['assets'] if a['task_id'] != TASK]) == baseline['other_asset_records_sha256'],
        'other_task_records_unchanged': record_hash([t for t in manifest['tasks'] if t['id'] != TASK]) == baseline['other_task_records_sha256'],
        'other_tasks_index_unchanged': record_hash([t for t in read('docs/art/tasks.json') if t['id'] != TASK]) == baseline['other_tasks_index_sha256'],
        'protected_files_unchanged': all(files.values()), 'protected_file_count': len(files),
        'changed_protected_files': [p for p, unchanged in files.items() if not unchanged]
    }
    passed = all(checks[k] for k in ['other_asset_records_unchanged', 'other_task_records_unchanged', 'other_tasks_index_unchanged', 'protected_files_unchanged'])
    write('docs/art/qa/ART-10-scope-audit.json', {'task_id': TASK, 'date': '2026-10-14', 'passed': passed, **checks})
    print(json.dumps({'task_id': TASK, 'passed': passed, **checks}))
    if not passed:
        raise SystemExit(1)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['prepare', 'audit'])
    args = parser.parse_args()
    prepare() if args.mode == 'prepare' else audit()
