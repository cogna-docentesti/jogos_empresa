"""Record manual visual review after the isolated metadata recovery.

This records review evidence; it does not perform visual inspection itself.
"""
import sys
sys.dont_write_bytecode = True
from produce_art32 import ROOT, TID, BASE, PROMPTS, PREVIEWS, SOURCE, read, write, sha, run
import json

if '--confirm-visual-review' not in sys.argv:
    raise SystemExit('Inspect the native/display light-dark proofs before confirming review.')

observations = {
    'EVENT_FREEZER_BREAKDOWN': 'Freezer horizontal preserva corpo claro, puxador, pés e ventilação direita do master. Tampa aberta, gelo derretendo, poça, ingredientes em cubas e caixa de ferramentas comunicam falha e risco ao estoque.',
    'EVENT_HEALTH_INSPECTION': 'Lupa amplia resíduos e desgaste na tábua da bancada; prancheta completamente vazia, termômetro sem números e materiais de limpeza em bandeja lateral comunicam inspeção e adequação. Sem fiscal ou texto.',
    'EVENT_INSUFFICIENT_TEAM': 'Avental grafite pendurado, banco vazio e posto sem operador contrastam com louça usada acumulada. Ausência e atraso de atendimento legíveis sem introduzir pessoas.',
    'EVENT_TEAM_PRODUCTIVITY_BOOST': 'Preparo organizado à esquerda, prato sob lâmpada ao centro e carrinho com três refeições prontas à direita sugerem fluxo eficiente. Ampulheta física, campainha e utensílios organizados reforçam ritmo de trabalho.',
    'EVENT_PAYMENT_SYSTEM_FAILURE': 'PDV preserva gaveta, monitores, impressora e leitor do master. Telas vazias, cabo de rede desconectado, LED âmbar e cartão liso junto à sacola aguardando comunicam interrupção no pagamento.'
}
m = read('docs/art/manifest.json')
p = read(PROMPTS)
technical = read('docs/art/qa/ART-32-technical.json')
plan = read('docs/art/qa/ART-32-plan-validation.json')
assert technical['passed'] and plan['passed']
task = next(t for t in m['tasks'] if t['id'] == TID)
records, proofs = [], []
sheet = PREVIEWS + '/ART-32-contact-sheet.png'
canvas_proofs = [PREVIEWS + '/ART-32-canvas-' + theme + '.png' for theme in ['light','dark']]
for a in [a for a in m['assets'] if a['task_id'] == TID]:
    rec = next(r for r in p['assets'] if r['id'] == a['id'])
    check = next(r for r in technical['results'] if r['id'] == a['id'])
    assert sha(rec['path']) == rec['sha256'] == check['sha256']
    assert sha(rec['source_path']) == rec['source_sha256']
    for ref,digest in rec['reference_sha256'].items():
        assert sha(ref) == digest
    assert sha(rec['catalog_source']) == rec['catalog_source_sha256']
    assert rec['actual_size_px'] == [768,512] and rec['source_size_px'] == [1536,1024]
    assert rec['alpha']['outer_edge_alpha_max'] == 0
    native = PREVIEWS + '/' + a['catalog_id'] + '-native-light-dark.png'
    display = PREVIEWS + '/' + a['catalog_id'] + '-display-light-dark.png'
    for path in [native, display, sheet, *canvas_proofs]:
        assert (ROOT/path).is_file()
    proofs.extend([native,display])
    rec.update(status='validado', qa={'technical':True,'visual':True,'identity':None},
        visual_observation=observations[a['id']], inspected_previews=[native,display,sheet,*canvas_proofs],
        display_proof=display, display_review_note='Individual 640x426 light/dark row inspected at original resolution; the contact sheet was also inspected, displayed at reduced resolution.')
    records.append(rec)
    a['status'] = 'validado'
    a['qa'] = {'technical':True,'visual':True,'identity':None}

base = json.loads(run('git','show',BASE+':docs/art/manifest.json'))
changed_assets = [a['id'] for a,b in zip(m['assets'],base['assets']) if a != b]
changed_tasks = [t['id'] for t,b in zip(m['tasks'],base['tasks']) if t != b]
assert changed_assets == task['asset_ids'] and changed_tasks == [TID]
assert {k:v for k,v in m.items() if k not in ['assets','tasks']} == {k:v for k,v in base.items() if k not in ['assets','tasks']}
paths = run('git','diff','--name-only',BASE).splitlines() + run('git','ls-files','--others','--exclude-standard').splitlines()
allowed_exact = {'docs/art/manifest.json','docs/art/tasks.json',PROMPTS}
allowed_prefixes = ('art-source/editable/ART-32/','art-source/previews/ART-32/','docs/art/qa/ART-32')
finals = {rec['path'] for rec in records} | {rec['path']+'.meta' for rec in records}
unrelated = sorted(set(path for path in paths if path not in allowed_exact and path not in finals and not path.startswith(allowed_prefixes)))
assert not unrelated, unrelated
recovery = read('docs/art/qa/ART-32-metadata-recovery.json')
assert recovery['passed'] and recovery['restored_file_count'] == 10
for rec in recovery['files']:
    assert sha(rec['path']) == rec['committed_sha256'], rec['path']
