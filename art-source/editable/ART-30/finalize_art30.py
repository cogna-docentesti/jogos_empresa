"""Persist ART-30 review findings after explicit visual inspection of all QA proofs."""
import sys
sys.dont_write_bytecode = True
from produce_art30 import ROOT, TID, BASE, PROMPTS, PREVIEWS, SOURCE, read, write, sha, run, alpha_metrics
from PIL import Image
import json

observations = {
    'EVENT_BULK_PURCHASE_OPPORTUNITY': 'Pallet com múltiplas caixas, dois caixotes de hortaliças frescas, dois sacos e moedas lisas; quantidade e abastecimento favorável distinguem a compra em volume.',
    'EVENT_SUPPLIER_DISCOUNT': 'Um caixote compacto, saco de ingrediente, duas moedas lisas e etiqueta física lilás totalmente vazia, presa por cordão; transação simples distinta do pallet em volume.',
    'EVENT_SUPPLIER_DELAY': 'Carrinho parado com caixa fechada, caixote quase vazio com um tomate e relógio físico sem números; espera e escassez legíveis sem pessoa ou UI.',
    'EVENT_INGREDIENT_PRICE_INCREASE': 'Balança física de base ameixa, pratos suspensos e corrente coerentes; moedas no prato mais baixo contrapõem ingredientes no prato elevado. Sem gráfico ou valor rasterizado.',
    'EVENT_EXCESSIVE_FOOD_WASTE': 'Caixotes em excesso, hortaliças murchas e tomates enrugados, recipiente grafite com tampa apoiada; desperdício legível sem insetos ou sujeira repulsiva.'
}

m = read('docs/art/manifest.json')
p = read(PROMPTS)
technical = read('docs/art/qa/ART-30-technical.json')
plan = read('docs/art/qa/ART-30-plan-validation.json')
assert technical['passed'] and plan['passed']
previews = []
records = []
for a in [a for a in m['assets'] if a['task_id'] == TID]:
    result = next(r for r in technical['results'] if r['id'] == a['id'])
    assert result['sha256'] == a['sha256']
    rec = next(r for r in p['assets'] if r['id'] == a['id'])
    assert sha(rec['source_path']) == rec['source_sha256']
    for ref,digest in rec['reference_sha256'].items():
        assert sha(ref) == digest
    assert sha(rec['catalog_source']) == rec['catalog_source_sha256']
    native = PREVIEWS+'/'+a['catalog_id']+'-native-light-dark.png'
    display = PREVIEWS+'/'+a['catalog_id']+'-display-light-dark.png'
    for proof in [native,display]:
        assert (ROOT/proof).is_file()
    previews.extend([native,display])
    rec.update(status='validado', qa={'technical':True,'visual':True,'identity':None}, visual_observation=observations[a['id']], inspected_previews=[native,display])
    records.append(rec)
    a.update(status='validado', qa={'technical':True,'visual':True,'identity':None})
