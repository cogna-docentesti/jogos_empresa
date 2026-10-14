"""Record the completed ART-04 visual review without changing production pixels."""
from pathlib import Path
import argparse
import hashlib
import json
import xml.etree.ElementTree as ET

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--visual-reviewed',action='store_true',required=True)
parser.parse_args()
project=Path(__file__).resolve().parents[3]
def read(p):
    return json.loads((project/p).read_text(encoding='utf-8-sig'))
def write(p,data):
    (project/p).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

manifest=read('docs/art/manifest.json')
technical=read('docs/art/qa/ART-04-technical.json')
measurements=read('docs/art/qa/ART-04-measurements.json')
production=read('docs/art/prompts/ART-04.json')
assert technical['passed'] and technical['asset_count']==20 and not technical['errors']
assert len(measurements['assets'])==20 and len(measurements['state_families'])==5
selected=[a for a in manifest['assets'] if a['task_id']=='ART-04']
by_id={a['id']:a for a in production['assets']}
measured={a['id']:a for a in measurements['assets']}
for asset in selected:
    record=by_id[asset['id']]
    current_hash=hashlib.sha256((project/manifest['asset_root']/asset['path']).read_bytes()).hexdigest()
    assert current_hash==asset['sha256']==record['sha256']==measured[asset['id']]['sha256']
    svg_source=project/asset['source_editable']
    assert hashlib.sha256(svg_source.read_bytes()).hexdigest()==record['source_sha256']
    tags={e.tag.rsplit('}',1)[-1] for e in ET.parse(svg_source).iter()}
    assert tags <= {'svg','defs','clipPath','rect','g'}, 'Unexpected bitmap/text/foreign content'

notes={
    'normal':'Cor base e contorno discretos, coerentes com a família e os concepts aprovados.',
    'hover':'Realce perceptível de preenchimento e contorno; sem expansão, halo ou deslocamento.',
    'pressed':'Preenchimento mais profundo e contorno reforçado; sem deslocamento da silhueta.',
    'disabled':'Preenchimento dessaturado e contorno suave; estado distinguível sem alterar a geometria.'
}
qa={
    'task_id':'ART-04','date':'2026-10-14','status':'validado',
    'reviewer':'Codex — revisão visual do agente',
    'technical_validation':{
        'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-04',
        'passed':True,'asset_count':20,'errors':[],'report':'docs/art/qa/ART-04-technical.json'
    },
    'visual_validation':{
        'passed':True,'backgrounds':['#F5F0E8','#24113D'],
        'export_scale':'1:1 nas cinco pranchas de família e nos previews individuais',
        'display_sizes_px':[[360,72],[64,64]],'display_method':'9-slice, bordas preservadas',
        'contact_sheet':'art-source/previews/ART-04/ART-04-contact-sheet.png',
        'preview_source':'art-source/editable/qa/ART-04-preview.py',
        'checks':[
            'Todos os vinte PNGs revisados em 1:1 e no tamanho de exibição, em fundos claros e escuros.',
            'Estados visualmente distintos, com cores derivadas dos tokens e acabamento coerente com os concepts e o ART-03.',
            'Mesmos bounds, pivot e silhueta alfa nos quatro estados de cada família; igualdade pixel a pixel no export e na exibição.',
            'Cantos com alfa zero e antialias limpo; centros opacos; sem checkerboard, halo ou sombra incorporados.',
            'Cantos 9-slice iguais pixel a pixel; cortes do stretch em faixas uniformes, sem emendas ou deformação.',
            'Preenchimentos planos, raio externo 12 px e contorno interno 2 px preservados entre os estados.',
            'Sem texto, ícones, números ou logos nos PNGs; SVGs contêm somente geometria vetorial.',
            'Dimensões reais, perfil sRGB, hashes e vinte fontes SVG editáveis conferidos.'
        ],
        'family_previews':[
            {'family':f['family'],
             'export':f"art-source/previews/ART-04/{f['family']}-states-export-1x.png",
             'display':f"art-source/previews/ART-04/{f['family']}-states-display-1x.png"}
            for f in measurements['state_families']
        ]
    },
    'state_family_measurements':measurements['state_families'],
    'assets':[],'pending':[],'integration_performed':False
}
for asset in selected:
    record=by_id[asset['id']]
    detail=measured[asset['id']]
    note=notes[record['state']]
    asset['status']='validado'
    asset['qa']={
        'technical':True,'visual':True,'identity':False,
        'identity_note':'Componente geométrico sem personagem.',
        'record':'docs/art/qa/ART-04.json','notes':note,
        'preview':detail['display_preview'],'details':detail['detail_preview'],
        'state_alpha_identical':True,'nine_slice_corners_preserved':True
    }
    record['status']='validado'
    qa['assets'].append({
        'id':asset['id'],'family':record['family'],'state':record['state'],
        'technical':True,'visual':True,'notes':note,
        'actual_size_px':asset['actual_size_px'],'sha256':asset['sha256'],
        'source_editable':asset['source_editable'],
        'display_preview':detail['display_preview'],'detail_preview':detail['detail_preview']
    })
manifest['tasks'][next(i for i,t in enumerate(manifest['tasks']) if t['id']=='ART-04')].update({
    'status':'validado','qa_record':'docs/art/qa/ART-04.json'
})
manifest['phase']='produção por lotes; ART-01, ART-02, ART-03 e ART-04 validados'
tasks=read('docs/art/tasks.json')
next(t for t in tasks if t['id']=='ART-04').update({'status':'validado','qa_record':'docs/art/qa/ART-04.json'})
production.update({'status':'validado','qa_record':'docs/art/qa/ART-04.json'})
write('docs/art/qa/ART-04.json',qa)
write('docs/art/manifest.json',manifest)
write('docs/art/tasks.json',tasks)
write('docs/art/prompts/ART-04.json',production)
print(json.dumps({'task_id':'ART-04','status':'validado','png_count':20,'svg_count':20,'pending':[]}))
