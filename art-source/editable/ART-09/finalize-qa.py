"""Record the completed ART-09 visual review; production pixels are read only."""
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

manifest=read('docs/art/manifest.json');technical=read('docs/art/qa/ART-09-technical.json')
measurements=read('docs/art/qa/ART-09-measurements.json');production=read('docs/art/prompts/ART-09.json')
assert technical['passed'] and technical['asset_count']==18 and not technical['errors']
assert len(measurements['assets'])==18 and len(measurements['state_families'])==3
assert all(f['bounds_and_pivot_identical'] and f['states_distinct'] and f['max_export_alpha_delta']==0 and f['max_display_alpha_delta']==0 for f in measurements['state_families'])
assert measurements['progress_track_fill_alpha_identical']
selected=[a for a in manifest['assets'] if a['task_id']=='ART-09']
by_id={a['id']:a for a in production['assets']};measured={a['id']:a for a in measurements['assets']}
for a in selected:
    r=by_id[a['id']];d=measured[a['id']]
    assert hashlib.sha256((project/manifest['asset_root']/a['path']).read_bytes()).hexdigest()==a['sha256']==r['sha256']==d['sha256']
    assert a['actual_size_px']==a['export_size_px']==d['actual_size_px']
    assert d['nine_slice_corners_pixel_equal'] and d['alpha_extrema']==[0,255]
    assert d['nine_slice_uniform_cut_strips'] or d['pixel_caps_preserved']
    source=project/a['source_editable']
    assert hashlib.sha256(source.read_bytes()).hexdigest()==r['source_sha256']
    assert {e.tag.rsplit('}',1)[-1] for e in ET.parse(source).iter()} <= {'svg','defs','clipPath','rect','path','g'}

qa={
    'task_id':'ART-09','date':'2026-10-14','status':'validado','reviewer':'Codex — revisão visual do agente',
    'technical_validation':{
        'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-09',
        'passed':True,'asset_count':18,'errors':[],'report':'docs/art/qa/ART-09-technical.json'
    },
    'visual_validation':{
        'passed':True,'backgrounds':['#F5F0E8','#24113D'],'export_scale':'1:1 nos seis painéis de revisão',
        'display_method':'9-slice com dimensões do manifesto; cantos sem reescala e centro/arestas expandidos.',
        'contact_sheet':'art-source/previews/ART-09/ART-09-contact-sheet.png',
        'preview_source':'art-source/editable/qa/ART-09-preview.py',
        'review_pages':measurements['review_pages'],'family_previews':measurements['family_previews'],
        'assembly_preview':measurements['assembly_preview'],
        'checks':[
            'Todos os 18 PNGs revisados em export 1:1 e nos tamanhos de exibição do manifesto, sobre fundos claros/escuros.',
            'Pílula do HUD em violeta profundo, KPI claro e sublinhado lavanda coerentes com o hub e as DREs aprovadas.',
            'Track e fill separados, com alfa idêntico; montagem em zero, metade e cheio revisada sem gravar percentual no bitmap.',
            'Pílulas normal/selecionada/desabilitada mantêm geometria, alfa, bounds e pivot idênticos; estados visualmente distintos.',
            'Avisos neutro/sucesso/atenção usam preenchimento e contorno semântico, com alfa idêntico e sem símbolos incorporados.',
            'Cabeçalho, linhas neutra/subtotal/positiva/negativa e coluna total preservam o tratamento sóbrio da DRE.',
            'Estados das linhas neutra/subtotal/positiva/negativa têm alfa exatamente igual, sem deslocamento entre variantes.',
            'Painel de gráfico vazio, sem dados, barras, eixos, legenda ou rótulo rasterizados.',
            'Cantos 9-slice preservados pixel a pixel e centro expansível uniforme, sem emendas ou contornos deformados.',
            'Sublinhado mantém quatro pixels de canto transparentes; display 240×4 com extremidades de um pixel preservadas.',
            'Alfa zero real nos recortes, centros opacos e antialias limpo; sem checkerboard, halo ou sombra incorporados.',
            'Dimensões exatas, RGBA8, perfil sRGB, hashes e fontes SVG sem texto ou bitmap incorporado conferidos.'
        ]
    },
    'state_family_measurements':measurements['state_families'],
    'progress_track_fill_alpha_identical':True,'progress_checked_fill_widths_px':measurements['progress_checked_fill_widths_px'],
    'assembly_notes':production['assembly_notes'],
    'assets':[],'pending':[],'integration_performed':False
}
for a in selected:
    r=by_id[a['id']];d=measured[a['id']]
    a['status']='validado'
    a['qa']={'technical':True,'visual':True,'identity':False,'identity_note':'Componente geométrico sem personagem.',
        'record':'docs/art/qa/ART-09.json','notes':r['design']['note'],
        'preview':d['display_preview'],'details':d['detail_preview'],
        'bounds_and_pivot_preserved':True,'nine_slice_corners_preserved':True}
    r['status']='validado'
    qa['assets'].append({'id':a['id'],'name':r['name'],'family':r['family'],'state':r['state'],'part':r['part'],
        'technical':True,'visual':True,'notes':r['design']['note'],'actual_size_px':a['actual_size_px'],
        'display_size_px':a['display_size_px'],'sha256':a['sha256'],'source_editable':a['source_editable'],
        'display_preview':d['display_preview'],'detail_preview':d['detail_preview']})
next(t for t in manifest['tasks'] if t['id']=='ART-09').update({'status':'validado','qa_record':'docs/art/qa/ART-09.json'})
manifest['phase']='produção por lotes; ART-01, ART-02, ART-03, ART-04, ART-05, ART-06, ART-07, ART-08 e ART-09 validados'
tasks=read('docs/art/tasks.json')
next(t for t in tasks if t['id']=='ART-09').update({'status':'validado','qa_record':'docs/art/qa/ART-09.json'})
production.update({'status':'validado','qa_record':'docs/art/qa/ART-09.json'})
write('docs/art/qa/ART-09.json',qa);write('docs/art/manifest.json',manifest)
write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-09.json',production)
print(json.dumps({'task_id':'ART-09','status':'validado','png_count':18,'svg_count':18,'pending':[]}))