task = next(t for t in m['tasks'] if t['id'] == TID)
task['status'] = 'validado'
tasks = read('docs/art/tasks.json')
next(t for t in tasks if t['id'] == TID)['status'] = 'validado'
p['technical_export_correction'] = {'initial_issue':'Direct full-frame 768x512 reduction left faint nonzero alpha on the lower edge of supplier_delay. Export was interrupted before marking any asset generated.', 'resolution':'Uniformly reduced all full 1536x1024 source frames to 720x480 and centered them with transparent padding [24,16] in final 768x512 canvases. No crop, repainting, alpha cleanup or extra illustration generation.', 'resolved':True}
write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks)
write(PROMPTS,p)
qa = {
    'task_id':TID, 'title':task['title'], 'date':'2026-10-15', 'status':'validado', 'reviewer':'Codex — inspeção visual e técnica',
    'production':{'tool':'image_gen.imagegen','model':None,'model_note':'Specific model identifier is not exposed by the integrated tool.','mode':'built-in','distinct_asset_generations':5,'revision_generations':0,'one_independent_asset_per_generation':True,'prompts_record':PROMPTS,'original_generations':'art-source/editable/ART-30/generations','export_and_preview_source':SOURCE,'review_record_source':'art-source/editable/ART-30/finalize_art30.py'},
    'technical':{'passed':True,'asset_count':5,'report':'docs/art/qa/ART-30-technical.json','plan_report':'docs/art/qa/ART-30-plan-validation.json','prerequisite_report':'docs/art/qa/ART-30-prerequisites.json','scope_report':'docs/art/qa/ART-30-scope-audit.json','format':'PNG RGBA 8-bit sRGB','all_export_size_px':[768,512],'all_source_size_px':[1536,1024],'full_source_resampled_size_px':[720,480],'transparent_offset_px':[24,16],'true_alpha':True,'outer_edge_alpha_max':0,'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'import_metadatas':'Single Sprite, 100 PPU, Bilinear, mipmaps off, sRGB, alpha transparency on, FullRect, no compression, max texture 1024. Editor not executed.','resolved_export_issue':p['technical_export_correction']},
    'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'scales':'Todos os cinco arquivos inspecionados em previews nativos 768x512 e em contact sheet original, uma caixa 640x426 por fundo. Contain mantém proporção e desenha 639x426 dentro da caixa.','inspected_previews':previews+[PREVIEWS+'/ART-30-contact-sheet.png'],'checks':['Cinco vinhetas independentes, um PNG por evento, no estilo cartoon adulto dos concepts 01-hub e 03-gestao; contorno ameixa, volumes simples e luz superior esquerda coerentes.','Silhueta, perspectiva, materiais, contornos, sombras e leitura das cinco situações conferidos. Sem cortes de objetos, checkerboard, fundo retangular ou halo amplo. Pequenas sombras de contato são mantidas.','Sem pessoas, rostos ou mãos; QA de identidade não se aplica. Masters canônicos e todos os pré-requisitos preservados.','Sem título, opções, efeitos, preço, texto, dígitos, símbolos monetários, marcas, logo, gráfico, moldura ou UI. Etiqueta vazia e relógio sem números são props físicos.','Não há rasterização das três opções do catálogo. IDs, paths, títulos e regras dos EventData permanecem intactos.','Fontes RGBA originais preservadas em cópias integrais com hashes; export apenas por redução uniforme e padding transparente.','Previews com fundos e legendas existem somente fora de Assets/Art/new; PNGs finais são recortes sem essas legendas.']},
    'assets':records,
    'scope':{'base_commit':BASE,'task_only':'ART-30','unity_executed':False,'gameplay_changed':False,'scene_integration_performed':False,'push_performed':False},
    'pending':[],
    'integration_note':'Aplicação nas cenas e teste dentro do Unity pertencem à integração posterior, fora deste lote autorizado.'
}
write('docs/art/qa/ART-30.json',qa)

# Structural audit against the exclusive queue base. All unrelated records are immutable.
base = json.loads(run('git','show',BASE+':docs/art/manifest.json'))
changed_assets = [a['id'] for a,b in zip(m['assets'],base['assets']) if a != b]
changed_tasks = [t['id'] for t,b in zip(m['tasks'],base['tasks']) if t != b]
assert changed_assets == task['asset_ids'] and changed_tasks == [TID]
assert {k:v for k,v in m.items() if k not in ['assets','tasks']} == {k:v for k,v in base.items() if k not in ['assets','tasks']}
base_tasks = json.loads(run('git','show',BASE+':docs/art/tasks.json'))
assert [t['id'] for t,b in zip(tasks,base_tasks) if t != b] == [TID]
paths = run('git','diff','--name-only',BASE).splitlines() + run('git','ls-files','--others','--exclude-standard').splitlines()
allowed_exact = {'docs/art/manifest.json','docs/art/tasks.json','docs/art/prompts/ART-30.json','Assets/Art/new/events/illustrations.meta'}
allowed_prefixes = ('art-source/editable/ART-30/','art-source/previews/ART-30/','docs/art/qa/ART-30')
finals = {rec['path'] for rec in records} | {rec['path']+'.meta' for rec in records}
assert all(path in allowed_exact or path in finals or path.startswith(allowed_prefixes) for path in paths), paths
assert run('git','branch','--show-current').strip() == 'dev/new-screens'
assert run('git','rev-parse','HEAD').strip() == BASE
guids = {}
for meta in (ROOT/'Assets/Art/new').rglob('*.meta'):
    for line in meta.read_text(encoding='utf-8').splitlines():
        if line.startswith('guid: '):
            guid = line[6:]
            assert guid not in guids, (str(meta),guids.get(guid))
            guids[guid] = str(meta)
write('docs/art/qa/ART-30-scope-audit.json',{'task_id':TID,'passed':True,'base_commit':BASE,'branch':'dev/new-screens','changed_manifest_asset_ids':changed_assets,'changed_manifest_task_ids':changed_tasks,'changed_tasks_json_ids':[TID],'unrelated_manifest_records_preserved':True,'current_lot_only':True,'all_sprite_meta_guids_unique':True,'source_and_catalog_reference_hashes_verified':True,'reviewed_paths':sorted(set(paths+['docs/art/qa/ART-30-scope-audit.json']))})
print('ART-30 marked validated; source hashes, scope and metadata audited.')
