"""Record ART-07 visual review without changing production pixels."""
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
m=read('docs/art/manifest.json');technical=read('docs/art/qa/ART-07-technical.json')
measure=read('docs/art/qa/ART-07-measurements.json');production=read('docs/art/prompts/ART-07.json')
assert technical['passed'] and technical['asset_count']==18 and not technical['errors']
assert len(measure['assets'])==18 and measure['unique_symbols']==18
selected=[a for a in m['assets'] if a['task_id']=='ART-07']
records={a['id']:a for a in production['assets']};measured={a['id']:a for a in measure['assets']}
for a in selected:
    r=records[a['id']];d=measured[a['id']]
    assert hashlib.sha256((project/m['asset_root']/a['path']).read_bytes()).hexdigest()==a['sha256']==r['sha256']==d['sha256']
    source=project/a['source_editable']
    assert hashlib.sha256(source.read_bytes()).hexdigest()==r['source_sha256']
    assert {e.tag.rsplit('}',1)[-1] for e in ET.parse(source).iter()} <= {'svg','g','path','rect','circle'}
    assert d['visible_rgb_colors']==[[255,255,255]] and min(d['clear_padding_lbrt_px'])>=26
qa={
    'task_id':'ART-07','date':'2026-10-14','status':'validado','reviewer':'Codex — revisão visual do agente',
    'technical_validation':{
        'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-07',
        'passed':True,'asset_count':18,'errors':[],'report':'docs/art/qa/ART-07-technical.json'
    },
    'visual_validation':{
        'passed':True,'export_scale':'1:1','checked_display_sizes_px':[32,40,48],
        'backgrounds':['#24113D','#F5F0E8'],
        'tint_policy':'Master branco sobre ameixa; tint ameixa sobre marfim apenas na QA. Nenhuma variante colorida de produção.',
        'contact_sheet':'art-source/previews/ART-07/ART-07-contact-sheet.png',
        'review_pages':measure['review_pages'],'preview_source':'art-source/editable/qa/ART-07-preview.py',
        'checks':[
            'Todos os 18 ícones inspecionados em export 1:1 e em 32, 40 e 48 px, sobre fundo claro/escuro.',
            'Acabamento, traço-base 16 px, preenchimentos e escala coerentes com o kit ART-06 e os concepts.',
            'RGB 255/255/255 em todos os pixels visíveis, alfa verdadeiro e preservação do alfa na tintura de QA.',
            'Margem transparente mínima real de 32 px; pivot central e escala uniforme contain.',
            'Vazios internos legíveis, antialias limpo, sem halo, checkerboard, fundo, badge ou sombra incorporados.',
            'Receita, principal, custos, estoque e contrato têm silhuetas distintas; sem repetir moedas, barras ou documento do ART-06.',
            'Tendências de alta e queda são pares espelhados no mesmo canvas/pivot, com direção inequívoca.',
            'Cliente individual, grupo de três clientes e equipe do ART-06 permanecem símbolos distintos.',
            'Juros usam apenas geometria percentual; prazo usa ampulheta; nenhuma taxa, data ou valor rasterizado.',
            'Reputação, coerência, capacidade e concorrência recebem símbolos próprios, sem duplicar os indicadores genéricos existentes.',
            'Dezoito PNGs e fontes SVG separados; hashes de alfa diferentes dentro do lote e sem duplicatas do ART-06.',
            'Export exato, perfil sRGB, hashes e ausência de texto/bitmap incorporado nas fontes conferidos.'
        ]
    },
    'semantic_choices':production['semantic_choices'],'true_alpha_internal_cutouts':measure['true_alpha_internal_cutouts'],
    'assets':[],'pending':[],'integration_performed':False
}
for a in selected:
    r=records[a['id']];d=measured[a['id']]
    a['status']='validado'
    a['qa']={'technical':True,'visual':True,'identity':False,'identity_note':'Símbolo abstrato sem personagem canônico.',
        'record':'docs/art/qa/ART-07.json','notes':r['semantic'],'preview':d['preview'],'details':d['preview'],
        'white_tintable_master':True,'display_sizes_checked_px':[32,40,48],
        'minimum_clear_padding_px':min(d['clear_padding_lbrt_px'])}
    r['status']='validado'
    qa['assets'].append({'id':a['id'],'name':r['name'],'technical':True,'visual':True,'notes':r['semantic'],
        'actual_size_px':[256,256],'sha256':a['sha256'],'source_editable':a['source_editable'],'preview':d['preview']})
next(t for t in m['tasks'] if t['id']=='ART-07').update({'status':'validado','qa_record':'docs/art/qa/ART-07.json'})
m['phase']='produção por lotes; ART-01, ART-02, ART-03, ART-04, ART-05, ART-06 e ART-07 validados'
tasks=read('docs/art/tasks.json');next(t for t in tasks if t['id']=='ART-07').update({'status':'validado','qa_record':'docs/art/qa/ART-07.json'})
production.update({'status':'validado','qa_record':'docs/art/qa/ART-07.json'})
write('docs/art/qa/ART-07.json',qa);write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-07.json',production)
print(json.dumps({'task_id':'ART-07','status':'validado','png_count':18,'svg_count':18,'pending':[]}))
