"""Finalize FONT-02 only after binary, specimen, and live Editor checks pass."""
import hashlib
import html
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/ui-update/fonts/FONT-02'
DOC = ROOT / 'docs/ui-update'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


manifest = read(DOC / 'font-sources.json')
binary = read(OUT / 'binary-verification.json')
editor = read(OUT / 'unity-import.json')
specimen = read(OUT / 'specimen-metrics.json')
assert binary['passed'] and editor['passed'] and specimen['passed']
assert editor['scenePreserved'] and not editor['sceneSaved'] and not editor['tmpAssetsCreated']
assert editor['consoleErrors'] == editor['consoleWarnings'] == 0
assert len(editor['faces']) == 6 and all(f['guidStableOnReimport'] for f in editor['faces'])

baseline_path = OUT / 'baseline.json'
baseline = read(baseline_path)
owned_existing = ['docs/ui-update/font-tasks/README.md', 'docs/ui-update/font-tasks/FONT-02-download-e-licencas.md']
changed = [p for p, digest in baseline['files'].items() if not (ROOT / p).is_file() or sha(ROOT / p) != digest]
unexpected = sorted(set(changed) - set(owned_existing))
assert not unexpected, unexpected
write(OUT / 'baseline-preservation.json', dict(passed=True, head=baseline['head'],
      baseline_local='output/ui-update/fonts/FONT-02/baseline.json', baseline_sha256=sha(baseline_path),
      files_checked=len(baseline['files']), authorized_document_edits=owned_existing,
      preexisting_files_changed=changed, unexpected_changes=unexpected,
      initial_status_sha256=sha(OUT / 'initial-status.txt'),
      initial_tracked_patch_sha256=sha(OUT / 'initial-tracked.patch'),
      initial_staged_patch_sha256=sha(OUT / 'initial-staged.patch'),
      note='Raw baseline, patches, local dependency packages, caches and databases are excluded from the commit.'))

files = sorted([ROOT / 'Assets/TextMesh Pro/Fonts/Concept.meta',
               *list((ROOT / 'Assets/TextMesh Pro/Fonts/Concept').rglob('*')),
               ROOT / 'Assets/Editor/Tests/Font02SourceValidation.cs',
               ROOT / 'Assets/Editor/Tests/Font02SourceValidation.cs.meta'])
files = [p for p in files if p.is_file()]
metas = []
for path in files:
    if path.suffix == '.meta':
        guid = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
        assert guid, path
        metas.append(dict(path=path.relative_to(ROOT).as_posix(), guid=guid[1], sha256=sha(path)))
    else:
        assert Path(str(path) + '.meta').exists(), path
assert len({m['guid'] for m in metas}) == len(metas)
write(OUT / 'unity-metas.json', dict(passed=True, count=len(metas), unique_guids=True, metas=metas))

