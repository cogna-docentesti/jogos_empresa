"""ART-37 provenance and QA proofs. Finalize only after direct visual inspection.

Run render_art37.cjs first. No Unity, scenes, gameplay or previous assets are edited.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import uuid
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
TID = 'ART-37'
BASE = '3736db7db7808c6cf0aa80906db6580584ed6aba'
TOKEN = os.environ.get('COGNA_ART_TOKEN')
PREVIEW = 'art-source/previews/ART-37'
PROMPTS = 'docs/art/prompts/ART-37.json'
SOURCE = 'art-source/editable/ART-37/produce_art37.py'
BACKGROUNDS = ['#F5F0E8', '#24113D']
SEMANTICS = {
    'ICON_PARTNERS': ('Parceiros', 'Aperto de mãos geométrico com dois punhos, representando acordo entre parceiros; não repetir equipe ou elos de coerência.'),
    'ICON_VALUE_PROPOSITION': ('Proposta de valor', 'Diamante facetado, representando valor oferecido; silhueta distinta da estrela de pontuação e do polegar de reputação.'),
    'ICON_CUSTOMER_RELATIONSHIP': ('Relacionamento com clientes', 'Balão de conversa com coração sólido, representando cuidado e vínculo com o cliente; não repetir pessoa ou grupo de clientes.'),
    'ICON_SALES_CHANNELS': ('Canais de venda', 'Um ponto de origem ligado a três saídas, representando distribuição por canais; sem copiar carrinho ou loja existentes.'),
}
# Coverage recommendations only; the future native UI owns labels, content and period.
REUSE = {
    'key_activities': 'ICON_EQUIPMENT',
    'key_resources': 'ICON_INVENTORY',
    'customer_segments': 'ICON_CLIENTS',
    'cost_structure': 'ICON_INPUT_COSTS',
    'revenue_streams': 'ICON_REVENUE',
}
REFS = ['art-source/references/concepts/01-hub.png',
        'art-source/editable/shared/icons/icon_team.svg',
        'art-source/editable/shared/icons/icon_coherence.svg',
        'art-source/editable/shared/icons/icon_cart.svg',
        'Assets/Art/new/shared/icons/icon_star.png',
        'Assets/Art/new/shared/icons/icon_clients.png']


def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8'))


def write(path, data):
    target = ROOT/path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()


def run(*args):
    result = subprocess.run(args, cwd=ROOT, capture_output=True, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(result.stdout+result.stderr)
    return result.stdout


def validator(*args):
    return json.loads(run(sys.executable, 'tools/art/validate_assets.py', '--project', str(ROOT), *args))


def owner():
    state = json.loads(Path('C:/Users/Pichau/.codex/art-queues/cogna/state.json').read_text(encoding='utf-8'))
    assert TOKEN and state['phase']=='running' and state['active']['task']==TID and state['active']['token']==TOKEN
    assert state['active']['base_commit']==BASE and run('git','rev-parse','HEAD').strip()==BASE
    assert run('git','branch','--show-current').strip()==state['branch']


def metrics(im):
    alpha = im.getchannel('A')
    hist = alpha.histogram()
    box = alpha.getbbox()
    return {'alpha_extrema':list(alpha.getextrema()), 'alpha_bbox_ltrb_px':list(box),
            'transparent_fraction':hist[0]/(im.width*im.height),
            'opaque_fraction':hist[255]/(im.width*im.height),
            'transparent_margins_ltrb_px':[box[0],box[1],im.width-box[2],im.height-box[3]],
            'all_visible_rgb_white':all(px[:3]==(255,255,255) for px in im.getdata() if px[3]),
            'outer_edge_alpha_max':max(max(alpha.crop(rect).getdata()) for rect in
                [(0,0,256,1),(0,255,256,256),(0,0,1,256),(255,0,256,256)]),
            'alpha_sha256':hashlib.sha256(alpha.tobytes()).hexdigest()}


def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/arial.ttf',size)


def tint(im, color):
    out = Image.new('RGBA', im.size, color)
    out.putalpha(im.getchannel('A'))
    return out


def proofs(m, records):
    dest = ROOT/PREVIEW
    dest.mkdir(parents=True, exist_ok=True)
    sheet = Image.new('RGB',(1920,1080),'#DDD6E5')
    draw = ImageDraw.Draw(sheet)
    draw.text((32,18),'ART-37 | símbolos BMC | prova de QA 1920 x 1080',font=font(30),fill=BACKGROUNDS[1])
    draw.text((32,61),'Masters 256 px + leitura 32 / 40 / 48 px. Tintas, fundos e rótulos pertencem apenas à prova.',font=font(22),fill=BACKGROUNDS[1])
    for i, rec in enumerate(records):
        im = Image.open(ROOT/rec['path']).convert('RGBA')
        page = Image.new('RGBA',(960,480))
        d = ImageDraw.Draw(page)
        for j, bg in enumerate(BACKGROUNDS):
            x = j*480
            color = '#6D28D9' if j==0 else '#FFFFFF'
            d.rectangle((x,0,x+479,479),fill=bg)
            d.text((x+20,14),SEMANTICS[rec['id']][0],font=font(23),fill=color)
            d.text((x+20,46),'PNG nativo: 256 x 256 px',font=font(18),fill=color)
            page.alpha_composite(tint(im,color),(x+112,76))
            for size,cx in [(32,90),(40,232),(48,374)]:
                glyph = tint(im.resize((size,size),Image.Resampling.LANCZOS),color)
                page.alpha_composite(glyph,(x+cx-size//2,360+(48-size)//2))
                d.text((x+cx-22,422),f'{size} px',font=font(18),fill=color)
        proof = f'{PREVIEW}/{Path(rec["path"]).stem}-native-and-display.png'
        page.convert('RGB').save(ROOT/proof)
        rec['inspection_proof'] = proof
        sheet.paste(page.convert('RGB'),((i%2)*960,108+(i//2)*480))
    sheet.save(dest/'ART-37-contact-sheet.png')
    # Reference comparison is an asset inventory, not a proposed BMC screen.
    ids = [r['id'] for r in records]+list(REUSE.values())
    index = {a['id']:a for a in m['assets']}
    comparison = Image.new('RGBA',(1920,1080))
    d = ImageDraw.Draw(comparison)
    for j,bg in enumerate(BACKGROUNDS):
        x = j*960
        color = '#6D28D9' if j==0 else '#FFFFFF'
        d.rectangle((x,0,x+959,1079),fill=bg)
        d.text((x+36,28),'ART-37 | inventário de símbolos | QA',font=font(30),fill=color)
        d.text((x+36,78),'40 x 40 px; os últimos cinco reutilizam o kit.',font=font(22),fill=color)
        for i,aid in enumerate(ids):
            a = index[aid]
            im = Image.open(ROOT/m['asset_root']/a['path']).convert('RGBA').resize((40,40),Image.Resampling.LANCZOS)
            y = 160+i*92
            comparison.alpha_composite(tint(im,color),(x+56,y))
            d.text((x+128,y+8),aid,font=font(23),fill=color)
    comparison.convert('RGB').save(dest/'ART-37-kit-comparison.png')


def prepare():
    owner()
    m = read('docs/art/manifest.json')
    task = next(t for t in m['tasks'] if t['id']==TID)
    assert task['status']=='planejado'
    deps = []
    for tid in task['dependencies']:
        report = validator('--task',tid)
        assets = [a for a in m['assets'] if a['task_id']==tid]
        assert report['passed'] and all(a['status']=='validado' and sha(m['asset_root']+'/'+a['path'])==a['sha256'] for a in assets)
        deps.append({'task_id':tid,'asset_count':len(assets),'passed':True,'all_png_hashes_match':True})
    write('docs/art/qa/ART-37-prerequisites.json',{'task_id':TID,'base_commit':BASE,'passed':True,'dependencies':deps})
    records = []
    for a in [a for a in m['assets'] if a['task_id']==TID]:
        assert a['status']=='planejado' and a['production_method']=='vector_or_code'
        path = m['asset_root']+'/'+a['path']
        im = Image.open(ROOT/path)
        measure = metrics(im)
        assert im.mode=='RGBA' and list(im.size)==a['export_size_px']==[256,256] and im.info.get('icc_profile')
        assert measure['all_visible_rgb_white'] and measure['outer_edge_alpha_max']==0
        assert min(measure['transparent_margins_ltrb_px'])>=256*a['padding_percent']/100
        svg = (ROOT/a['source_editable']).read_text(encoding='utf-8')
        assert not any(s in svg.lower() for s in ['<text','<image','<filter'])
        meta = ROOT/(path+'.meta')
        assert not meta.exists(), 'Preserve any existing importer.'
        template = (ROOT/'Assets/Art/new/system/loading/spinner.png.meta').read_text(encoding='utf-8')
        template = re.sub(r'^guid: [0-9a-f]+$','guid: '+uuid.uuid4().hex,template,flags=re.M)
        template = re.sub(r'^    spriteID: [0-9a-f]+$','    spriteID: '+uuid.uuid4().hex,template,flags=re.M)
        template = template.replace('ART-35 single white tintable UI sprite; no scene integration','ART-37 single white tintable BMC symbol; no scene integration')
        meta.write_text('\n'.join(s.rstrip() for s in template.splitlines())+'\n',encoding='utf-8')
        prompt = '\n'.join([f'Produzir somente {a["id"]}: {a["brief"]}', SEMANTICS[a['id']][1],
            'SVG original, branco #FFFFFF tintável, traço-base 16 px com terminais e junções arredondados coerentes com ART-06/07. Sem fundo, badge, sombra, label ou logo.',
            'PNG RGBA 8-bit sRGB 256x256; display 40x40 contain; conferência adicional 32/48 px. Pivot [0.5,0.5], bordas 9-slice zero, margem transparente mínima de 10%.',
            'Um símbolo por arquivo. Cinco blocos do BMC reutilizam o kit. BMC, balanço e fluxo de caixa mantêm layout, texto e dados nativos; período aceita 3 ou 12 meses.',
            'Não produzir tela, screenshot de relatório, números, gráfico, personagem, cenário ou UI.'])
        rec = {'id':a['id'],'path':path,'status':'gerado','tool':'original SVG + sharp/librsvg','model':None,
               'origin':'Geometria vetorial original da especificação ART-37 e linguagem de ícones ART-06/07.',
               'exact_prompt':prompt,'references':[{'path':p,'sha256':sha(p)} for p in REFS],
               'source_editable':a['source_editable'],'source_sha256':sha(a['source_editable']),
               'actual_size_px':[256,256],'display_size_px':[40,40],'display_fit':'contain','pivot':a['pivot'],
               'nine_slice_lbrt_px':a['nine_slice_lbrt_px'],'mode':'RGBA','color_space':'sRGB',
               'master_color':'#FFFFFF','tintable':True,'line_weight_master_px':16,
               'sha256':sha(path),'meta_sha256':sha(path+'.meta'),'alpha':measure,
               'character_ids':[],'qa':{'technical':True,'visual':False,'identity':None}}
        records.append(rec)
        a.update(status='gerado',source=rec['origin'],references=REFS,sha256=rec['sha256'],actual_size_px=[256,256],
                 generation_prompt=prompt,generation_tool=rec['tool'],generation_model=None,
                 generation_model_note='Ícones simples vector_or_code; geometria original em SVG sem modelo generativo.',
                 production_record=PROMPTS,master_color='#FFFFFF',tintable=True,qa=rec['qa'].copy())
    assert len(set(r['alpha']['alpha_sha256'] for r in records))==4
    prior_alpha = {hashlib.sha256(Image.open(ROOT/m['asset_root']/a['path']).getchannel('A').tobytes()).hexdigest()
                   for a in m['assets'] if a['task_id'] in ['ART-06','ART-07']}
    assert all(r['alpha']['alpha_sha256'] not in prior_alpha for r in records)
    proofs(m,records)
    index = {a['id']:a for a in m['assets']}
    reuse = [{'bmc_block':block,'existing_asset_id':aid,'path':m['asset_root']+'/'+index[aid]['path'],
              'sha256':sha(m['asset_root']+'/'+index[aid]['path']),'new_asset_created':False}
             for block,aid in REUSE.items()]
    write('docs/art/qa/ART-37-bmc-coverage.json',{'task_id':TID,'new_symbols':[r['id'] for r in records],
          'five_reused_symbols':reuse,'mapping_status':'Recomendação documental; sem modelo/tela final implementados.',
          'native_ui_requirements':['Rótulos e conteúdo dos nove blocos BMC permanecem dinâmicos.',
              'Balanço e fluxo de caixa usam layout, texto e dados; nenhum novo bitmap.',
              'Período deve aceitar 3 ou 12 meses; não rasterizar contagem ou valores.'],
          'scene_or_gameplay_changes':False})
    write(PROMPTS,{'task_id':TID,'date':'2026-10-15','status':'gerado','tool':'original SVG + sharp/librsvg',
                  'model':None,'imagegen_required':False,'imagegen_used':False,
                  'method_reason':'Os quatro ícones são vector_or_code no manifesto; SVG original mantém precisão e consistência do kit.',
                  'producer_source':SOURCE,'renderer':read('art-source/editable/ART-37/renderer-info.json'),'assets':records})
    task.update(status='gerado',qa_record='docs/art/qa/ART-37.json')
    write('docs/art/manifest.json',m)
    tasks = read('docs/art/tasks.json')
    next(t for t in tasks if t['id']==TID).update(status='gerado',qa_record='docs/art/qa/ART-37.json')
    write('docs/art/tasks.json',tasks)
    write('docs/art/qa/ART-37-technical.json',validator('--task',TID))
    write('docs/art/qa/ART-37-plan-validation.json',validator('--plan'))
    print('Prepared four SVGs, four PNGs/importers, four review pages and two 1920x1080 QA sheets; visual review pending.')


def refresh():
    owner()
    m, prompts = read('docs/art/manifest.json'), read(PROMPTS)
    assert prompts['status']=='gerado'
    for r in prompts['assets']:
        a = next(a for a in m['assets'] if a['id']==r['id'])
        assert a['status']=='gerado' and sha(r['path']+'.meta')==r['meta_sha256']
        im = Image.open(ROOT/r['path'])
        measure = metrics(im)
        assert im.mode=='RGBA' and im.size==(256,256) and measure['all_visible_rgb_white']
        assert min(measure['transparent_margins_ltrb_px'])>=25.6
        r.update(sha256=sha(r['path']),source_sha256=sha(r['source_editable']),alpha=measure)
        a['sha256']=r['sha256']
    prompts['revisions']=['Após inspeção visual, saídas de ICON_SALES_CHANNELS reduzidas para separar os três terminais. Fontes e provas atualizadas antes da validação final.']
    proofs(m,prompts['assets'])
    write(PROMPTS,prompts)
    write('docs/art/manifest.json',m)
    write('docs/art/qa/ART-37-technical.json',validator('--task',TID))
    print('Refreshed own unvalidated glyphs and proofs; visual review still pending.')


def finalize():
    owner()
    m, prompts = read('docs/art/manifest.json'), read(PROMPTS)
    assert prompts['status']=='gerado'
    notes = read('docs/art/qa/ART-37-visual-review.json')
    assert notes['passed'] and set(notes['assets'])==set(SEMANTICS)
    previews = sorted(str(p.relative_to(ROOT)).replace('\\','/') for p in (ROOT/PREVIEW).glob('*.png'))
    assert set(notes['inspected_previews'])==set(previews)
    for r in prompts['assets']:
        assert sha(r['path'])==r['sha256'] and sha(r['source_editable'])==r['source_sha256'] and sha(r['path']+'.meta')==r['meta_sha256']
        r.update(status='validado',visual_observation=notes['assets'][r['id']])
        r['qa']['visual']=True
        next(a for a in m['assets'] if a['id']==r['id']).update(status='validado',qa=r['qa'].copy(),qa_record='docs/art/qa/ART-37.json')
    prompts['status']='validado'
    write(PROMPTS,prompts)
    next(t for t in m['tasks'] if t['id']==TID)['status']='validado'
    write('docs/art/manifest.json',m)
    tasks = read('docs/art/tasks.json')
    next(t for t in tasks if t['id']==TID)['status']='validado'
    write('docs/art/tasks.json',tasks)
    technical, plan = validator('--task',TID), validator('--plan')
    assert technical['passed'] and plan['passed']
    write('docs/art/qa/ART-37-technical.json',technical)
    write('docs/art/qa/ART-37-plan-validation.json',plan)
    write('docs/art/qa/ART-37.json',{'task_id':TID,'date':'2026-10-15','status':'validado',
          'reviewer':'Codex — inspeção visual direta das seis provas em resolução original',
          'technical':{'passed':True,'asset_count':4,'report':'docs/art/qa/ART-37-technical.json',
              'plan_report':'docs/art/qa/ART-37-plan-validation.json','prerequisites':'docs/art/qa/ART-37-prerequisites.json',
              'scope_report':'docs/art/qa/ART-37-scope-audit.json','format':'PNG RGBA 8-bit sRGB',
              'true_alpha':True,'white_tintable_masters':True,'unique_alpha_masks':True,
              'import':'Single; FullRect; PPU 100; central pivot; zero borders; Bilinear; no mipmaps/compression; max 256.'},
          'visual':notes,'identity':{'applicable':False,'reason':'Somente símbolos geométricos; nenhum personagem.'},
          'production':{'method':'vector_or_code','imagegen_used':False,'prompts':PROMPTS,'source':SOURCE},
          'assets':prompts['assets'],'documentary_coverage':'docs/art/qa/ART-37-bmc-coverage.json',
          'pending':['Integração e modelo final da tela documental BMC/relatórios ficam para etapa posterior.',
              'Pendências de importadores e referências serializadas do ART-36 permanecem fora deste lote.']})
    audit()


def audit():
    old = json.loads(run('git','show',BASE+':docs/art/manifest.json'))
    m = read('docs/art/manifest.json')
    old_tasks = json.loads(run('git','show',BASE+':docs/art/tasks.json'))
    keep_a = lambda v:[a for a in v['assets'] if a['task_id']!=TID]
    keep_t = lambda v:[t for t in v if t['id']!=TID]
    paths = set(run('git','diff','--name-only',BASE).splitlines()) | set(run('git','ls-files','--others','--exclude-standard').splitlines())
    selected = [a for a in m['assets'] if a['task_id']==TID]
    exact = {m['asset_root']+'/'+a['path'] for a in selected}
    exact |= {p+'.meta' for p in list(exact)} | {a['source_editable'] for a in selected}
    exact |= {'docs/art/manifest.json','docs/art/tasks.json',PROMPTS}
    allowed = lambda p: p in exact or p.startswith(('art-source/editable/ART-37/','art-source/previews/ART-37/')) or (p.startswith('docs/art/qa/ART-37') and p.endswith('.json'))
    checks = {'other_asset_records_unchanged':keep_a(old)==keep_a(m),
              'other_manifest_tasks_unchanged':keep_t(old['tasks'])==keep_t(m['tasks']),
              'other_tasks_unchanged':keep_t(old_tasks)==keep_t(read('docs/art/tasks.json')),
              'other_manifest_fields_unchanged':all(old[k]==v for k,v in m.items() if k not in ['assets','tasks']),
              'all_265_previously_validated_pngs_unchanged':all(sha(old['asset_root']+'/'+a['path'])==a['sha256'] for a in old['assets'] if a['status']=='validado'),
              'only_current_task_files_changed':all(allowed(p) for p in paths)}
    write('docs/art/qa/ART-37-scope-audit.json',{'task_id':TID,'base_commit':BASE,'passed':all(checks.values()),
          'checks':checks,'changed_files':sorted(paths),'out_of_scope_files':sorted(p for p in paths if not allowed(p))})
    assert all(checks.values()),checks
    print(json.dumps({'task':TID,'passed':True,'checks':checks},indent=2))


if __name__=='__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['prepare','refresh','finalize','audit'])
    args = parser.parse_args()
    {'prepare':prepare,'refresh':refresh,'finalize':finalize,'audit':audit}[args.command]()
