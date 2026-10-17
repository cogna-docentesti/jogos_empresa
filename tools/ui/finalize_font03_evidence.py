"""Close FONT-03 only after live Editor validation and recorded visual review."""
import hashlib
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/ui-update/fonts/FONT-03'
DOC = ROOT / 'docs/ui-update'

def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, data): path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

execution = read(OUT / 'execution.json')
regen = read(OUT / 'regeneration.json')
coverage = read(OUT / 'coverage.json')
weights = read(OUT / 'weight-resolution.json')
visual = read(OUT / 'visual-review.json')
generation = read(OUT / 'generation.json')
fallback = read(OUT / 'fallback-binaries.json')
acquisition = read(OUT / 'fallback-acquisition.json')
assert all(r['passed'] for r in [execution, regen, coverage, weights, visual, fallback])
assert execution['scenePreserved'] and not execution['sceneSaved'] and execution['captures'] >= 8
assert execution['playMode']
capture_rows = read(OUT / 'captures.json')
assert all(c['advancing'] and not c['editMode'] for c in capture_rows)
assert len({sha(ROOT / c['path']) for c in capture_rows}) == 8, 'Stale/repeated Game View frames'
assert execution['consoleErrors'] == 0
assert len(generation['assets']) == 9

baseline_path = OUT / 'baseline.json'
baseline = read(baseline_path)
owned = ['tools/ui/font03_support.py', 'docs/ui-update/font-tasks/README.md', 'docs/ui-update/font-tasks/FONT-03-assets-tmpro.md', 'docs/ui-update/font-sources.json']
changed = [p for p, digest in baseline['files'].items() if not (ROOT/p).is_file() or sha(ROOT/p) != digest]
external = [p for p in changed if p == 'output/ui-update/fonts/orchestration/sequence-state.json']
unexpected = sorted(set(changed) - set(owned) - set(external))
assert not unexpected, unexpected
write(OUT / 'baseline-preservation.json', dict(passed=True, head=baseline['head'], baseline_local=str(baseline_path.relative_to(ROOT)).replace('\\','/'),
      baseline_sha256=sha(baseline_path), files_checked=len(baseline['files']), authorized_edits=owned, changed=changed, unexpected_changes=unexpected,
      external_orchestration_changes=[dict(path=p, before=baseline['files'][p], after=sha(ROOT/p), owner='Coordinator conversation; observed status update during FONT-03; preserved without writing') for p in external],
      initial_status_sha256=sha(OUT/'initial-status.txt'), initial_patch_sha256=sha(OUT/'initial-tracked.patch'), initial_index_sha256=sha(OUT/'initial-staged.patch'),
      note='Raw baseline, patches, dependencies, caches and databases stay local and are not committed.'))

manifest = dict(schema_version=1, task='FONT-03', status='concluído', date='2026-10-17', unity=execution['unity'],
    package='com.unity.ugui 2.0.0, fingerprint e20f1880fa04 (TMP integrado)', generator='Assets/Editor/UI/ConceptFontAssetGenerator.cs',
    validation='Assets/Editor/Tests/Font03AssetValidation.cs', resource='Assets/Resources/UI/ConceptFontSet.asset', resource_load='Resources.Load<ConceptFontSet>("UI/ConceptFontSet")',
    assets=generation['assets'], corpus='output/ui-update/fonts/FONT-01/glyph-corpus.json',
    coverage=dict(actual=118, actual_covered=118, expanded=536, expanded_covered=536, per_primary=342, per_primary_actual=117,
                  latin_fallback=190, arrows_fallback=4, missing=[], details='output/ui-update/fonts/FONT-03/coverage.json'),
    material=dict(shared_subasset=True, face_color='white; label controls tint', face_dilate=0, outline_width=0, underlay=False, weight_normal=0, weight_bold=0),
    weights=dict(roboto={'400':'Roboto-Regular','500':'Roboto-Regular','600':'Roboto-Bold','700':'Roboto-Bold'},
                 condensed={'400':'RobotoCondensed-Regular','500':'RobotoCondensed-Medium','600':'RobotoCondensed-SemiBold','700':'RobotoCondensed-Bold'},
                 native_face='Direct face + FontWeight.Regular/FontStyles.Normal preserves its real source outlines, including Medium/SemiBold/Bold.',
                 bold='fontWeight=700, FontStyles.Bold and <b> resolve to the real Bold via the TMP weight table; boldStyle/boldSpacing/_WeightBold=0.',
                 unsupported='Weights below 400 map to Regular; above 700 to Bold. Roboto 500/600 are compatibility mappings, not additional real source weights. No italic consumer or italic face.'),
    fallback=dict(population='Static', multi_atlas=False, system_fonts=False, global_tmp_settings_used=False, cycles=False,
        latin_codepoints=fallback['latin_codepoints'], symbols_codepoints=fallback['symbols_codepoints'],
        policy='Only the 190 declared Latin gaps and four arrows can come from Noto. No other Noto glyph is baked. Static finite atlases, no dynamic or OS expansion.',
        limits='Other scripts, emoji and characters outside the 536-codepoint corpus are not homologated. Future input policy/coverage changes must extend this manifest explicitly.',
        sources=acquisition),
    player=dict(serialized_resource_references=True, baked_atlas_materials=True, source_font_required_at_runtime=False, system_fonts=False,
                tested='Resource load, transitive asset dependencies, static coverage and advancing Play-mode Game View in Editor. Standalone Player build remains FONT-10.'),
    regeneration=dict(guid_and_subasset_local_ids_stable=True, atlas_pixels_and_metrics_stable=True, reimport_stable=True, report='output/ui-update/fonts/FONT-03/regeneration.json'),
    atlas_budget=dict(texture_format='Alpha8', static_texture_bytes=sum(a['atlas'][0]*a['atlas'][1] for a in generation['assets']),
        note='Eight finite 2048 atlases plus one 512 arrow atlas; 1024 packing attempts and actual packed counts are recorded per face. No growth at runtime.'),
    ui_migration='No production scene/prefab/theme migration in FONT-03. Screen swaps and specimen existed only during QA and were restored without saving.')
