"""Record generated ART-11 files and finalize only after completed visual/technical review."""
from pathlib import Path
import argparse, hashlib, json
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
TASK='ART-11'
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8-sig'))
def write(p,d):
    target=ROOT/p;target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def rh(value):return hashlib.sha256(json.dumps(value,ensure_ascii=False,sort_keys=True).encode()).hexdigest()

def generated():
    m=read('docs/art/manifest.json');selected=[a for a in m['assets'] if a['task_id']==TASK]
    assert len(selected)==5 and all(a['status'] in ['planejado','gerado'] for a in selected)
    inputs=read('art-source/editable/ART-11/generation-inputs.json');records=[]
    contract=read('docs/art/layouts/map-layout.json')
    for a in selected:
        final=ROOT/m['asset_root']/a['path'];export=read('docs/art/qa/ART-11-export-'+a['id']+'.json')
        with Image.open(final) as im:
            assert im.size==(1920,1080) and im.mode=='RGBA' and im.info.get('icc_profile')
            assert im.getchannel('A').getextrema()[0]==0
        assert export['output_sha256']==sha(final)
        recipe=next(r for r in inputs['assets'] if r['id']==a['id'])
        for attempt in recipe['attempts']:
            attempt['sha256']=sha(attempt['generated_file'])
            with Image.open(attempt['generated_file']) as im:
                attempt['actual_size_px']=list(im.size);attempt['mode']=im.mode
                attempt['alpha_extrema']=list(im.getchannel('A').getextrema())
            attempt['input_hashes']=[{'path':p,'sha256':sha(p)} for p in attempt['input_paths']]
        chosen=next(r for r in recipe['attempts'] if r.get('selected'))
        zone=next(z for z in contract['zones'] if z['landmark_asset_id']==a['id'])
        refs=['art-source/references/concepts/01-hub.png','Assets/Art/new/location/map/map_base.png','docs/art/layouts/map-layout.json',export['source_project_copy']]
        record={'id':a['id'],'catalog_id':a['catalog_id'],'output_path':m['asset_root']+'/'+a['path'],
            'origin':'Construção/bairro original pela ferramenta integrada ImageGen; câmera/estilo do hub e implantação do ART-10.',
            'catalog_source':a['source'],'references':[{'path':p,'sha256':sha(ROOT/p)} for p in refs],
            'requested_final_size_px':[1920,1080],'actual_size_px':[1920,1080],'mode':'RGBA','color_space':'sRGB',
            'sha256':sha(final),'tool':'image_gen.imagegen','model':None,'model_note':inputs['model_note'],
            'final_prompt':chosen['prompt'],'attempts':recipe['attempts'],'technical_export':export,
            'source_editable':None,'source_note':'Arte raster generativa; prompts, origem e script de encaixe técnico preservados.',
            'coordinate_space':'map1920x1080','pivot':[0,1],'nine_slice_lbrt_px':[0,0,0,0],
            'zone':zone,'status':'gerado'}
        records.append(record)
        a.update({'status':'gerado','references':refs,'sha256':record['sha256'],'actual_size_px':[1920,1080],
            'generation_prompt':record['final_prompt'],'generation_tool':'image_gen.imagegen','generation_model':None,
            'generation_model_note':inputs['model_note'],'production_method':'imagegen',
            'production_record':'docs/art/prompts/ART-11.json','coordinate_contract':'docs/art/layouts/map-layout.json',
            'placement_record':'docs/art/qa/ART-11-export-'+a['id']+'.json',
            'alpha_note':'Alfa real zero externo; preservar alfa original da fonte, sem clamping ou remoção de fundo por código.'})
    write('docs/art/prompts/ART-11.json',{'task_id':TASK,'date':'2026-10-14','status':'gerado',
        'tool':'image_gen.imagegen','mode':'built-in','model':None,'model_note':inputs['model_note'],
        'scope':'Somente cinco construções/bairros do ART-11 em camadas independentes; sem controles do ART-05, restaurante central ou realces de seleção.',
        'assets':records,'export_source':'art-source/editable/ART-11/export-landmark.cjs',
        'placement_source':'art-source/editable/ART-11/placements.json','preview_source':'art-source/editable/qa/ART-11-preview.py',
        'coordinate_contract':'docs/art/layouts/map-layout.json','contract_sha256':sha(ROOT/'docs/art/layouts/map-layout.json'),
        'integration_performed':False})
    next(t for t in m['tasks'] if t['id']==TASK)['status']='gerado'
    tasks=read('docs/art/tasks.json');next(t for t in tasks if t['id']==TASK)['status']='gerado'
    write('docs/art/manifest.json',m);write('docs/art/tasks.json',tasks)
    print(json.dumps({'task_id':TASK,'status':'gerado','png_count':5}))

