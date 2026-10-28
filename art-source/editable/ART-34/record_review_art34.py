"""Persist the completed ART-34 visual review; no automatic visual inspection."""
import json
from pathlib import Path
import re
import sys
sys.dont_write_bytecode = True
from produce_art34 import ROOT, TID, BASE, PROMPTS, PREVIEWS, SOURCE, read, write, sha, run

if '--confirm-visual-review' not in sys.argv:
    raise SystemExit('Inspect all native/display light-dark proofs, both canvases and CH01 identity before recording.')

observations = {
    'REPORT_CONCLUSION': 'Pasta ameixa e folhas vazias em primeiro plano, planta saudável e fachada iluminada ao fundo comunicam encerramento e desenvolvimento do estabelecimento. Arquitetura reconhecível do ART-15: cobertura plana, ventilação, testeira vazia, toldos roxos, paredes marfim e interior âmbar. Sem pessoas, troféu, confete, medalha, números ou gráfico.',
    'BANKRUPTCY_OVERLAY': 'Fachada íntegra e compatível com ART-15, porta fechada, luzes apagadas e cadeiras recolhidas sobre as mesas indicam operação parada e pouco movimento. Sem abandono, danos, humilhação, palavras ou alertas. A mesa externa aparece aberta, com cadeiras aninhadas; essa variação do prompt não altera o briefing e foi aceita na revisão.',
    'EMPTY_CART': 'Carrinho vazio com quatro rodízios, cesta e estrutura coerentes em perspectiva. Grades e vazio interno legíveis em 480x320; transparência entre as barras e sob a estrutura nos dois fundos. Sem produtos, etiqueta ou alerta.',
    'EMPTY_TEAM': 'Prancheta com folha de proposta vazia e dois porta-crachás sem retratos nem texto. Alças roxa e lavanda distintas, recortes e ferragens completos. Folhas e crachás são superfícies vazias; sem rostos, ícones de pessoa ou nomes.',
    'EMPTY_RESULTS': 'Pasta lavanda com folhas vazias em leque e pequeno fecho metálico. Silhueta legível nos dois fundos; sem linhas, palavras, dados ou gráfico. Estado visual distinto da conclusão por não conter estabelecimento nem planta.',
    'CH01_CONCERNED': 'Rosto, pele, olhos castanhos, bob ondulado com risca lateral, argolas douradas, camisa lilás, calça grafite com cinto e sapatos preservam o master CH01. Sobrancelhas levemente contraídas e boca neutra comunicam preocupação serena. Postura ereta, mãos e pés completos, proporção adulta e dignidade preservadas. Comparação direta com o canônico em 420x760 nos dois fundos; sem cenário ou outras pessoas.'
}

m, p = read('docs/art/manifest.json'), read(PROMPTS)
task = next(t for t in m['tasks'] if t['id'] == TID)
technical = read('docs/art/qa/ART-34-technical.json')
plan = read('docs/art/qa/ART-34-plan-validation.json')
prereq = read('docs/art/qa/ART-34-prerequisites.json')
assert technical['passed'] and plan['passed'] and prereq['passed']
assert run('git','rev-parse','HEAD').strip() == BASE
assert run('git','branch','--show-current').strip() == 'dev/new-screens'
assert read('docs/art/characters.json')[0]['identity_hash'] == sha('Assets/Art/new/characters/CH01/ch01_canonical.png')

