"""Persist ART-31 QA after visual review of native, display and 1920x1080 proofs."""
import sys
sys.dont_write_bytecode = True
from produce_art31 import ROOT, TID, BASE, PROMPTS, PREVIEWS, SOURCE, read, write, sha, run
import json

observations = {
    'EVENT_EXTRA_LOCAL_DEMAND': 'Travessia e pegadas físicas no pavimento convergem para a entrada aberta do restaurante roxo; mesa com pratos e bolsa de passageiro reforçam circulação local. Sem pessoas, texto ou setas de UI.',
    'EVENT_LUNCH_DEMAND_PEAK': 'Muitos pratos distintos aguardam no passe de refeições, com bandejas, pilhas de louça e campainha. Relógio físico sem números marca meio-dia com ponteiros sobrepostos; leitura de almoço e volume clara.',
    'EVENT_CORPORATE_ORDER': 'Dois caixotes de transporte contêm muitas embalagens kraft uniformes; três refeições abertas, maleta executiva, telefone sem dígitos e prédio comercial ao fundo distinguem encomenda de empresa.',
    'EVENT_COMPETITOR_TEMPORARY_CLOSURE': 'Restaurante de toldo verde com persiana fechada e cadeado, caixa de ferramentas e rolo de pintura indica manutenção temporária. Restaurante vizinho roxo permanece com porta aberta; fachadas sem escrita.',
    'EVENT_NEW_COMPETITOR': 'Novo restaurante com toldo coral/verde, fita cerimonial, tesoura no banco e três balões contrasta com restaurante estabelecido de toldo roxo. Inauguração próxima legível sem pessoas ou escrita.'
}

m = read('docs/art/manifest.json')
p = read(PROMPTS)
technical = read('docs/art/qa/ART-31-technical.json')
plan = read('docs/art/qa/ART-31-plan-validation.json')
assert technical['passed'] and plan['passed']
assert len(p['generations']) == 5 and all(g['selected'] for g in p['generations'])
records, native_proofs, display_proofs = [], [], []
sheet = PREVIEWS + '/ART-31-contact-sheet.png'
canvas_proofs = [PREVIEWS + '/ART-31-canvas-' + theme + '.png' for theme in ['light','dark']]
for a in [a for a in m['assets'] if a['task_id'] == TID]:
    result = next(r for r in technical['results'] if r['id'] == a['id'])
    assert result['sha256'] == a['sha256']
    rec = next(r for r in p['assets'] if r['id'] == a['id'])
    assert sha(rec['source_path']) == rec['source_sha256']
    for ref,digest in rec['reference_sha256'].items():
        assert sha(ref) == digest
    assert sha(rec['catalog_source']) == rec['catalog_source_sha256']
    assert rec['actual_size_px'] == [768,512] and rec['source_size_px'] == [1536,1024]
    assert rec['alpha']['outer_edge_alpha_max'] == 0
    native = PREVIEWS + '/' + a['catalog_id'] + '-native-light-dark.png'
    display = PREVIEWS + '/' + a['catalog_id'] + '-display-light-dark.png'
    native_proofs.append(native)
    display_proofs.append(display)
    for proof in [native,display,sheet,*canvas_proofs]:
        assert (ROOT / proof).is_file()
    rec.update(status='validado', qa={'technical':True,'visual':True,'identity':None},
        visual_observation=observations[a['id']],
        inspected_previews=[native,sheet,*canvas_proofs],
        display_proof=display,
        display_review_note='The exact 640x426 light/dark preview row was inspected within the contact sheet, at original resolution.')
    records.append(rec)
    a.update(status='validado', qa={'technical':True,'visual':True,'identity':None})

task = next(t for t in m['tasks'] if t['id'] == TID)
task['status'] = 'validado'
tasks = read('docs/art/tasks.json')
next(t for t in tasks if t['id'] == TID)['status'] = 'validado'
p['technical_export'] = {
    'operation':'Uniform full-frame reduction from 1536x1024 to 720x480; centered on transparent 768x512 canvas at offset [24,16].',
    'illustration_and_transparency_origin':'Integrated imagegen; original RGBA sources preserved byte-for-byte.',
    'safety_padding':'Uses the existing event-vignette export method to preserve all source pixels and ensure a transparent outer edge.',
    'visual_edits':False,
    'display_fit':'Contain: 639x426 inside 640x426 slot; no independent axis scaling.'
}
write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks)
write(PROMPTS,p)