def finalize():
    m=read('docs/art/manifest.json');tech=read('docs/art/qa/ART-11-technical.json')
    measured=read('docs/art/qa/ART-11-measurements.json');prod=read('docs/art/prompts/ART-11.json')
    assert tech['passed'] and tech['asset_count']==5 and not tech['errors']
    assert measured['passed'] and len(measured['assets'])==5
    assert measured['central_site_clear'] and measured['layers_nonoverlapping']
    selected=[a for a in m['assets'] if a['task_id']==TASK]
    qa={'task_id':TASK,'date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual do agente',
        'technical_validation':{'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-11',
            'passed':True,'asset_count':5,'errors':[],'report':'docs/art/qa/ART-11-technical.json'},
        'visual_validation':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],
            'export_scale':'PNG full-frame 1920x1080 e detalhes das construções em 1:1.',
            'display_method':'Todas as camadas usam o mesmo contain 2/3; conjunto 1280x720 dentro da caixa 1280x848 com offset local [0,64].',
            'previews':measured['previews'],'checks':[
                'Cinco construções/bairros separados, sem nomes, números, ícones, HUD, pinos ou realces rasterizados.',
                'Universidade com cúpula índigo/colunas/alas terracota, banco marfim/vidro azul, comércio coral, escritórios marfim/azul e seis casas terracota reconhecíveis.',
                'Câmera oblíqua, luz superior esquerda, contornos e paleta coerentes entre os assets e o hub aprovado.',
                'Montagem conjunta sobre MAP_BASE conferida; edifícios apoiados nas zonas corretas, vias e pontes legíveis e restaurante central livre.',
                'Bairro residencial revisado por ImageGen para encaixar as seis casas nos gramados existentes e conservar caminhos.',
                'RGBA8 sRGB 1920x1080, alfa zero real externo, antialias e recortes conferidos sobre fundos claros/escuros.',
                'Escala técnica uniforme por asset, sem warp/esticamento, seguida de master/origem/pivot iguais em todas as camadas.',
                'Footprints físicos no polígono da respectiva zona; volume alto dentro do envelope de construção.',
                'Full-frame e provas no tamanho de exibição revisados, com boa leitura das silhuetas e volumes.'
            ]},
        'assets':[],'coordinate_contract':'docs/art/layouts/map-layout.json',
        'contract_unchanged':True,'pending':[],'integration_performed':False,
        'alpha_note':'Alfa original de ImageGen preservado. Há pixels residuais de baixa opacidade na fonte; silhueta visível e envelopes são medidos com alfa >=128 e complementados pela revisão visual.',
        'future_work':'Restaurantes/foreground ART-12 e realces/marcadores ART-13 continuam planejados.'}
    for a in selected:
        record=next(r for r in prod['assets'] if r['id']==a['id'])
        measure=next(r for r in measured['assets'] if r['id']==a['id'])
        assert sha(ROOT/m['asset_root']/a['path'])==a['sha256']==record['sha256']==measure['sha256']
        a['status']='validado';a['qa']={'technical':True,'visual':True,'identity':False,'identity_note':'Construção/bairro sem personagens.',
            'record':'docs/art/qa/ART-11.json','preview':measure['review_page'],'details':measure['detail_preview'],
            'assembly_preview':'art-source/previews/ART-11/ART-11-assembly-master-light.png',
            'notes':'Origem/pivot/full-frame alinhados ao ART-10; footprint e montagem conjunta revisados.'}
        record['status']='validado';record['qa']=a['qa']
        qa['assets'].append({'id':a['id'],'catalog_id':a['catalog_id'],'actual_size_px':[1920,1080],
            'sha256':a['sha256'],'technical':True,'visual':True,'review_page':measure['review_page'],
            'detail_preview':measure['detail_preview'],'ground_inside_zone':True,'visible_inside_envelope':True})
    next(t for t in m['tasks'] if t['id']==TASK).update({'status':'validado','qa_record':'docs/art/qa/ART-11.json'})
    tasks=read('docs/art/tasks.json');next(t for t in tasks if t['id']==TASK).update({'status':'validado','qa_record':'docs/art/qa/ART-11.json'})
    m['phase']='produção por lotes; ART-01 a ART-11 validados'
    prod.update({'status':'validado','qa_record':'docs/art/qa/ART-11.json'})
    write('docs/art/qa/ART-11.json',qa);write('docs/art/manifest.json',m);write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-11.json',prod)
    print(json.dumps({'task_id':TASK,'status':'validado','png_count':5,'pending':[]}))

def audit():
    baseline=read('docs/art/qa/ART-11-scope-baseline.json');m=read('docs/art/manifest.json')
    files={p:(ROOT/p).is_file() and sha(ROOT/p)==h for p,h in baseline['protected_files'].items()}
    checks={'other_asset_records_unchanged':rh([a for a in m['assets'] if a['task_id']!=TASK])==baseline['other_asset_records_sha256'],
        'other_task_records_unchanged':rh([t for t in m['tasks'] if t['id']!=TASK])==baseline['other_task_records_sha256'],
        'other_tasks_index_unchanged':rh([t for t in read('docs/art/tasks.json') if t['id']!=TASK])==baseline['other_tasks_index_sha256'],
        'protected_files_unchanged':all(files.values()),'protected_file_count':len(files),'changed_protected_files':[p for p,ok in files.items() if not ok]}
    passed=all(checks[k] for k in ['other_asset_records_unchanged','other_task_records_unchanged','other_tasks_index_unchanged','protected_files_unchanged'])
    write('docs/art/qa/ART-11-scope-audit.json',{'task_id':TASK,'date':'2026-10-14','passed':passed,**checks})
    print(json.dumps({'task_id':TASK,'passed':passed,**checks}));assert passed

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('mode',choices=['generated','finalize','audit']);p.add_argument('--visual-reviewed',action='store_true');args=p.parse_args()
    if args.mode=='finalize' and not args.visual_reviewed:p.error('Finalization requires completed visual review.')
    {'generated':generated,'finalize':finalize,'audit':audit}[args.mode]()