sheet = PREVIEWS+'/ART-34-contact-sheet.png'
canvases = [PREVIEWS+'/ART-34-canvas-'+t+'.png' for t in ['light','dark']]
identity = PREVIEWS+'/CH01-identity-comparison.png'
proofs, records, importers = [], [], []
for a in [a for a in m['assets'] if a['task_id'] == TID]:
    rec = next(r for r in p['assets'] if r['id'] == a['id'])
    check = next(r for r in technical['results'] if r['id'] == a['id'])
    assert sha(rec['path']) == rec['sha256'] == check['sha256']
    assert sha(rec['source_path']) == rec['source_sha256']
    for ref, digest in rec['reference_sha256'].items():
        assert sha(ref) == digest, ref
    assert rec['actual_size_px'] == a['export_size_px']
    assert rec['alpha']['outer_edge_alpha_max'] == 0
    from PIL import Image
    im = Image.open(ROOT/rec['path'])
    assert im.mode == 'RGBA' and im.info.get('icc_profile')
    meta = (ROOT/(rec['path']+'.meta')).read_text(encoding='utf-8')
    for setting in ['enableMipMap: 0','sRGBTexture: 1','spriteMode: 1','spritePixelsToUnits: 100',
                    'spritePivot: {x: 0.5, y: 0.5}','spriteBorder: {x: 0, y: 0, z: 0, w: 0}',
                    'alphaIsTransparency: 1','textureType: 8','textureCompression: 0']:
        assert setting in meta, (a['id'],setting)
    limits = [int(x) for x in re.findall(r'maxTextureSize: (\d+)',meta)]
    assert limits and min(limits) >= max(a['export_size_px'])
    importers.append({'id':a['id'],'path':rec['path']+'.meta','sha256':sha(rec['path']+'.meta'),
                      'max_texture_sizes':limits,'passed':True})
    key = Path(a['path']).stem
    native = PREVIEWS+'/'+key+'-native-light-dark.png'
    display = PREVIEWS+'/'+key+'-display-light-dark.png'
    individual = [native,display]
    if a['character_ids']:
        individual.append(identity)
        rec.update(identity_qa='passed_against_canonical_master',
                   canonical_asset='Assets/Art/new/characters/CH01/ch01_canonical.png',
                   canonical_sha256=sha('Assets/Art/new/characters/CH01/ch01_canonical.png'),
                   identity_comparison=identity)
    else:
        individual.append(sheet)
    for path in [*individual,*canvases]:
        assert (ROOT/path).is_file()
    proofs.extend(individual)
    qa = {'technical':True,'visual':True,'identity':True if a['character_ids'] else None}
    rec.update(status='validado',qa=qa,visual_observation=observations[a['id']],
               inspected_previews=[*individual,*canvases],display_proof=display,
               display_review_note='Native and exact manifest display-box light/dark proofs inspected at original resolution; contain preserves aspect.')
    records.append(rec)
    a.update(status='validado',qa=qa)
task.update(status='validado',delivery_preview=canvases[0])
tasks = read('docs/art/tasks.json')
next(t for t in tasks if t['id'] == TID).update(task)

base = json.loads(run('git','show',BASE+':docs/art/manifest.json'))
assert len(m['assets']) == len(base['assets']) and len(m['tasks']) == len(base['tasks'])
changed_assets = [a['id'] for a,b in zip(m['assets'],base['assets']) if a != b]
changed_tasks = [t['id'] for t,b in zip(m['tasks'],base['tasks']) if t != b]
assert changed_assets == task['asset_ids'] and changed_tasks == [TID]
assert {k:v for k,v in m.items() if k not in ['assets','tasks']} == {k:v for k,v in base.items() if k not in ['assets','tasks']}
base_tasks = json.loads(run('git','show',BASE+':docs/art/tasks.json'))
assert [a['id'] for a,b in zip(tasks,base_tasks) if a != b] == [TID]
for dep in prereq['dependencies']:
    for a in dep['assets']:
        assert sha(a['path']) == a['sha256']
write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks)
write(PROMPTS,p)

scope = {'task_id':TID,'passed':True,'base_commit':BASE,'branch':'dev/new-screens',
         'initial_head_verified':True,'initial_tree_clean':True,'current_lot_only':True,
         'changed_manifest_asset_ids':changed_assets,'changed_manifest_task_ids':changed_tasks,
         'unrelated_manifest_records_preserved':True,'reference_and_prerequisite_hashes_verified':True,
         'unrelated_changed_paths':[],'recorded_before_commit':True}