qa = {
    'task_id':TID, 'title':task['title'], 'date':'2026-10-15', 'status':'validado',
    'reviewer':'Codex — inspeção visual e técnica',
    'production':{
        'tool':'image_gen.imagegen','model':None,
        'model_note':'Specific model identifier is not exposed by the integrated tool.',
        'mode':'built-in','distinct_asset_generations':5,'revision_generations':0,
        'one_independent_asset_per_generation':True,
        'prompts_record':PROMPTS,'original_generations':'art-source/editable/ART-31/generations',
        'export_and_preview_source':SOURCE,
        'review_record_source':'art-source/editable/ART-31/finalize_art31.py'
    },
    'technical':{
        'passed':True,'asset_count':5,
        'report':'docs/art/qa/ART-31-technical.json',
        'plan_report':'docs/art/qa/ART-31-plan-validation.json',
        'prerequisite_report':'docs/art/qa/ART-31-prerequisites.json',
        'scope_report':'docs/art/qa/ART-31-scope-audit.json',
        'format':'PNG RGBA 8-bit sRGB','all_export_size_px':[768,512],
        'all_source_size_px':[1536,1024],'full_source_resampled_size_px':[720,480],
        'transparent_offset_px':[24,16],'true_alpha':True,'outer_edge_alpha_max':0,
        'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],
        'export':p['technical_export'],
        'import_metadatas':'Single Sprite, 100 PPU, Bilinear, mipmaps off, sRGB, alpha transparency on, FullRect, no compression, max texture 1024. Editor not executed.'
    },
    'visual':{
        'passed':True,'backgrounds':['#F5F0E8','#24113D'],
        'scales':'Cada arquivo revisado no preview nativo 768x512; cada linha de display 640x426 revista na contact sheet em resolução original; duas provas de canvas 1920x1080 também inspecionadas. Contain preserva proporção.',
        'inspected_previews':native_proofs+[sheet,*canvas_proofs],
        'display_proof_rows_in_contact_sheet':display_proofs,
        'canvas_proofs_are_scene_integration':False,
        'checks':[
            'Cinco vinhetas independentes no cartoon adulto aprovado, com contornos ameixa, arquitetura marfim, roxo Cogna, volumes sombreados e luz superior esquerda coerentes.',
            'Contornos, silhuetas, perspectiva, luz e materiais revisados nos dois fundos. Recortes sem halo amplo, checkerboard incorporado ou fundo retangular; sombras locais de contato preservadas.',
            'Demanda local, pico de almoço, encomenda corporativa, manutenção temporária do concorrente e inauguração distinguem-se pelos props e situações observados por asset.',
            'Sem pessoas, rostos ou mãos. QA de identidade não aplicável; masters canônicos e pré-requisitos mantidos e verificados por hash.',
            'Sem letras, números, preços, logos, título, opções, efeitos, gráficos ou UI. Relógio, pegadas no pavimento, fita e tesoura são elementos físicos da situação.',
            'Catálogo mantém três opções dinâmicas por evento; nenhuma opção é rasterizada. IDs, EventData e regras permanecem intactos.',
            'As fontes RGBA originais são preservadas e têm hashes registrados. Export contém apenas redução uniforme e padding técnico, sem pintura, extração de fundo ou correção manual do alfa.',
            'Textos e fundos dos previews são exclusivos das provas fora de Assets/Art/new.'
        ]
    },
    'assets':records,
    'scope':{
        'base_commit':BASE,'task_only':TID,'unity_executed':False,
        'gameplay_changed':False,'scene_integration_performed':False,'push_performed':False
    },
    'pending':[],
    'integration_note':'Aplicação nas cenas e teste no Unity pertencem à integração posterior, fora deste lote autorizado.'
}
write('docs/art/qa/ART-31.json',qa)

base = json.loads(run('git','show',BASE+':docs/art/manifest.json'))
assert len(m['assets']) == len(base['assets']) and len(m['tasks']) == len(base['tasks'])
changed_assets = [a['id'] for a,b in zip(m['assets'],base['assets']) if a != b]
changed_tasks = [t['id'] for t,b in zip(m['tasks'],base['tasks']) if t != b]
assert changed_assets == task['asset_ids'] and changed_tasks == [TID]
assert {k:v for k,v in m.items() if k not in ['assets','tasks']} == {k:v for k,v in base.items() if k not in ['assets','tasks']}
base_tasks = json.loads(run('git','show',BASE+':docs/art/tasks.json'))
assert [t['id'] for t,b in zip(tasks,base_tasks) if t != b] == [TID]
paths = run('git','diff','--name-only',BASE).splitlines() + run('git','ls-files','--others','--exclude-standard').splitlines()
allowed_exact = {'docs/art/manifest.json','docs/art/tasks.json','docs/art/prompts/ART-31.json'}
allowed_prefixes = ('art-source/editable/ART-31/','art-source/previews/ART-31/','docs/art/qa/ART-31')
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
write('docs/art/qa/ART-31-scope-audit.json',{
    'task_id':TID,'passed':True,'base_commit':BASE,'branch':'dev/new-screens',
    'changed_manifest_asset_ids':changed_assets,'changed_manifest_task_ids':changed_tasks,
    'changed_tasks_json_ids':[TID],'unrelated_manifest_records_preserved':True,
    'current_lot_only':True,'all_sprite_meta_guids_unique':True,
    'source_and_catalog_reference_hashes_verified':True,
    'reviewed_paths':sorted(set(paths+['docs/art/qa/ART-31-scope-audit.json']))
})
print('ART-31 validated; source hashes, unchanged records, current lot scope and sprite GUIDs audited.')
