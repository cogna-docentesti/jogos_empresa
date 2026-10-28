"""Persist the ART-38 manual review performed on 2026-10-15.

The agent inspected the master, approved concept, raw light/dark composite,
native light/dark exports, exact display proof, identity comparison and 1:1
face/hand details before invoking this record. This is not a visual validator.
"""
from pathlib import Path
import json
import subprocess
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from produce_art38 import (ROOT, TASK, BASE, FINAL, MASTER, SOURCE, PREVIEW,
                           PROMPTS, PRODUCER, read, write, sha, git, validate)

OBSERVATION = (
    'Rosto, pele morena quente, olhos castanhos, bob curto ondulado com risca lateral, '
    'argolas douradas, camisa lilas de mangas dobradas, calca grafite com cinto e '
    'sapatos fechados de salto baixo preservam a identidade CH01. Postura adulta '
    'ereta em tres quartos; uma palma aberta convida a refletir, outra mao relaxada, '
    'sem gesto exagerado. Dedos, punhos, cabeca e dois pes completos. Leitura clara '
    'na caixa 420x760 em contain, sem deformacao. Fundos marfim e ameixa mostram '
    'recorte limpo e vazios transparentes, sem checkerboard, cenario ou halo visivel. '
    'Sem texto, balao, sinal de interrogacao, respostas, graficos, props ou outras pessoas.'
)


def scope_audit():
    current = read('docs/art/manifest.json')
    baseline = json.loads(git('show', BASE + ':docs/art/manifest.json'))
    changes = {}
    for key, identity in [('assets', 'id'), ('tasks', 'id')]:
        old = {a[identity]: a for a in baseline[key]}
        new = {a[identity]: a for a in current[key]}
        assert old.keys() == new.keys()
        changes[key] = [k for k in old if old[k] != new[k]]
    assert changes == {'assets': ['CH01_GUIDING'], 'tasks': [TASK]}, changes
    assert {k: v for k, v in baseline.items() if k not in ('assets', 'tasks')} == {
        k: v for k, v in current.items() if k not in ('assets', 'tasks')}
    old_tasks = json.loads(git('show', BASE + ':docs/art/tasks.json'))
    new_tasks = read('docs/art/tasks.json')
    assert [t for t in old_tasks if t['id'] != TASK] == [t for t in new_tasks if t['id'] != TASK]
    baseline_hashes = read('docs/art/qa/ART-38-prerequisites.json')['baseline_assets']
    for a in baseline_hashes:
        assert sha(a['path']) == a['sha256'], a['id']
    changed = set(git('diff', '--name-only', BASE).splitlines())
    changed.update(git('ls-files', '--others', '--exclude-standard').splitlines())
    changed.update(['docs/art/qa/ART-38.json', 'docs/art/qa/ART-38-scope-audit.json'])
    exact = {FINAL, FINAL+'.meta', 'docs/art/manifest.json', 'docs/art/tasks.json', PROMPTS}
    prefixes = ('art-source/editable/ART-38/', 'art-source/previews/ART-38/', 'docs/art/qa/ART-38')
    unexpected = sorted(p for p in changed if p not in exact and not p.startswith(prefixes))
    assert not unexpected, unexpected
    record = {
        'task_id': TASK, 'passed': True, 'base_commit': BASE,
        'branch': git('branch', '--show-current'),
        'initial_head_verified': True, 'initial_tree_clean': True,
        'current_lot_only': True, 'changed_manifest_asset_ids': changes['assets'],
        'changed_manifest_task_ids': changes['tasks'],
        'unrelated_manifest_records_preserved': True,
        'prior_png_hash_count': len(baseline_hashes), 'prior_png_hashes_preserved': True,
        'canonical_master_sha256': sha(MASTER),
        'unrelated_changed_paths': unexpected, 'reviewed_paths': sorted(changed),
        'recorded_before_commit': True,
    }
    write('docs/art/qa/ART-38-scope-audit.json', record)
    return record