write(DOC/'font-assets.json',manifest)

sources = read(DOC/'font-sources.json')
sources['font03_fallbacks'] = dict(task='FONT-03', reason='Explicit finite coverage for the 194 gaps left by the six Roboto production faces.',
    sources=acquisition, static_faces=fallback['faces'], symbols_source='Assets/TextMesh Pro/Fonts/Concept/Sources/NotoSansMath/NotoSansMath-Regular.ttf',
    license='SIL Open Font License 1.1; original OFL.txt and METADATA.pb preserved beside each source.', atlas_manifest='docs/ui-update/font-assets.json')
write(DOC/'font-sources.json', sources)

qa = dict(task='FONT-03', date='2026-10-17', status='concluído', scope='Six real TMP faces, three finite fallback atlases, neutral materials, Player-safe reference set and idempotent native Editor generation.',
    acceptance=dict(complete_tmp_assets=True, real_weights=True, no_synthetic_bold=True, shared_neutral_materials=True, actual_corpus_covered=True,
        expanded_corpus_covered=True, explicit_limited_fallbacks=True, no_old_family_or_os_fallback=True, stable_guids_and_subasset_ids=True,
        identical_regenerated_pixels_and_metrics=True, reimport_valid=True, all_faces_visually_reviewed=True, real_screen_rendered=True, dirty_scene_preserved=True, previous_work_preserved=True),
    validation=dict(unity=execution['unity'], primary_faces=6, fallback_assets=3, expanded_codepoints=536, actual_codepoints=118,
        weight_mesh_cases=len(weights['cases']), captures=execution['captures'], viewport=[1920,1080], mode='Play mode, existing Game View, isolated QA SQLite/identification key',
        console_errors=execution['consoleErrors'], console_warnings=execution['consoleWarnings'],
        scene_before=execution['before'], scene_after=execution['after'], previous_files_checked=len(baseline['files']), unexpected_changes=[]),
    evidence=dict(manifest='docs/ui-update/font-assets.json', sources='docs/ui-update/font-sources.json', gallery='output/ui-update/fonts/FONT-03/index.html',
        execution='output/ui-update/fonts/FONT-03/execution.json', coverage='output/ui-update/fonts/FONT-03/coverage.json', weights='output/ui-update/fonts/FONT-03/weight-resolution.json',
        regeneration='output/ui-update/fonts/FONT-03/regeneration.json', visual='output/ui-update/fonts/FONT-03/visual-review.json', preservation='output/ui-update/fonts/FONT-03/baseline-preservation.json'),
    limitations=[manifest['fallback']['limits'], 'Captures qualify font asset rendering at 1920×1080 in the existing Editor screen; no persistent screen typography/layout migration is claimed.',
        'Standalone Player build and viewport/device qualification remain FONT-10; FONT-04 to FONT-09 own integration and per-screen sizing.',
        'Pipeline/Unity CLI is absent; the native validator was invoked through the open Editor menu via Computer Use. No second Editor was opened.',
        'Existing dirty scene and the unsaved Restaurante TMP were preserved. Original workspace baseline and patches remain local outside this commit.'],
    next_task='FONT-04, started only by the coordinator')
