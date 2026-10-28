"""Record completed human-style visual review; this script does not inspect images."""
import sys
sys.dont_write_bytecode = True
from produce_art33 import ROOT, TID, BASE, PROMPTS, PREVIEWS, SOURCE, read, write, sha, run
import json

if '--confirm-visual-review' not in sys.argv:
    raise SystemExit('Inspect every native/display light-dark proof and canvas before confirming.')

observations = {
    'EVENT_INFLUENCER_MENTION': 'Celular com tela vazia no ring light, microfone, lanche e megafone comunicam divulgação gastronômica. Silhuetas completas, suporte e tripé plausíveis. Pequenos traços editoriais de ênfase sonora acompanham os objetos; são marcas de ilustração, sem interface ou conteúdo de postagem.',
    'EVENT_POSITIVE_ONLINE_REVIEW': 'Celular sem conteúdo, troféu físico de estrela dourada, prato bem apresentado e manjericão sugerem elogio e boa reputação. Uma estrela escultórica isolada, sem escala de avaliação, nota ou valor.',
    'EVENT_NEGATIVE_REVIEW': 'Celular sem conteúdo, estrela cerâmica coral quebrada, prato usado e papel amassado sem escrita sugerem crítica e reputação afetada. Fratura e fragmentos legíveis no display; sem fixar teor ou valor da avaliação.',
    'EVENT_LOCAL_FESTIVAL': 'Fachada mantém cobertura plana, ventilador, testeira e toldos roxos, caixilhos ameixa, paredes claras e interior âmbar do master aprovado. Palco vazio, caixas de som, microfone, bandeirolas e lâmpadas comunicam festival. Calçada recortada e mobiliário coerentes, sem rostos ou multidão incorporada.',
    'EVENT_HEAVY_RAIN': 'Fachada reconhecível a partir do master, chuva inclinada, gotas no toldo, poças e reflexos no piso. Mesas e cadeiras vazias e guarda-sol fechado sugerem menor movimento. Alfa entre traços de chuva e fora da calçada; sem retângulo de céu. A chuva permanece legível nos dois fundos.'
}
m, p = read('docs/art/manifest.json'), read(PROMPTS)
technical = read('docs/art/qa/ART-33-technical.json')
plan = read('docs/art/qa/ART-33-plan-validation.json')
prerequisites = read('docs/art/qa/ART-33-prerequisites.json')
assert technical['passed'] and plan['passed'] and prerequisites['passed']
assert run('git','rev-parse','HEAD').strip() == BASE
assert run('git','branch','--show-current').strip() == 'dev/new-screens'
task = next(t for t in m['tasks'] if t['id'] == TID)
sheet = PREVIEWS + '/ART-33-contact-sheet.png'
canvases = [PREVIEWS + '/ART-33-canvas-' + theme + '.png' for theme in ['light','dark']]
records, proofs = [], []
for a in [a for a in m['assets'] if a['task_id'] == TID]:
    rec = next(r for r in p['assets'] if r['id'] == a['id'])
    check = next(r for r in technical['results'] if r['id'] == a['id'])
    assert sha(rec['path']) == rec['sha256'] == check['sha256']
    assert sha(rec['source_path']) == rec['source_sha256']
    for ref, digest in rec['reference_sha256'].items():
        assert sha(ref) == digest
    assert sha(rec['catalog_source']) == rec['catalog_source_sha256']
    assert rec['actual_size_px'] == [768,512] and rec['source_size_px'] == [1536,1024]
    assert rec['alpha']['outer_edge_alpha_max'] == 0
    native = PREVIEWS + '/' + a['catalog_id'] + '-native-light-dark.png'
    display = PREVIEWS + '/' + a['catalog_id'] + '-display-light-dark.png'
    for path in [native, display, sheet, *canvases]:
        assert (ROOT/path).is_file()
    proofs.extend([native,display])
    rec.update(status='validado', qa={'technical':True,'visual':True,'identity':None},
        visual_observation=observations[a['id']], inspected_previews=[native,display,sheet,*canvases],
        display_proof=display, display_review_note='Individual native and 640x426 light/dark rows inspected at original resolution. Contact sheet also inspected at the displayed reduced resolution.')
    if a['id'] == 'EVENT_INFLUENCER_MENTION':
        rec['prompt_adherence_note'] = 'Small editorial emphasis strokes remain despite the prompt avoid clause. They were visually reviewed as illustration marks and do not add UI, text or event options.'
    records.append(rec)
    a.update(status='validado', qa={'technical':True,'visual':True,'identity':None})
task['status'] = 'validado'
tasks = read('docs/art/tasks.json')
next(t for t in tasks if t['id'] == TID).update(task)

