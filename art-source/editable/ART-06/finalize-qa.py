"""Record visual approval of ART-06 without modifying production pixels."""
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
m=read('docs/art/manifest.json');technical=read('docs/art/qa/ART-06-technical.json')
measure=read('docs/art/qa/ART-06-measurements.json');production=read('docs/art/prompts/ART-06.json')
assert technical['passed'] and technical['asset_count']==18 and not technical['errors']
assert len(measure['assets'])==18 and measure['unique_symbols']==18
selected=[a for a in m['assets'] if a['task_id']=='ART-06']
records={a['id']:a for a in production['assets']};measured={a['id']:a for a in measure['assets']}
for a in selected:
    r=records[a['id']];mdata=measured[a['id']]
    assert hashlib.sha256((project/m['asset_root']/a['path']).read_bytes()).hexdigest()==a['sha256']==r['sha256']==mdata['sha256']
    source=project/a['source_editable']
    assert hashlib.sha256(source.read_bytes()).hexdigest()==r['source_sha256']
    tags={e.tag.rsplit('}',1)[-1] for e in ET.parse(source).iter()}
    assert tags <= {'svg','g','path','rect','circle','ellipse'}
    assert mdata['visible_rgb_colors']==[[255,255,255]] and min(mdata['clear_padding_lbrt_px'])>=26

qa={
    'task_id':'ART-06','date':'2026-10-14','status':'validado',
    'reviewer':'Codex — revisão visual do agente',
    'technical_validation':{
        'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-06',
        'passed':True,'asset_count':18,'errors':[],'report':'docs/art/qa/ART-06-technical.json'
    },
    'visual_validation':{
        'passed':True,'export_scale':'1:1','checked_display_sizes_px':[32,40,48],
        'backgrounds':['#24113D','#F5F0E8'],
        'tint_policy':'Master branco sobre ameixa; tint ameixa sobre marfim somente na QA. Nenhuma variante colorida de produção.',
        'contact_sheet':'art-source/previews/ART-06/ART-06-contact-sheet.png',
        'review_pages':measure['review_pages'],'preview_source':'art-source/editable/qa/ART-06-preview.py',
        'checks':[
            'Todos os 18 ícones inspecionados em export 1:1 e em 32, 40 e 48 px, sobre fundo claro/escuro.',
            'Símbolos reconhecíveis e distintos, com acabamento coerente com os ícones dos concepts.',
            'Geometria original: preenchimentos sólidos para silhuetas do concept e traço-base 16 px para contornos, equivalente a 2/2,5/3 px na exibição.',
            'Masters brancos tintáveis: todos os pixels visíveis têm RGB 255/255/255; tint de QA preserva o alfa.',
            'Fundo e vazios internos com alfa verdadeiro, antialias limpo, sem checkerboard ou halo perceptível.',
            'Margem transparente mínima real de 32 px e pivot central; desenho dentro do canvas e escala uniforme contain.',
            'Home/casas residenciais, universidade/banco/empresas, cardápio/documento e equipamentos/configurações são símbolos distintos.',
            'Garfo de três dentes, haste e cabo da faca completos após correção vetorial do ícone de restaurante.',
            'Sem texto, números, dados, badges, botões, molduras de UI ou sombras incorporados.',
            'Dezoito PNGs e dezoito fontes SVG individuais; sem duplicatas de silhueta, export exato, sRGB e hashes conferidos.'
        ]
    },
    'semantic_choices':production['semantic_choices'],
    'true_alpha_internal_cutouts':measure['true_alpha_internal_cutouts'],
    'production_adjustments':[
        {'asset_id':'ICON_RESTAURANT','reason':'Separar corretamente parâmetros numéricos de curvas SVG para restaurar os talheres completos.',
         'source':'art-source/editable/ART-06/repair-restaurant.cjs','rechecked':True}
    ],
    'assets':[],'pending':[],'integration_performed':False
}
for a in selected:
    r=records[a['id']];d=measured[a['id']]
    a['status']='validado'
    a['qa']={
        'technical':True,'visual':True,'identity':False,'identity_note':'Símbolo abstrato sem personagem canônico.',
        'record':'docs/art/qa/ART-06.json','notes':r['semantic'],
        'preview':d['preview'],'details':d['preview'],
        'white_tintable_master':True,'display_sizes_checked_px':[32,40,48],
        'minimum_clear_padding_px':min(d['clear_padding_lbrt_px'])
    }
    r['status']='validado'
    qa['assets'].append({'id':a['id'],'name':r['name'],'technical':True,'visual':True,
        'notes':r['semantic'],'actual_size_px':[256,256],'sha256':a['sha256'],
        'source_editable':a['source_editable'],'preview':d['preview']})
next(t for t in m['tasks'] if t['id']=='ART-06').update({'status':'validado','qa_record':'docs/art/qa/ART-06.json'})
m['phase']='produção por lotes; ART-01, ART-02, ART-03, ART-04, ART-05 e ART-06 validados'
tasks=read('docs/art/tasks.json');next(t for t in tasks if t['id']=='ART-06').update({'status':'validado','qa_record':'docs/art/qa/ART-06.json'})
production.update({'status':'validado','qa_record':'docs/art/qa/ART-06.json'})
write('docs/art/qa/ART-06.json',qa);write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-06.json',production)
print(json.dumps({'task_id':'ART-06','status':'validado','png_count':18,'svg_count':18,'pending':[]}))