write(DOC/'qa/FONT-03.json', qa)

task = DOC/'font-tasks/FONT-03-assets-tmpro.md'
content = task.read_text(encoding='utf-8-sig').replace('Status: **pendente**','Status: **concluído**').replace('- [ ]','- [x]')
if '## Execução FONT-03' not in content:
    content += '''\n## Execução FONT-03\n\nSeis faces Roboto/Roboto Condensed e três fallbacks estáticos gerados pelo Editor. O atlas 1024 foi tentado em amostragem 80/padding 8; os parâmetros finais e as tentativas medidas estão em [font-assets.json](../font-assets.json). Materiais neutros são subassets compartilhados, sem espessura sintética. Cobertura completa de 118 caracteres atuais e 536 ampliados, com 190 glifos latinos Noto Sans e quatro setas Noto Sans Math explicitamente limitados.\n\nReferências de Player em `Assets/Resources/UI/ConceptFontSet.asset`. Nenhuma tela foi migrada permanentemente. O teste trocou temporariamente as faces nos textos da identificação e adicionou uma folha de amostras ao Canvas existente, restaurando tudo sem salvar a cena. Capturas são Play mode com frames avançando no Game View real, com SQLite e identificação de QA isolados; não equivalem a build standalone.\n\n### Regeneração\n\n1. Instalar/reutilizar `tools/ui/requirements-font02.txt`; definir `PYTHONPATH` para os pacotes locais da FONT-02.\n2. `python tools/ui/font03_support.py fetch` baixa revisões fixadas, verifica cobertura e gera as duas faces estáticas Noto. `generate` usa as fontes já baixadas; `verify` compara os bytes reproduzidos.\n3. No Unity aberto, em Edit mode com GameScene e Game View 1920×1080, atualizar assets e executar `Tools > Jogos de Empresa > FONT-03 > Generate and validate assets`. Preferir o menu se o atalho Ctrl+Alt+F3 conflitar com uma sobreposição de GPU. Não abrir outro Editor nem salvar a cena suja para executar o QA.\n4. O gerador usa `TMP_FontAsset.CreateFontAsset`, `TryAddCharacters`, API TextCore e `SerializedObject` para configurações com setters internos nesta versão. Não cria YAML de atlas. Atualiza fontes, materiais e texturas em lugar, salvando apenas os assets próprios.\n5. Revisar as imagens e registrar `visual-review.json`; só então executar `python tools/ui/finalize_font03_evidence.py`. Não substituir o baseline inicial em uma repetição.\n\nRegeneração completa e reimportação mantiveram GUIDs, IDs locais de subassets, métricas e pixels. Ver [QA FONT-03](../qa/FONT-03.json) e [galeria](../../../output/ui-update/fonts/FONT-03/index.html). Pesos não existentes na Roboto são mapeamentos de compatibilidade documentados; itálico e alfabetos fora do corpus não estão homologados. Integração por papel e builds seguem nas tarefas seguintes.\n'''
task.write_text(content, encoding='utf-8')
index = DOC/'font-tasks/README.md'
content=index.read_text(encoding='utf-8-sig').replace('**FONT-01 e FONT-02 concluídos; FONT-03 a FONT-10 pendentes**','**FONT-01 a FONT-03 concluídos; FONT-04 a FONT-10 pendentes**')
content=content.replace('| FONT-02 | Pendente |','| FONT-02 | Concluído — [QA](../qa/FONT-03.json) |')
index.write_text(content,encoding='utf-8')

panels=[]
for c in read(OUT/'captures.json'):
    name=Path(c['path']).name
    panels.append(f'<figure><figcaption>{html.escape(c["name"])}</figcaption><img src="{name}" loading="lazy"></figure>')
(OUT/'index.html').write_text('<!doctype html><meta charset="utf-8"><title>FONT-03 — TMP assets</title><style>body{margin:24px;background:#24113d;color:#f4f1e9;font:16px system-ui}figure{margin:24px 0}img{width:100%;max-width:1920px}figcaption{margin:12px 0}</style><h1>FONT-03 · TMP assets</h1><p>Six real faces, 536/536 declared codepoints, finite explicit fallbacks. Temporary live-screen swaps; scene restored. Editor Game View 1920×1080.</p>'+''.join(panels),encoding='utf-8')
print('FONT-03 evidence finalized')
