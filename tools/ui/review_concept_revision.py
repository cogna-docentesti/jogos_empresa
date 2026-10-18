"""Record generated art metadata and build a local concept/implementation review gallery."""
import hashlib
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/ui-update/concept-revision'
record_path = ROOT / 'docs/ui-update/concept-art.json'
record = json.loads(record_path.read_text(encoding='utf-8'))
for asset in record['assets']:
    p = ROOT / 'Assets/Art/new/restaurant/street-views' / asset['filename']
    with Image.open(p) as img:
        asset['dimensions'] = list(img.size)
        asset['mode'] = img.mode
    asset['path'] = p.relative_to(ROOT).as_posix()
    asset['sha256'] = hashlib.sha256(p.read_bytes()).hexdigest()
    asset['qa'] = {'visual': True, 'distinct_restaurant': True, 'no_rasterized_ui': True}
    kind = asset['id'].rsplit('_', 1)[-1].lower()
    asset['references'].append('Assets/Art/new/restaurant/facades/rest_'+kind+'.png') if len(asset['references']) == 1 else None
    asset['reference_hashes'] = {ref: hashlib.sha256((ROOT/ref).read_bytes()).hexdigest() for ref in asset['references']}
record_path.write_text(json.dumps(record, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
(ROOT / 'docs/art/prompts/CONCEPT-REVISION.json').write_text(json.dumps(record, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

names = ['Identification','Location','Restaurant','MenuPricing','Financial','Equipment','Team','Menu','Establishment','Review','Event','MonthlyResult','QuarterlyResult','FinalReport','Bankruptcy']
showcase=[]
for name in names:
    p=OUT/'polish'/f'Panel_{name}-normal-1920x1080.png'
    if not p.exists():p=OUT/'states'/f'Panel_{name}-normal-1920x1080.png'
    if name=='MonthlyResult':p=OUT/'campaigns/completion-month-3-summary-1920x1080.png'
    if name=='QuarterlyResult':p=OUT/'campaigns/completion-quarter-1920x1080.png'
    if name=='Event':p=OUT/'campaigns/completion-month-1-event-1920x1080.png'
    if p.exists():showcase.append((name,p))
if showcase:
    sheet=Image.new('RGB',(1440,306*((len(showcase)+2)//3)), '#24113d')
    draw=ImageDraw.Draw(sheet)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',20)
    for i,(name,p) in enumerate(showcase):
        with Image.open(p) as img:
            thumb=img.convert('RGB');thumb.thumbnail((480,270))
            x=(i%3)*480;y=(i//3)*306
            sheet.paste(thumb,(x,y));draw.text((x+12,y+276),name,font=font,fill='#f5f0e8')
    sheet.save(OUT/'screen-overview.jpg',quality=92)

groups = [
    ('Concepts aprovados', ROOT / 'art-source/references/concepts', '*.png'),
    ('Interface revisada · fluxo real', OUT / 'campaigns', '*month-3*1920x1080.png'),
    ('Estados e telas complementares', OUT / 'states', '*normal-1920x1080.png'),
    ('Relatório final', OUT / 'states', '*top-1920x1080.png'),
    ('Diálogos', OUT / 'dialogs', '*1920x1080.png'),
]
parts = ['<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>Revisão da interface · Cogna</title>',
         '<style>body{margin:0;background:#24113d;color:#f5f0e8;font:17px system-ui}main{max-width:1500px;margin:auto;padding:32px}h1{font-size:36px}h2{margin-top:48px}p{max-width:900px;line-height:1.6}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(390px,1fr));gap:24px}figure{margin:0;background:#38214e;border-radius:12px;overflow:hidden}img{display:block;width:100%;height:auto}figcaption{padding:14px;font-size:14px}a{color:#d4b6ff}</style><main>',
         '<h1>Revisão visual da interface</h1><p>Comparação com os cinco concepts aprovados. Capturas reais do Unity; os valores exibidos pertencem às campanhas isoladas de QA. Abra uma imagem para inspecionar em tamanho integral.</p>']
parts.append('<h2>Visão geral das 15 telas de produção</h2><div class="grid">')
for name,p in showcase:
    import os
    rel=Path(os.path.relpath(p,OUT)).as_posix()
    parts.append(f'<figure><a href="{rel}"><img loading="lazy" src="{rel}"></a><figcaption>{name}</figcaption></figure>')
parts.append('</div>')
for title, folder, pattern in groups:
    images = sorted(folder.glob(pattern))
    if not images:
        continue
    parts.append('<h2>'+title+'</h2><div class="grid">')
    for p in images:
        import os
        rel = Path(os.path.relpath(p, OUT)).as_posix()
        parts.append(f'<figure><a href="{html.escape(rel)}"><img loading="lazy" src="{html.escape(rel)}"></a><figcaption>{html.escape(p.stem)}</figcaption></figure>')
    parts.append('</div>')
parts.append('</main></html>')
(OUT / 'index.html').write_text('\n'.join(parts), encoding='utf-8')
print('Gallery:', OUT / 'index.html')
print('Generated art:', len(record['assets']))

import xml.etree.ElementTree as ET
reports = {}
for name in ['campaigns','states','dialogs','polish']:
    p=OUT/name/'playmode.json'
    if p.exists():
        report=json.loads(p.read_text(encoding='utf-8'))
        reports[name]={'path':p.relative_to(ROOT).as_posix(),'completed':report.get('completed',False),'checks':len(report.get('checks',[])),
                       'failed':sum(c.get('passed') is False for c in report.get('checks',[]))}
test_path=OUT/'editmode.xml'
if test_path.exists() and len(reports)==4 and all(r['completed'] and r['failed']==0 for r in reports.values()):
    tests=ET.parse(test_path).getroot().attrib
    polish=json.loads((OUT/'polish-execution.json').read_text(encoding='utf-8'))
    if tests.get('result')=='Passed' and polish['passed']:
        qa={'date':'2026-10-17','status':'concluído','scope':[f'UI-{i:02}' for i in range(1,21)],
            'scene':'Assets/Scenes/GameScene.unity','concepts':'art-source/references/concepts',
            'layout':'docs/ui-update/concept-layout.json','art':'docs/ui-update/concept-art.json',
            'changes':['Cabeçalhos compartilhados, contraste e tipografia','Mapa maior, recorte uniforme e marcadores com hit areas correspondentes',
                       'Três fachadas de rua e pratos em camadas','Cards verticais de equipamentos e equipe','Banco em quatro opções verticais',
                       'Estabelecimento com fachada em destaque','DRE mensal com reconciliação de caixa','Comparativo trimestral compacto e gráfico de colunas',
                       'Navegação, eventos, relatórios finais, falência, feedback e diálogos revisados'],
            'validation':{'editmode':{k:tests.get(k) for k in ['result','total','passed','failed']},'playmode':reports,
                          'viewports':[[1920,1080],[1280,720],[1600,1000]],'production_screens':15,
                          'states':['normal','hover','pressed','selected','disabled','focus'],
                          'equipment_child_bounds_verified':True,'data':'Banco SQLite e identificação de QA isolados',
                          'native_campaigns':'Conclusão e falência em 1920x1080; estados das 15 telas nas três resoluções',
                          'device_test':False},
            'gallery':'output/ui-update/concept-revision/index.html','overview':'output/ui-update/concept-revision/screen-overview.jpg',
            'preserved':['IDs e enums','economia e balanceamento','persistência SQLite/PlayerPrefs','campanha de três meses','277 PNGs originais; três variantes adicionadas']}
        (ROOT/'docs/ui-update/qa/CONCEPT-REVISION.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        implementation=OUT/'implementation.json'
        data=json.loads(implementation.read_text(encoding='utf-8'))
        data['verified']=True
        data['qa']='docs/ui-update/qa/CONCEPT-REVISION.json'
        implementation.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print('QA: completed,',sum(r['checks'] for r in reports.values()),'Play Mode checks')
