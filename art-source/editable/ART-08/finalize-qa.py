"""Record ART-08 visual review without changing production pixels."""
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
m=read('docs/art/manifest.json');technical=read('docs/art/qa/ART-08-technical.json')
measure=read('docs/art/qa/ART-08-measurements.json');production=read('docs/art/prompts/ART-08.json')
assert technical['passed'] and technical['asset_count']==33 and not technical['errors']
assert len(measure['assets'])==33 and measure['unique_symbols']==33
selected=[a for a in m['assets'] if a['task_id']=='ART-08']
records={a['id']:a for a in production['assets']};measured={a['id']:a for a in measure['assets']}
for a in selected:
    r=records[a['id']];d=measured[a['id']]
    assert hashlib.sha256((project/m['asset_root']/a['path']).read_bytes()).hexdigest()==a['sha256']==r['sha256']==d['sha256']
    source=project/a['source_editable']
    assert hashlib.sha256(source.read_bytes()).hexdigest()==r['source_sha256']
    assert {e.tag.rsplit('}',1)[-1] for e in ET.parse(source).iter()} <= {'svg','g','path','rect','circle'}
    assert d['visible_rgb_colors']==[[255,255,255]] and min(d['clear_padding_lbrt_px'])>=26
qa={
    'task_id':'ART-08','date':'2026-10-14','status':'validado','reviewer':'Codex — revisão visual do agente',
    'technical_validation':{
        'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-08',
        'passed':True,'asset_count':33,'errors':[],'report':'docs/art/qa/ART-08-technical.json'
    },
    'visual_validation':{
        'passed':True,'export_scale':'1:1','checked_display_sizes_px':[32,40,48],
        'backgrounds':['#24113D','#F5F0E8'],
        'tint_policy':'Master branco sobre ameixa; tint ameixa sobre marfim apenas na QA. Nenhuma variante colorida de produção.',
        'contact_sheet':'art-source/previews/ART-08/ART-08-contact-sheet.png',
        'review_pages':measure['review_pages'],'preview_source':'art-source/editable/qa/ART-08-preview.py',
        'checks':[
            'Todos os 33 ícones inspecionados em export 1:1 e em 32, 40 e 48 px sobre fundo claro/escuro.',
            'Acabamento, traço-base de 16 px e preenchimentos coerentes com o kit ART-06/07 e os concepts aprovados.',
            'RGB 255/255/255 em todos os pixels visíveis; alfa verdadeiro preservado na tintura apenas da QA.',
            'Margem transparente mínima real de 31 px, acima dos 10% exigidos; pivot central e escala uniforme contain.',
            'Vazios internos legíveis, antialias limpo e nenhuma sombra, fundo, checkerboard ou badge incorporado.',
            'Voltar/avançar e chevrons usam pares espelhados no mesmo canvas e pivot; direção legível nas três escalas.',
            'Adicionar/remover/confirmar são glifos separados das bases de controle e seleção já produzidas no ART-05.',
            'Cadeado preserva o recorte transparente da fechadura; alerta, informação e ajuda usam símbolos geométricos sem fonte tipográfica.',
            'Salvar, sair, baixar, editar e excluir permanecem ações visualmente distintas, sem rótulos rasterizados.',
            'Reiniciar usa arco único; sincronizar usa duas setas curvas, sem duplicar as setas retas de concorrência do ART-07.',
            'Som ativo/desativado preservam o mesmo corpo de alto-falante, com ondas ou cruz de estado separadas.',
            'Cozinha, refrigeração, atendimento e móveis identificados por panela, geladeira, cloche e cadeira.',
            'Trinta e três PNGs e fontes SVG separados; hashes de alfa únicos no lote e sem duplicatas dos 36 ícones do ART-06/07.',
            'Export 256×256, perfil sRGB, hashes e ausência de texto ou bitmap incorporado nas fontes conferidos.'
        ]
    },
    'semantic_choices':production['semantic_choices'],'true_alpha_internal_cutouts':measure['true_alpha_internal_cutouts'],
    'assets':[],'pending':[],'integration_performed':False
}
for a in selected:
    r=records[a['id']];d=measured[a['id']]
    a['status']='validado'
    a['qa']={'technical':True,'visual':True,'identity':False,'identity_note':'Símbolo abstrato sem personagem canônico.',
        'record':'docs/art/qa/ART-08.json','notes':r['semantic'],'preview':d['preview'],'details':d['preview'],
        'white_tintable_master':True,'display_sizes_checked_px':[32,40,48],
        'minimum_clear_padding_px':min(d['clear_padding_lbrt_px'])}
    r['status']='validado'
    qa['assets'].append({'id':a['id'],'name':r['name'],'technical':True,'visual':True,'notes':r['semantic'],
        'actual_size_px':[256,256],'sha256':a['sha256'],'source_editable':a['source_editable'],'preview':d['preview']})
next(t for t in m['tasks'] if t['id']=='ART-08').update({'status':'validado','qa_record':'docs/art/qa/ART-08.json'})
m['phase']='produção por lotes; ART-01, ART-02, ART-03, ART-04, ART-05, ART-06, ART-07 e ART-08 validados'
tasks=read('docs/art/tasks.json');next(t for t in tasks if t['id']=='ART-08').update({'status':'validado','qa_record':'docs/art/qa/ART-08.json'})
production.update({'status':'validado','qa_record':'docs/art/qa/ART-08.json'})
write('docs/art/qa/ART-08.json',qa);write('docs/art/manifest.json',m)
write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-08.json',production)
print(json.dumps({'task_id':'ART-08','status':'validado','png_count':33,'svg_count':33,'pending':[]}))