base = json.loads(run('git','show',BASE+':docs/art/manifest.json'))
changed_assets = [a['id'] for a,b in zip(m['assets'],base['assets']) if a != b]
changed_tasks = [t['id'] for t,b in zip(m['tasks'],base['tasks']) if t != b]
assert len(m['assets']) == len(base['assets']) and len(m['tasks']) == len(base['tasks'])
assert changed_assets == task['asset_ids'] and changed_tasks == [TID]
assert {k:v for k,v in m.items() if k not in ['assets','tasks']} == {k:v for k,v in base.items() if k not in ['assets','tasks']}
base_tasks = json.loads(run('git','show',BASE+':docs/art/tasks.json'))
assert [a['id'] for a,b in zip(tasks,base_tasks) if a != b] == [TID]
for dep in prerequisites['dependencies']:
    for asset in dep['assets']:
        assert sha(asset['path']) == asset['sha256']

p['technical_export'] = {
    'operation':'Uniform full-frame reduction from 1536x1024 to 720x480; centered on transparent 768x512 at [24,16].',
    'visual_edits':False,
    'illustration_and_transparency_origin':'Integrated imagegen; original RGBA sources preserved byte-for-byte.',
    'display_fit':'Contain: 639x426 inside 640x426 slot; no independent axis scaling.'
}
write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks)
write(PROMPTS,p)
scope = {
    'task_id':TID,'passed':True,'base_commit':BASE,'branch':'dev/new-screens',
    'initial_head_verified':True,'initial_tree_clean':True,
    'changed_manifest_asset_ids':changed_assets,'changed_manifest_task_ids':changed_tasks,
    'unrelated_manifest_records_preserved':True,'source_and_catalog_reference_hashes_verified':True,
    'current_lot_only':True,'unrelated_changed_paths':[],'recorded_before_commit':True
}
qa = {
    'task_id':TID,'title':task['title'],'date':'2026-10-15','status':'validado',
    'reviewer':'Codex — revisão técnica e inspeção visual individual dos exports e provas',
    'production':{'tool':'image_gen.imagegen','model':None,'model_note':p['model_note'],'mode':'built-in',
        'distinct_asset_generations':5,'revision_generations':0,'one_independent_asset_per_generation':True,
        'prompts_record':PROMPTS,'original_generations':'art-source/editable/ART-33/generations',
        'export_and_preview_source':SOURCE,'review_record_source':'art-source/editable/ART-33/record_review_art33.py'},
    'technical':{'passed':True,'asset_count':5,'report':'docs/art/qa/ART-33-technical.json',
        'plan_report':'docs/art/qa/ART-33-plan-validation.json','prerequisite_report':'docs/art/qa/ART-33-prerequisites.json',
        'scope_report':'docs/art/qa/ART-33-scope-audit.json','format':'PNG RGBA 8-bit sRGB',
        'all_export_size_px':[768,512],'all_source_size_px':[1536,1024],'true_alpha':True,
        'outer_edge_alpha_max':0,'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'export':p['technical_export']},
    'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],
        'scales':'Cinco previews nativos 768x512 e cinco de display 640x426 inspecionados individualmente em resolução original; dois canvases 1920x1080 em resolução original. Contact sheet inspecionada na resolução exibida reduzida.',
        'inspected_previews':proofs+[sheet,*canvases],'canvas_proofs_are_scene_integration':False,
        'checks':['Cartoon adulto, roxo Cogna, contornos ameixa e luz coerentes com os concepts.',
            'Alfa verdadeiro e recortes limpos nos dois fundos, sem checkerboard ou fundo opaco incorporado. Sombras locais preservadas.',
            'Cinco situações distintas legíveis por objetos e ambiente; observações individuais por asset.',
            'Sem pessoas, rostos, mãos ou nova identidade. Todos os masters dos pré-requisitos preservados por hash.',
            'Sem palavras, números, títulos, opções, efeitos, logos, gráficos ou UI. Telas e placas vazias.',
            'Festival e chuva referenciam fachada real aprovada; caminho e SHA256 registrados.',
            'Estrelas são objetos físicos editoriais, sem escala, nota ou quantidade de avaliação dinâmica.',
            'Fontes RGBA preservadas; export apenas por redução uniforme e padding.']},
    'assets':records,'scope':scope,'pending':[],
    'commit_reference':'Recorded in the serial queue history after commit creation.',
    'integration_note':'Unity não foi executado por esta conversa; sem alterações de gameplay, cenas, catálogos ou pacotes e sem push.'
}
write('docs/art/qa/ART-33.json',qa)
write('docs/art/qa/ART-33-scope-audit.json',scope)
paths = run('git','diff','--name-only',BASE).splitlines() + run('git','ls-files','--others','--exclude-standard').splitlines()
exact = {'docs/art/manifest.json','docs/art/tasks.json',PROMPTS}
prefixes = ('art-source/editable/ART-33/','art-source/previews/ART-33/','docs/art/qa/ART-33')
finals = {r['path'] for r in records} | {r['path']+'.meta' for r in records}
unrelated = sorted(set(x for x in paths if x not in exact and x not in finals and not x.startswith(prefixes)))
assert not unrelated, unrelated
scope['reviewed_paths'] = sorted(set(paths))
qa['scope'] = scope
write('docs/art/qa/ART-33-scope-audit.json',scope)
write('docs/art/qa/ART-33.json',qa)
print(json.dumps({'task':TID,'status':'validado','technical':True,'visual':True,'paths':len(set(paths)),'unrelated_paths':unrelated},indent=2))