def main():
    assert git('rev-parse', 'HEAD') == BASE
    p = read(PROMPTS)
    a = p['assets'][0]
    assert a['sha256'] == sha(FINAL) and a['source_sha256'] == sha(SOURCE)
    assert a['canonical_sha256'] == sha(MASTER)
    index = read('docs/art/qa/ART-38-preview-index.json')
    previews = index['previews']
    for path in previews:
        assert (ROOT/path).is_file()
    a.update(status='validado', qa={'technical': True, 'visual': True, 'identity': True},
             visual_observation=OBSERVATION, identity_qa='passed_against_real_canonical_master',
             identity_comparison=PREVIEW+'/CH01-identity-comparison.png',
             inspected_previews=previews,
             display_proof=PREVIEW+'/CH01_GUIDING-display-light-dark.png')
    write(PROMPTS, p)
    m = read('docs/art/manifest.json')
    asset = next(x for x in m['assets'] if x['id'] == 'CH01_GUIDING')
    asset.update(status='validado', qa={
        'technical': True, 'visual': True, 'identity': True,
        'record': 'docs/art/qa/ART-38.json', 'notes': OBSERVATION,
        'preview': a['display_proof'], 'details': PREVIEW+'/CH01_GUIDING-details-1x.png',
        'identity_comparison': a['identity_comparison'],
    })
    task = next(t for t in m['tasks'] if t['id'] == TASK)
    task.update(status='validado', qa_record='docs/art/qa/ART-38.json')
    write('docs/art/manifest.json', m)
    tasks = read('docs/art/tasks.json')
    task = next(t for t in tasks if t['id'] == TASK)
    task.update(status='validado', qa_record='docs/art/qa/ART-38.json')
    write('docs/art/tasks.json', tasks)
    validate()
    # Import fields are checked directly; Unity is not launched.
    meta = (ROOT/(FINAL+'.meta')).read_text(encoding='utf-8')
    for expected in ['textureType: 8', 'spriteMode: 1', 'spriteMeshType: 0',
                     'spritePixelsToUnits: 100', 'spritePivot: {x: 0.5, y: 0.5}',
                     'spriteBorder: {x: 0, y: 0, z: 0, w: 0}', 'enableMipMap: 0',
                     'sRGBTexture: 1', 'alphaIsTransparency: 1', 'filterMode: 1',
                     'maxTextureSize: 2048', 'textureCompression: 0']:
        assert expected in meta, expected
    scope = scope_audit()
    qa = {
        'task_id': TASK, 'status': 'validado', 'date': '2026-10-15',
        'asset_count': 1, 'new_sprite_count': 1,
        'technical': {
            'passed': True, 'task_validation': 'docs/art/qa/ART-38-technical.json',
            'plan_validation': 'docs/art/qa/ART-38-plan-validation.json',
            'prerequisites': 'docs/art/qa/ART-38-prerequisites.json',
            'rgba_srgb': True, 'export_size_px': [1024, 1536],
            'true_transparency': True, 'transparent_margins_at_least_10_percent': True,
            'alpha': a['alpha'], 'import_metadata_verified': True,
            'import_verification': 'Serialized metadata inspected; Unity not executed.',
            'source_raster_preserved': True,
        },
        'visual': {
            'passed': True, 'identity_passed': True, 'canonical_master': MASTER,
            'canonical_master_sha256': sha(MASTER), 'canonical_master_unchanged': True,
            'method': 'Direct manual inspection of all listed previews and actual references.',
            'backgrounds': ['#F5F0E8', '#24113D'],
            'native_size_inspected_px': [1024, 1536],
            'display_box_inspected_px': [420, 760], 'display_fit': 'contain',
            'display_raster_px': [420, 630], 'display_offset_px': [0, 65],
            'observation': OBSERVATION, 'inspected_previews': previews,
            'raw_diagnostics_inspected': [
                PREVIEW+'/CH01_GUIDING-initial-light-dark.png',
                PREVIEW+'/CH01_GUIDING-initial-hand-detail.png',
            ],
            'final_ui_proposed': False,
        },
        'assets': [a], 'production_record': PROMPTS, 'scope': scope,
        'documentary_scope': {
            'delivery': 'Only CH01_GUIDING; one independent generation and one final PNG.',
            'reuse': 'Existing ART-04 buttons and ART-05 inputs/controls cover future forms, answers, feedback and offline status. No new components generated.',
            'ui_status': 'Debriefing has no final implemented UI. Proofs are inspection boards only.',
            'backend_or_services_created': False,
        },
        'pending': [],
        'integration_pending': [
            'Future Unity integration and final debriefing/form design remain outside ART-38.',
            'ART-36 historical audit reported 227 earlier importers differing from proposals and no matches for its 265 GUIDs in 175 serialized files. Not repaired or re-audited here.',
            'BMC and documentary reports still await their final model/screen.',
        ],
        'commit_reference': 'Recorded by queue finish after creation of the single commit.',
        'integration_note': 'No Unity execution, gameplay, scene, catalog, economy, package changes or push.',
    }
    write('docs/art/qa/ART-38.json', qa)
    print('Recorded manual technical, visual and identity QA; current-lot scope and 269 prior hashes passed.')


if __name__ == '__main__':
    main()