limitations = [
    'Fontes TTF prontas; atlas, materiais, fallbacks e aplicação na UI pertencem a FONT-03 em diante. Nenhum TMP criado nesta tarefa.',
    'Cada face cobre 117/118 caracteres atuais; falta U+2190 (seta esquerda). Do corpus ampliado de 536, faltam 194, incluindo Latin Extended B e U+2190 a U+2193. FONT-03 precisa fornecer cobertura/fallback explícito.',
    'Nomes livres além da cobertura Latin informada não estão homologados; sem dependência em fontes instaladas no Windows.',
    'Pipeline/Unity CLI indisponíveis na sessão preexistente; verificador executado pelo menu do Editor via Computer Use. Não foi aberto segundo Editor.',
    'Amostra visual de TTF em FreeType/Pillow não equivale a homologação dos futuros atlas TMP ou das telas do Player.',
    'Baseline original completo e patches brutos permanecem locais, fora do commit; os hashes constam na evidência de preservação.'
]
qa = dict(task='FONT-02', date='2026-10-17', status='concluído',
    scope='Download oficial, instâncias estáticas, licenças e importação de fontes; sem geração TMP.',
    acceptance=dict(six_required_faces=True, official_pinned_origins_and_hashes=True,
                    actual_license_and_credits_preserved=True, full_axes_pinned=True,
                    real_unique_outlines=True, byte_reproduction=True, actual_weight_and_style_verified=True,
                    unity_import_without_errors=True, stable_metas=True, no_system_or_output_dependency=True,
                    dirty_scene_preserved=True, preexisting_work_preserved=True),
    validation=dict(unity=editor['unity'], fonttools=manifest['generation']['fonttools'],
                    static_faces=6, unique_outline_hashes=6, all_glyph_outlines_and_metrics_compared=True,
                    current_codepoints=118, current_covered=117, planned_codepoints=536, planned_covered=342,
                    console_errors=0, console_warnings=0, scene_texts_before=editor['before']['texts'],
                    scene_texts_after=editor['after']['texts'], unsaved_restaurant_preserved=True,
                    preexisting_files_checked=len(baseline['files']), unexpected_preexisting_changes=[],
                    metas=len(metas), source_versions={'Roboto': '3.015; 2026', 'Roboto Condensed': '3.008; 2023'}),
    evidence=dict(sources='docs/ui-update/font-sources.json', editor='output/ui-update/fonts/FONT-02/unity-import.json',
                  binaries='output/ui-update/fonts/FONT-02/binary-verification.json',
                  metas='output/ui-update/fonts/FONT-02/unity-metas.json',
                  preservation='output/ui-update/fonts/FONT-02/baseline-preservation.json',
                  specimen='output/ui-update/fonts/FONT-02/static-specimen.png',
                  specimen_metrics='output/ui-update/fonts/FONT-02/specimen-metrics.json',
                  gallery='output/ui-update/fonts/FONT-02/index.html'),
    limitations=limitations, next_task='FONT-03; iniciada somente pela conversa coordenadora')
write(DOC / 'qa/FONT-02.json', qa)
rows = ''.join('<tr><td>' + html.escape(f['names']['1'] + ' ' + f['names']['2']) + '</td><td>' + str(f['weight']) + '</td><td>' + html.escape(f['names']['5']) + '</td><td><code>' + f['sha256'] + '</code></td></tr>' for f in manifest['static_faces'])
(OUT / 'index.html').write_text('''<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>FONT-02 — Fontes de produção</title>
<style>body{font:16px/1.5 sans-serif;margin:32px;background:#f5f0e8;color:#30243c}img{width:100%;max-width:1200px}table{border-collapse:collapse}td,th{padding:10px;border:1px solid #b9aeab}code{font-size:11px}</style>
<h1>FONT-02 — Fontes de produção</h1><p>Seis TTF estáticos verificados. Unity 6000.3.20f1: PASS, zero erros/avisos, GUIDs preservados na reimportação. Cena suja e 1052 textos preservados.</p>
<p>Fontes oficiais Google Fonts fixadas por commit; licenças OFL 1.1 e créditos mantidos. Nenhum asset TMP ou cena de produção alterado.</p>
<img src="static-specimen.png" alt="Seis faces em 46 px, escala 1"><table><tr><th>Face</th><th>Peso</th><th>Versão</th><th>SHA-256</th></tr>''' + rows + '''</table><p>Cobertura: 117/118 caracteres atuais; U+2190 ausente. Corpus ampliado: 342/536. Lacunas completas no manifesto; FONT-03 define fallback explícito.</p>
<p><a href="../../../../docs/ui-update/qa/FONT-02.json">QA</a> · <a href="../../../../docs/ui-update/font-sources.json">Manifesto</a> · <a href="unity-import.json">Importação Unity</a> · <a href="binary-verification.json">Verificação binária</a> · <a href="baseline-preservation.json">Preservação</a> · <a href="unity-metas.json">Metas</a></p></html>''', encoding='utf-8')
print('FONT-02 accepted:', len(editor['faces']), 'faces;', len(baseline['files']), 'preexisting files preserved;', len(metas), 'stable metas.')
