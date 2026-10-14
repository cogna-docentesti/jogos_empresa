"""Record the completed visual review; does not modify production pixels."""
from pathlib import Path
import argparse
import hashlib
import json

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--visual-reviewed', action='store_true', required=True)
parser.parse_args()
project = Path(__file__).resolve().parents[3]
def read(p):
    return json.loads((project/p).read_text(encoding='utf-8-sig'))
def write(p, data):
    (project/p).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

manifest = read('docs/art/manifest.json')
technical = read('docs/art/qa/ART-03-technical.json')
measurements = read('docs/art/qa/ART-03-measurements.json')
production = read('docs/art/prompts/ART-03.json')
assert technical['passed'] and technical['asset_count'] == 14 and not technical['errors']
selected = [a for a in manifest['assets'] if a['task_id'] == 'ART-03']
for asset in selected:
    current_hash = hashlib.sha256((project/manifest['asset_root']/asset['path']).read_bytes()).hexdigest()
    measured = next(m for m in measurements['assets'] if m['id'] == asset['id'])
    assert current_hash == measured['sha256'] == asset['sha256']

notes = {
    'UI_TOPBAR_VIOLET': 'Violeta com gradiente discreto, tela inteira e alfa 255; sem texto ou ícones.',
    'UI_NAVBAR_PLUM': 'Ameixa opaco com separador superior discreto; sem conteúdo incorporado.',
    'UI_PANEL_IVORY': 'Marfim e contorno neutro discretos, cantos limpos e centro uniforme para 9-slice.',
    'UI_PANEL_LAVENDER': 'Lavanda suave derivado dos tokens, cantos limpos e centro uniforme para 9-slice.',
    'UI_PANEL_PLUM': 'Ameixa e contorno lavanda discretos; cantos limpos e centro uniforme para 9-slice.',
    'UI_PANEL_SHADOW': 'Sombra semitransparente independente com queda suave até alfa zero; composição atrás do painel revisada.',
    'UI_CARD_NORMAL': 'Superfície clara isolada; raio e bounds externos alinhados aos dois overlays.',
    'UI_CARD_SELECTED_OUTLINE': 'Contorno violeta de 3 px com centro totalmente transparente; alinhamento sobre card normal revisado.',
    'UI_CARD_DISABLED_OVERLAY': 'Overlay a 64% com centro semitransparente; conteúdo de QA permanece visível sob a camada; bounds alinhados.',
    'UI_MODAL_SURFACE': 'Superfície marfim isolada sem scrim ou sombra; ampliação 900×440 revisada.',
    'UI_TOOLTIP_SURFACE': 'Superfície marfim de raio 12 px, sem seta ou conteúdo; ampliação 420×160 revisada.',
    'UI_SCRIM': 'Pixel sólido ameixa com alfa 143; expansão para preencher 1920×1080 revisada.',
    'UI_BADGE_CIRCLE': 'Círculo violeta com contorno lavanda, alfa fora da silhueta e leitura a 64×64 revisados.',
    'BRAND_COGNA_LOGO': 'Desenho e todos os pixels RGBA iguais ao logo oficial. Boa leitura em fundo claro; baixo contraste original sobre ameixa foi preservado e não autoriza recolorir a marca.'
}
qa = {
    'task_id':'ART-03', 'date':'2026-10-14', 'status':'validado',
    'reviewer':'Codex — revisão visual do agente',
    'technical_validation':{
        'command':'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-03',
        'passed':True, 'asset_count':14, 'errors':[], 'report':'docs/art/qa/ART-03-technical.json'
    },
    'visual_validation':{
        'passed':True, 'backgrounds':['#F5F0E8','#24113D'],
        'export_scale':'1:1; previews individuais preservados',
        'display_scale':'Tamanhos de display do manifesto; nove regiões nos componentes sliced, escala uniforme para badge/logo e expansão sólida no scrim.',
        'contact_sheet':'art-source/previews/ART-03/ART-03-contact-sheet.png',
        'preview_source':'art-source/editable/qa/ART-03-preview.py',
        'checks':[
            'Todos os arquivos inspecionados em fundos claros/escuros e tamanho de exibição.',
            'Cores, raios, contornos e espessuras coerentes com os concepts e tokens.',
            'PNG RGBA 8-bit sRGB e tamanhos exatos; fontes SVG existentes para os 13 itens geométricos.',
            'Cantos transparentes, sem checkerboard incorporado ou halo perceptível.',
            'Cantos do 9-slice comparados pixel a pixel antes/depois da ampliação, sem deformação.',
            'Estados do card preservam canvas, pivot, raio externo e bounds; rig revisado.',
            'Sombra, superfície, scrim e overlays entregues em arquivos independentes.',
            'Logo oficial convertido de paleta/tRNS para RGBA sem alteração dos pixels ou do alfa.'
        ],
        'composition_previews':[
            'art-source/previews/ART-03/card-states-1x.png',
            'art-source/previews/ART-03/panel-shadow-composition-1x.png'
        ]
    },
    'specification_notes':[
        'UI_SCRIM: display_fit corrigido de contain para stretch, pois o pixel uniforme deve preencher a caixa inteira de layout.',
        'BRAND_COGNA_LOGO: referência de produção registrada no arquivo oficial existente; nenhuma reinterpretação do concept.',
        'Marca oficial destinada a superfície clara; variante clara para fundo escuro não faz parte deste lote.'
    ],
    'assets':[], 'pending':[], 'integration_performed':False
}
measure_lookup = {m['id']:m for m in measurements['assets']}
for asset in selected:
    measurement = measure_lookup[asset['id']]
    asset['status'] = 'validado'
    asset['qa'] = {
        'technical':True, 'visual':True, 'identity':False,
        'identity_note':'Sem personagem; preservação da marca oficial verificada separadamente quando aplicável.',
        'record':'docs/art/qa/ART-03.json', 'notes':notes[asset['id']],
        'preview':measurement['display_preview'], 'details':measurement['detail_preview']
    }
    if asset['id'] == 'BRAND_COGNA_LOGO':
        asset['qa']['brand_preservation'] = True
    qa['assets'].append({
        'id':asset['id'], 'technical':True, 'visual':True, 'notes':notes[asset['id']],
        'sha256':asset['sha256'], 'actual_size_px':asset['actual_size_px'],
        'display_preview':measurement['display_preview'], 'detail_preview':measurement['detail_preview']
    })
for task in manifest['tasks']:
    if task['id'] == 'ART-03':
        task.update({'status':'validado','qa_record':'docs/art/qa/ART-03.json'})
manifest['phase'] = 'produção por lotes; ART-01, ART-02 e ART-03 validados'
production['status'] = 'validado'
production['qa_record'] = 'docs/art/qa/ART-03.json'
for item in production['assets']:
    item['status'] = 'validado'
tasks = read('docs/art/tasks.json')
for task in tasks:
    if task['id'] == 'ART-03':
        task.update({'status':'validado','qa_record':'docs/art/qa/ART-03.json'})
write('docs/art/qa/ART-03.json',qa)
write('docs/art/manifest.json',manifest)
write('docs/art/tasks.json',tasks)
write('docs/art/prompts/ART-03.json',production)
print(json.dumps({'task_id':'ART-03','status':'validado','assets':len(selected),'pending':[]}))