qa = {
    'task_id':TID,'title':task['title'],'date':'2026-10-15','status':'validado',
    'reviewer':'Codex — inspeção visual individual e verificação técnica',
    'production':{'tool':'image_gen.imagegen','mode':'built-in','model':None,
                  'model_note':p['model_note'],'distinct_asset_generations':6,'revision_generations':0,
                  'one_independent_asset_per_generation':True,'prompts_record':PROMPTS,
                  'original_generations':'art-source/editable/ART-34/generations',
                  'export_and_preview_source':SOURCE,
                  'review_record_source':'art-source/editable/ART-34/record_review_art34.py'},
    'technical':{'passed':True,'asset_count':6,'report':'docs/art/qa/ART-34-technical.json',
                 'plan_report':'docs/art/qa/ART-34-plan-validation.json',
                 'prerequisite_report':'docs/art/qa/ART-34-prerequisites.json',
                 'scope_report':'docs/art/qa/ART-34-scope-audit.json',
                 'format':'PNG RGBA 8-bit sRGB','true_alpha':True,'outer_edge_alpha_max':0,
                 'export_sizes_px':{'objects':[768,512],'CH01_CONCERNED':[1024,1536]},
                 'source_sizes_px':{'objects':[1536,1024],'CH01_CONCERNED':[1024,1536]},
                 'export_operation':'Uniform full-frame reduction and centered transparent padding only; original generated sources preserved byte-for-byte. No visual edits or alpha cleanup.',
                 'character_padding':'At least 10 percent transparent canvas margin on every side.',
                 'display_fit':'contain / preserveAspect','pivot':[0.5,0.5],
                 'nine_slice_lbrt_px':[0,0,0,0],'sprite_importers':importers},
    'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],
              'scales':'Seis provas nativas e seis provas de display (cinco caixas 480x320 e uma 420x760) inspecionadas individualmente em resolução original. Dois canvases 1920x1080, contact sheet dos cinco objetos e comparação canônica CH01 inspecionados em resolução original.',
              'inspected_previews':list(dict.fromkeys(proofs+canvases)),
              'canvas_proofs_are_scene_integration':False,
              'checks':['Linguagem editorial cartoon adulta e paleta coerentes com os concepts.',
                        'Recortes, silhuetas e espaços vazios limpos nos fundos claro e escuro.',
                        'Conclusão sem medalha, troféu, confete, números ou gráficos.',
                        'Dificuldade sem danos, humilhação, drama ou palavras; fachada compatível com ART-15.',
                        'Estados vazios legíveis por objetos, com documentos e crachás sem conteúdo.',
                        'Identidade, proporção e figurino CH01 comparados diretamente ao master.',
                        'Mãos, pés, perspectiva, luz, bordas e superfícies vazias revisados.',
                        'Nenhuma nova UI implementada; componentes comuns dos modais continuam reutilizáveis.']},
    'assets':records,'scope':scope,'pending':[],
    'commit_reference':'Recorded in serial queue history after commit creation.',
    'integration_note':'Sem execução do Unity, alteração de cenas, gameplay, catálogos, economia ou pacotes; sem push.'
}
write('docs/art/qa/ART-34.json',qa)
write('docs/art/qa/ART-34-scope-audit.json',scope)
paths = set(run('git','diff','--name-only',BASE).splitlines()+run('git','ls-files','--others','--exclude-standard').splitlines())
exact = {'docs/art/manifest.json','docs/art/tasks.json',PROMPTS}
finals = {r['path'] for r in records} | {r['path']+'.meta' for r in records}
prefixes = ('art-source/editable/ART-34/','art-source/previews/ART-34/','docs/art/qa/ART-34')
unrelated = sorted(path for path in paths if path not in exact and path not in finals and not path.startswith(prefixes))
assert not unrelated, unrelated
scope['reviewed_paths'] = sorted(paths)
qa['scope'] = scope
write('docs/art/qa/ART-34-scope-audit.json',scope)
write('docs/art/qa/ART-34.json',qa)
print(json.dumps({'task':TID,'status':'validado','technical':True,'visual':True,
                  'asset_count':6,'paths':len(paths),'unrelated_paths':unrelated},indent=2))