scope = {'task_id':TID,'passed':True,'base_commit':BASE,'branch':run('git','branch','--show-current').strip(),
    'initial_head_verified':True,'initial_tree_clean':True,'changed_manifest_asset_ids':changed_assets,
    'changed_manifest_task_ids':changed_tasks,'unrelated_manifest_records_preserved':True,
    'source_and_catalog_reference_hashes_verified':True,'current_lot_only':True,
    'unrelated_changed_paths':unrelated,'metadata_recovery_record':'docs/art/qa/ART-32-metadata-recovery.json',
    'reviewed_paths':sorted(set(paths)),'recorded_before_commit':True}
write('docs/art/qa/ART-32-scope-audit.json',scope)
p['technical_export'] = {'operation':'Uniform full-frame reduction from 1536x1024 to 720x480; centered on transparent 768x512 at [24,16].',
    'visual_edits':False,'illustration_and_transparency_origin':'Integrated imagegen; original RGBA sources preserved byte-for-byte.',
    'display_fit':'Contain: 639x426 inside 640x426 slot; no independent axis scaling.'}
task['status'] = 'validado'
tasks = read('docs/art/tasks.json')
next(t for t in tasks if t['id'] == TID).update(task)
write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks)
write(PROMPTS,p)
qa = {'task_id':TID,'title':task['title'],'date':'2026-10-15','status':'validado',
    'reviewer':'Codex — revisão técnica e visual repetida após recuperação dos metadados; lote isolado para commit',
    'production':{'tool':'image_gen.imagegen','model':None,'model_note':p['model_note'],'mode':'built-in',
        'distinct_asset_generations':5,'revision_generations':0,'one_independent_asset_per_generation':True,
        'prompts_record':PROMPTS,'original_generations':'art-source/editable/ART-32/generations',
        'export_and_preview_source':SOURCE,'review_record_source':'art-source/editable/ART-32/record_review_art32.py'},
    'technical':{'passed':True,'asset_count':5,'report':'docs/art/qa/ART-32-technical.json',
        'plan_report':'docs/art/qa/ART-32-plan-validation.json','prerequisite_report':'docs/art/qa/ART-32-prerequisites.json',
        'scope_report':'docs/art/qa/ART-32-scope-audit.json','format':'PNG RGBA 8-bit sRGB',
        'all_export_size_px':[768,512],'all_source_size_px':[1536,1024],'true_alpha':True,
        'outer_edge_alpha_max':0,'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'export':p['technical_export']},
    'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],
        'scales':'Cinco previews nativos 768x512 e cinco previews de display 640x426 inspecionados individualmente em resolução original. Contact sheet também revisada (exibida reduzida), além de dois canvases 1920x1080 em resolução original.',
        'inspected_previews':proofs+[sheet,*canvas_proofs],'canvas_proofs_are_scene_integration':False,
        'checks':['Cartoon adulto, roxo Cogna, contornos ameixa, volumes sombreados e luz superior esquerda coerentes com os concepts.',
            'Recortes limpos nos dois fundos, sem checkerboard ou halo amplo; sombras locais preservadas.',
            'Situações distintas legíveis por objetos e ambiente. Observações individuais registradas por asset.',
            'Sem pessoas, rostos, mãos ou identidade nova. Masters preservados e pré-requisitos verificados por hash.',
            'Sem palavras, números, títulos, opções, efeitos, logos, gráficos ou UI no bitmap. Telas e prancheta vazias.',
            'Freezer e PDV usam referências reais aprovadas; respectivos hashes registrados.',
            'Fontes RGBA preservadas. Export usa apenas redução uniforme e padding, sem pintura nem limpeza de alfa.']},
    'assets':records,'scope':scope,
    'pending':[],
    'recovery':{'record':'docs/art/qa/ART-32-metadata-recovery.json','new_generations':0,
        'artwork_bytes_preserved':True,'manual_visual_review_repeated':True,
        'proofs_reviewed_again':proofs+[sheet,*canvas_proofs]},
    'commit_reference':'Recorded in the serial queue history after commit creation.',
    'integration_note':'Unity não foi executado por esta conversa; sem alterações de gameplay, cenas ou catálogo e sem push.'}
write('docs/art/qa/ART-32.json',qa)
print(json.dumps({'task':TID,'status':'validado','technical':True,'visual':True,'unrelated_paths':unrelated},indent=2))
