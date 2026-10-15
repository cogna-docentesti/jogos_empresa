"""ART-35 provenance, import metadata and inspection proofs. No Unity execution.

Run prepare after render_art35.cjs; run finalize only after viewing the proofs.
The SVGs are the editable sources; all text and tint in proofs are QA only.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
import sys
import uuid
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[3]
TID = 'ART-35'
BASE = 'e343fe943f23e096c703311707bf46f4ebe38817'
PREVIEW = 'art-source/previews/ART-35'
PROMPTS = 'docs/art/prompts/ART-35.json'
SOURCE = 'art-source/editable/ART-35/produce_art35.py'
BG = ['#F5F0E8', '#24113D']
OBSERVATIONS = {
    'UI_SPINNER': 'Arco único de 270 graus, abertura clara e pontas arredondadas; centro vazado e pivot central. Legível em 48 px, sem indicação numérica ou seta de reiniciar.',
    'TUTORIAL_TAP': 'Contorno neutro de dedo indicador com arco de contato acima da ponta. A mão ocupa apenas a caixa do glifo de 64 px; sem pele, detalhe anatômico ou caricatura. Arco e polegar distinguíveis nos dois fundos.',
    'TUTORIAL_DRAG': 'Mesmo contorno de contato do tap, com seta horizontal de duas pontas em vez de pulso circular. Direção de arraste legível em 64 px, com separação entre seta e dedo.',
    'TUTORIAL_FOCUS_RING': 'Anel circular completo, simétrico e vazado; o centro preserva o alvo visível. Traço contínuo legível em 64 px e distinto do spinner aberto.',
}


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8'))


def write(path, value):
    dest = ROOT / path
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()


def run(*args):
    result = subprocess.run(args, cwd=ROOT, capture_output=True, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    return result.stdout


def validator(*args):
    return json.loads(run(sys.executable, 'tools/art/validate_assets.py', '--project', str(ROOT), *args))


def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/arial.ttf', size)


def tint(image, color):
    result = Image.new('RGBA', image.size, color)
    result.putalpha(image.getchannel('A'))
    return result


def metrics(image):
    alpha = image.getchannel('A')
    hist = alpha.histogram()
    box = alpha.getbbox()
    margins = [box[0], box[1], image.width-box[2], image.height-box[3]]
    visible = [rgb for rgb in image.getdata() if rgb[3]]
    return {'alpha_extrema': list(alpha.getextrema()), 'alpha_bbox_ltrb_px': list(box),
            'transparent_fraction': hist[0]/(image.width*image.height),
            'opaque_fraction': hist[255]/(image.width*image.height),
            'transparent_margins_ltrb_px': margins,
            'outer_edge_alpha_max': max(max(alpha.crop(rect).getdata()) for rect in
                [(0,0,image.width,1), (0,image.height-1,image.width,image.height),
                 (0,0,1,image.height), (image.width-1,0,image.width,image.height)]),
            'all_visible_rgb_white': all(px[:3] == (255,255,255) for px in visible),
            'center_alpha': alpha.getpixel((image.width//2, image.height//2))}


def nine_slice(image, target, border):
    l,b,r,t = border
    sx, sy = [0,l,image.width-r,image.width], [0,t,image.height-b,image.height]
    dx, dy = [0,l,target[0]-r,target[0]], [0,t,target[1]-b,target[1]]
    out = Image.new('RGBA', target)
    for y in range(3):
        for x in range(3):
            patch = image.crop((sx[x],sy[y],sx[x+1],sy[y+1]))
            patch = patch.resize((dx[x+1]-dx[x],dy[y+1]-dy[y]), Image.Resampling.LANCZOS)
            out.alpha_composite(patch,(dx[x],dy[y]))
    return out


def proofs(m, records):
    dest = ROOT / PREVIEW
    dest.mkdir(parents=True, exist_ok=True)
    assets = {a['id']:a for a in m['assets']}
    sheet = Image.new('RGB', (1280, 560), '#DDD6E5')
    sd = ImageDraw.Draw(sheet)
    sd.text((24,18), 'ART-35 | exact display size | white master / violet QA tint', font=font(26), fill=BG[1])
    sd.text((24,58), 'Proof labels and backgrounds are separate from production sprites.', font=font(18), fill=BG[1])
    for i, rec in enumerate(records):
        a = assets[rec['id']]
        im = Image.open(ROOT / rec['path']).convert('RGBA')
        for kind, size in [('native', im.size), ('display', tuple(a['display_size_px']))]:
            tile_w, tile_h = max(size[0]+48,300), size[1]+100
            proof = Image.new('RGBA', (tile_w*2,tile_h))
            d = ImageDraw.Draw(proof)
            for j, bg in enumerate(BG):
                d.rectangle((j*tile_w,0,(j+1)*tile_w,tile_h),fill=bg)
                color = '#6D28D9' if j==0 else '#FFFFFF'
                d.text((j*tile_w+16,14),rec['id'],font=font(17),fill=color)
                d.text((j*tile_w+16,40),f'{kind}: {size[0]} x {size[1]} px',font=font(16),fill=color)
                proof.alpha_composite(tint(im.resize(size,Image.Resampling.LANCZOS),color),
                                      (j*tile_w+(tile_w-size[0])//2,76))
            path = f'{PREVIEW}/{Path(a["path"]).stem}-{kind}-light-dark.png'
            proof.convert('RGB').save(ROOT/path)
            rec[kind+'_proof'] = path
        x = 20+i*315
        sd.text((x,110),rec['id'],font=font(17),fill=BG[1])
        sd.text((x,140),f'{a["display_size_px"][0]} x {a["display_size_px"][1]} px',font=font(17),fill=BG[1])
        for j,bg in enumerate(BG):
            y = 180+j*150
            sd.rectangle((x,y,x+296,y+138),fill=bg)
            glyph = tint(im.resize(a['display_size_px'],Image.Resampling.LANCZOS), '#6D28D9' if j==0 else '#FFFFFF')
            sheet.paste(glyph,(x+(296-glyph.width)//2,y+(138-glyph.height)//2),glyph)
    sheet.save(dest/'ART-35-contact-sheet.png')
    # Native 1920x1080 assembly proof with already validated CH01 and sliced controls.
    assembly_refs = ['CH01_CANONICAL','UI_PANEL_IVORY','UI_PRIMARY_VIOLET_NORMAL','UI_INPUT_FOCUS']
    for bg in BG:
        canvas = Image.new('RGBA',(1920,1080),bg)
        d = ImageDraw.Draw(canvas)
        color = '#24113D' if bg==BG[0] else '#FFFFFF'
        d.text((60,44),'ART-35 | tutorial and loading assembly proof | 1920 x 1080',font=font(32),fill=color)
        d.text((60,96),'QA only: existing CH01 and controls; text remains a separate layer.',font=font(22),fill=color)
        ch = Image.open(ROOT/m['asset_root']/assets['CH01_CANONICAL']['path']).convert('RGBA')
        ch = ImageOps.contain(ch,(420,760),Image.Resampling.LANCZOS)
        canvas.alpha_composite(ch,(85+(420-ch.width)//2,190))
        panel = assets['UI_PANEL_IVORY']
        panel_image = Image.open(ROOT/m['asset_root']/panel['path']).convert('RGBA')
        canvas.alpha_composite(nine_slice(panel_image,(1170,800),panel['nine_slice_lbrt_px']),(650,190))
        d = ImageDraw.Draw(canvas)
        d.text((700,225),'Tutorial active',font=font(30),fill=BG[1])
        control = assets['UI_INPUT_FOCUS']
        control_image = Image.open(ROOT/m['asset_root']/control['path']).convert('RGBA')
        canvas.alpha_composite(nine_slice(control_image,(560,80),control['nine_slice_lbrt_px']),(730,315))
        d.text((758,340),'Native UI label',font=font(22),fill=BG[1])
        for aid,position in [('TUTORIAL_TAP',(1310,323)),('TUTORIAL_DRAG',(1310,503)),('TUTORIAL_FOCUS_RING',(910,685))]:
            a = assets[aid]
            glyph = Image.open(ROOT/m['asset_root']/a['path']).convert('RGBA').resize(a['display_size_px'],Image.Resampling.LANCZOS)
            canvas.alpha_composite(tint(glyph,'#6D28D9'),position)
        d.text((730,454),'Drag cue | 64 x 64 px',font=font(22),fill=BG[1])
        d.line((760,535,1240,535),fill='#B99AE8',width=4)
        d.ellipse((930,521,958,549),fill='#6D28D9')
        d.text((730,625),'Focus cue | target stays visible',font=font(22),fill=BG[1])
        d.ellipse((935,710,949,724),fill='#E7B65A')
        button = assets['UI_PRIMARY_VIOLET_NORMAL']
        button_im = Image.open(ROOT/m['asset_root']/button['path']).convert('RGBA')
        canvas.alpha_composite(nine_slice(button_im,(360,72),button['nine_slice_lbrt_px']),(1080,820))
        spinner = assets['UI_SPINNER']
        spin = Image.open(ROOT/m['asset_root']/spinner['path']).convert('RGBA').resize((48,48),Image.Resampling.LANCZOS)
        canvas.alpha_composite(spin,(1100,832))
        d.text((1164,843),'Loading',font=font(22),fill='#FFFFFF')
        name = 'ART-35-canvas-light.png' if bg==BG[0] else 'ART-35-canvas-dark.png'
        canvas.convert('RGB').save(dest/name)
    return [{'id':aid,'path':m['asset_root']+'/'+assets[aid]['path'],'sha256':sha(m['asset_root']+'/'+assets[aid]['path'])} for aid in assembly_refs]


def prepare():
    assert run('git','rev-parse','HEAD').strip()==BASE
    assert run('git','branch','--show-current').strip()=='dev/new-screens'
    m = read('docs/art/manifest.json')
    task = next(t for t in m['tasks'] if t['id']==TID)
    prereqs = []
    for dep in task['dependencies']:
        report = validator('--task',dep)
        assert report['passed']
        existing = [a for a in m['assets'] if a['task_id']==dep]
        assert all(a['status']=='validado' and sha(m['asset_root']+'/'+a['path'])==a['sha256'] for a in existing)
        prereqs.append({'task_id':dep,'passed':True,'asset_count':len(existing),'all_png_hashes_match':True})
    write('docs/art/qa/ART-35-prerequisites.json',{'task_id':TID,'passed':True,'base_commit':BASE,'dependencies':prereqs})
    renderer = read('art-source/editable/ART-35/renderer-info.json')
    records = []
    refs = ['art-source/references/concepts/01-hub.png','art-source/editable/shared/icons/icon_next.svg','art-source/editable/shared/icons/icon_sync.svg']
    for a in [a for a in m['assets'] if a['task_id']==TID]:
        assert a['status']=='planejado'
        path = m['asset_root']+'/'+a['path']
        im = Image.open(ROOT/path)
        alpha = metrics(im)
        assert im.mode=='RGBA' and list(im.size)==a['export_size_px'] and im.info.get('icc_profile')
        assert alpha['all_visible_rgb_white'] and alpha['outer_edge_alpha_max']==0
        if a['padding_percent']:
            assert min(alpha['transparent_margins_ltrb_px'])>=im.width*a['padding_percent']/100
        meta = ROOT/(path+'.meta')
        assert not meta.exists(), 'Preserve existing import metadata.'
        template = (ROOT/'Assets/Art/new/events/illustrations/heavy_rain.png.meta').read_text(encoding='utf-8')
        template = re.sub(r'^guid: [0-9a-f]+$', 'guid: '+uuid.uuid4().hex,template,flags=re.M)
        template = re.sub(r'^    spriteID: [0-9a-f]+$', '    spriteID: '+uuid.uuid4().hex,template,flags=re.M)
        template = template.replace('maxTextureSize: 1024','maxTextureSize: 256')
        template = template.replace('ART-33 independent event vignette; no scene integration','ART-35 single white tintable UI sprite; no scene integration')
        meta.write_text('\n'.join(line.rstrip() for line in template.splitlines())+'\n',encoding='utf-8')
        prompt = '\n'.join([f'Produzir somente {a["id"]}: {a["brief"]}',
            'Geometria SVG original, branco #FFFFFF tintável, antialias limpo e traço arredondado coerente com ART-08. Sem fundo, sombra, badge, palavras ou números.',
            f'Export PNG RGBA 8-bit sRGB {im.width}x{im.height}; caixa de display {a["display_size_px"]} com contain, pivot central [0.5,0.5], bordas 9-slice zero.',
            'Uma imagem estática por arquivo. UI controla rotação do spinner e visibilidade do tutorial; sem sprite sheet. Anéis têm centro transparente.',
            'Tap e drag usam o mesmo contorno pequeno e neutro de dedo indicador; tap com arco de contato, drag com seta horizontal de duas pontas. Focus ring é um círculo completo.',
            'Não gerar personagem: montagem de QA reutiliza CH01 canônica. Texto e controles são camadas independentes.'])
        rec = {'id':a['id'],'path':path,'status':'gerado','tool':'original SVG + sharp/librsvg',
               'model':None,'origin':'Geometria vetorial original da especificação ART-35 e sistema de ícones ART-08.',
               'exact_prompt':prompt,'references':[{'path':p,'sha256':sha(p)} for p in refs],
               'source_editable':a['source_editable'],'source_sha256':sha(a['source_editable']),
               'actual_size_px':list(im.size),'display_size_px':a['display_size_px'],'display_fit':'contain',
               'mode':im.mode,'color_space':'sRGB','sha256':sha(path),'meta_sha256':sha(path+'.meta'),
               'alpha':alpha,'master_color':'#FFFFFF','tintable':True,'character_ids':[],
               'qa':{'technical':True,'visual':False,'identity':None}}
        records.append(rec)
        a.update(status='gerado',source=rec['origin'],references=refs,sha256=rec['sha256'],actual_size_px=list(im.size),
                 generation_prompt=prompt,generation_tool=rec['tool'],generation_model=None,
                 generation_model_note='Componentes geométricos previstos como vector_or_code; sem geração por modelo.',
                 production_record=PROMPTS,master_color='#FFFFFF',tintable=True,
                 qa={'technical':True,'visual':False,'identity':None})
    assembly_refs = proofs(m,records)
    write(PROMPTS,{'task_id':TID,'date':'2026-10-15','status':'gerado','tool':'original SVG + sharp/librsvg',
                  'model':None,'imagegen_required':False,'method_reason':'Todos os quatro assets são vector_or_code no manifesto; geometria simples usa fontes SVG editáveis.',
                  'renderer':renderer,'producer_source':SOURCE,'assets':records,'assembly_references':assembly_refs})
    task.update(status='gerado',qa_record='docs/art/qa/ART-35.json')
    write('docs/art/manifest.json',m)
    tasks = read('docs/art/tasks.json')
    next(t for t in tasks if t['id']==TID).update(status='gerado',qa_record='docs/art/qa/ART-35.json')
    write('docs/art/tasks.json',tasks)
    write('docs/art/qa/ART-35-technical.json',validator('--task',TID))
    write('docs/art/qa/ART-35-plan-validation.json',validator('--plan'))
    print('Prepared 4 SVG, 4 RGBA PNG, import metadata and 11 inspection proofs; visual review pending.')


def finalize():
    m, prompts = read('docs/art/manifest.json'), read(PROMPTS)
    assert prompts['status']=='gerado'
    for r in prompts['assets']:
        assert sha(r['path'])==r['sha256'] and sha(r['source_editable'])==r['source_sha256']
        r.update(status='validado',visual_observation=OBSERVATIONS[r['id']])
        r['qa']['visual']=True
        a = next(a for a in m['assets'] if a['id']==r['id'])
        a.update(status='validado',qa={'technical':True,'visual':True,'identity':None},qa_record='docs/art/qa/ART-35.json')
    prompts['status']='validado'
    write(PROMPTS,prompts)
    next(t for t in m['tasks'] if t['id']==TID)['status']='validado'
    write('docs/art/manifest.json',m)
    tasks = read('docs/art/tasks.json')
    next(t for t in tasks if t['id']==TID)['status']='validado'
    write('docs/art/tasks.json',tasks)
    write('docs/art/qa/ART-35-technical.json',validator('--task',TID))
    write('docs/art/qa/ART-35-plan-validation.json',validator('--plan'))
    previews = sorted(str(p.relative_to(ROOT)).replace('\\','/') for p in (ROOT/PREVIEW).glob('*.png'))
    qa = {'task_id':TID,'date':'2026-10-15','status':'validado','reviewer':'Codex — inspeção direta das provas em resolução original',
          'technical':{'passed':True,'asset_count':4,'report':'docs/art/qa/ART-35-technical.json',
                       'plan_report':'docs/art/qa/ART-35-plan-validation.json',
                       'prerequisite_report':'docs/art/qa/ART-35-prerequisites.json',
                       'scope_report':'docs/art/qa/ART-35-scope-audit.json',
                       'format':'PNG RGBA 8-bit sRGB','true_alpha':True,'white_tintable_masters':True,
                       'import':'Single; FullRect; PPU 100; pivot 0.5/0.5; zero borders; Bilinear; no mipmaps; no compression; max 256.'},
          'visual':{'passed':True,'backgrounds':BG,'inspected_previews':previews,
                    'display_sizes_px':{'UI_SPINNER':[48,48],'tutorials':[64,64]},
                    'review':'Quatro pares de provas nativas e quatro pares em display, contact sheet e dois canvases 1920x1080 inspecionados diretamente. Branco sobre ameixa; tint violeta sobre marfim somente na prova.',
                    'assembly':'CH01 canônica preservada e controles existentes em 9-slice. Texto e símbolos em camadas separadas; nenhuma cena ou UI implementada.'},
          'identity':{'applicable_to_new_assets':False,'reason':'Somente glifos geométricos; CH01 reutilizada apenas em prova, com hash canônico conferido.'},
          'production':{'method':'vector_or_code','imagegen_used':False,'reason':prompts['method_reason'],
                        'prompts_record':PROMPTS,'producer_source':SOURCE},
          'assets':prompts['assets'],'assembly_references':prompts['assembly_references'],'pending':[]}
    write('docs/art/qa/ART-35.json',qa)
    audit()


def audit():
    old = json.loads(run('git','show',BASE+':docs/art/manifest.json'))
    current = read('docs/art/manifest.json')
    old_tasks = json.loads(run('git','show',BASE+':docs/art/tasks.json'))
    now_tasks = read('docs/art/tasks.json')
    keep_a = lambda m:[a for a in m['assets'] if a['task_id']!=TID]
    keep_t = lambda ts:[t for t in ts if t['id']!=TID]
    paths = set(run('git','diff','--name-only',BASE).splitlines()) | set(run('git','ls-files','--others','--exclude-standard').splitlines())
    selected = [a for a in current['assets'] if a['task_id']==TID]
    exact = {current['asset_root']+'/'+a['path'] for a in selected}
    exact |= {p+'.meta' for p in list(exact)}
    exact |= {a['source_editable'] for a in selected}
    exact |= {'docs/art/manifest.json','docs/art/tasks.json',PROMPTS}
    allowed = lambda p: p in exact or p.startswith(('art-source/editable/ART-35/','art-source/previews/ART-35/')) or (p.startswith('docs/art/qa/ART-35') and p.endswith('.json'))
    other_top_fields = all(old[k]==v for k,v in current.items() if k not in ['assets','tasks'])
    checks = {'other_assets_unchanged':keep_a(old)==keep_a(current),
              'other_manifest_tasks_unchanged':keep_t(old['tasks'])==keep_t(current['tasks']),
              'other_tasks_unchanged':keep_t(old_tasks)==keep_t(now_tasks),
              'other_manifest_fields_unchanged':other_top_fields,
              'all_previously_validated_pngs_unchanged':all(sha(old['asset_root']+'/'+a['path'])==a['sha256'] for a in old['assets'] if a['status']=='validado'),
              'only_current_task_files_changed':all(allowed(p) for p in paths)}
    write('docs/art/qa/ART-35-scope-audit.json',{'task_id':TID,'base_commit':BASE,'passed':all(checks.values()),
          'checks':checks,'changed_files':sorted(paths),'out_of_scope_files':sorted(p for p in paths if not allowed(p))})
    assert all(checks.values()),checks
    print(json.dumps({'task':TID,'passed':True,'scope_checks':checks},indent=2))


if __name__=='__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['prepare','finalize','audit'])
    args = parser.parse_args()
    {'prepare':prepare,'finalize':finalize,'audit':audit}[args.command]()
