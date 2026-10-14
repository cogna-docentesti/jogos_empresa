"""Record ART-05 visual approval after inspection; never edits production pixels."""
from pathlib import Path
import argparse
import hashlib
import json
import xml.etree.ElementTree as ET

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--visual-reviewed',action='store_true',required=True)
parser.parse_args()
project=Path(__file__).resolve().parents[3]
def read(p):return json.loads((project/p).read_text(encoding='utf-8-sig'))
def write(p,d):(project/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
manifest=read('docs/art/manifest.json')
technical=read('docs/art/qa/ART-05-technical.json')
measurements=read('docs/art/qa/ART-05-measurements.json')
production=read('docs/art/prompts/ART-05.json')
assert technical['passed'] and technical['asset_count']==24 and not technical['errors']
assert len(measurements['assets'])==24 and len(measurements['state_families'])==5
assert all(f['bounds_and_pivot_identical'] and f['states_distinct'] for f in measurements['state_families'])
selected=[a for a in manifest['assets'] if a['task_id']=='ART-05']
by_id={a['id']:a for a in production['assets']}
measured={a['id']:a for a in measurements['assets']}
for a in selected:
    r=by_id[a['id']]
    digest=hashlib.sha256((project/manifest['asset_root']/a['path']).read_bytes()).hexdigest()
    assert digest==a['sha256']==r['sha256']==measured[a['id']]['sha256']
    source=project/a['source_editable']
    assert hashlib.sha256(source.read_bytes()).hexdigest()==r['source_sha256']
    assert {e.tag.rsplit('}',1)[-1] for e in ET.parse(source).iter()} <= {'svg','defs','clipPath','rect','circle','g'}

qa={
    'task_id':'ART-05','date':'2026-10-14','status':'validado',
    'reviewer':'Codex — revisão visual do agente',
    'technical_validation':{
        'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-05',
        'passed':True,'asset_count':24,'errors':[],'report':'docs/art/qa/ART-05-technical.json'
    },
    'visual_validation':{
        'passed':True,'backgrounds':['#F5F0E8','#24113D'],
        'export_scale':'1:1 nos previews individuais e por família',
        'display_method':'9-slice nas superfícies; escala uniforme contain em radio, checkbox e puxador circular',
        'contact_sheet':'art-source/previews/ART-05/ART-05-contact-sheet.png',
        'preview_source':'art-source/editable/qa/ART-05-preview.py',
        'assembly_preview':'art-source/previews/ART-05/controls-assembly-1x.png',
        'checks':[
            'Todos os 24 PNGs inspecionados sobre fundos claros/escuros, em tamanho de export e de exibição.',
            'Campos normal, foco, inválido e desabilitado distintos por contorno/preenchimento, sem halo ou mensagens incorporadas.',
            'Abas normal, selecionada, hover, pressionada e desabilitada coerentes com os concepts e ART-04.',
            'Radio, checkbox e toggle mantêm bounds, pivot e geometria nos três estados; as bases não contêm ícones, check ou puxador.',
            'Cantos/área externa com alfa zero e contornos antialias limpos; centros opacos; sem checkerboard.',
            'Cantos 9-slice iguais pixel a pixel e cortes em faixas uniformes; ausência de emendas e deformação na exibição.',
            'Slider com trilho, fill e puxador em arquivos distintos; montagem em zero, metade e máximo revisada.',
            'Toggle montado em QA com UI_SLIDER_THUMB como camada independente, à esquerda/direita, sem alterar os PNGs de base.',
            'Scrollbar com trilho e puxador separados e margem transparente de 1 px; montagem e alinhamento central revisados.',
            'Stepper entregue vazio, sem sinais, quantidade ou divisórias incorporados.',
            'Dimensões reais, perfil sRGB, hashes, fontes SVG e ausência de texto/bitmaps incorporados conferidos.'
        ],
        'family_previews':[
            {'family':f,'export':f'art-source/previews/ART-05/{f}-export-1x.png',
             'display':f'art-source/previews/ART-05/{f}-display-1x.png'}
            for f in ['input','tab','radio','checkbox','toggle','slider','stepper','scrollbar']
        ]
    },
    'state_family_measurements':measurements['state_families'],
    'alpha_note':'Silhueta geométrica, bounds e pivot iguais por família. Nos campos, a rasterização dos contornos de 2/3 px difere em até 1 nível de alfa (1/255) em seis pixels antialias dos cantos; demais famílias têm alfa idêntico pixel a pixel.',
    'production_adjustments':[
        {'assets':['UI_SCROLLBAR_TRACK','UI_SCROLLBAR_THUMB'],
         'reason':'Garantir alfa zero com cantos pequenos e manter raio/margem dentro das faixas sliced.',
         'result':'Margem externa transparente de 1 px e raios 2/1 px, sem alterar dimensões, pivot ou borders do manifesto.',
         'initial_findings':'docs/art/qa/ART-05-initial-alpha-findings.json'}
    ],
    'assembly_notes':production['assembly_notes'],
    'assets':[],'pending':[],'integration_performed':False
}
for a in selected:
    r=by_id[a['id']];d=measured[a['id']]
    a['status']='validado'
    a['qa']={
        'technical':True,'visual':True,'identity':False,'identity_note':'Componente geométrico sem personagem.',
        'record':'docs/art/qa/ART-05.json','notes':r['design']['note'],
        'preview':d['display_preview'],'details':d['detail_preview'],
        'bounds_and_pivot_preserved':True,'nine_slice_corners_preserved':d['nine_slice_corners_pixel_equal']
    }
    r['status']='validado'
    qa['assets'].append({
        'id':a['id'],'family':r['family'],'state':r['state'],'part':r['part'],
        'technical':True,'visual':True,'notes':r['design']['note'],
        'actual_size_px':a['actual_size_px'],'sha256':a['sha256'],'source_editable':a['source_editable'],
        'display_preview':d['display_preview'],'detail_preview':d['detail_preview']
    })
next(t for t in manifest['tasks'] if t['id']=='ART-05').update({'status':'validado','qa_record':'docs/art/qa/ART-05.json'})
manifest['phase']='produção por lotes; ART-01, ART-02, ART-03, ART-04 e ART-05 validados'
tasks=read('docs/art/tasks.json')
next(t for t in tasks if t['id']=='ART-05').update({'status':'validado','qa_record':'docs/art/qa/ART-05.json'})
production.update({'status':'validado','qa_record':'docs/art/qa/ART-05.json'})
write('docs/art/qa/ART-05.json',qa);write('docs/art/manifest.json',manifest)
write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-05.json',production)
print(json.dumps({'task_id':'ART-05','status':'validado','png_count':24,'svg_count':24,'pending':[]}))
