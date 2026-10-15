"""Finalize real ART-17 deliverables after visual inspection of all QA previews."""
from pathlib import Path
from io import BytesIO
import hashlib, json, subprocess, sys
from PIL import Image, ImageCms

ROOT = Path(__file__).resolve().parents[3]
TASK = 'ART-17'
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def write(p, d):
    dest = ROOT/p
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(d, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
def validate(args, dest):
    proc = subprocess.run([sys.executable, str(ROOT/'tools/art/validate_assets.py'), '--project', str(ROOT)]+args, cwd=ROOT, capture_output=True, encoding='utf-8')
    assert proc.returncode == 0, proc.stdout+proc.stderr
    report = json.loads(proc.stdout); write(dest, report); return report

m = read('docs/art/manifest.json')
index = read('docs/art/tasks.json')
p = read('docs/art/prompts/ART-17.json')
g = read('art-source/editable/ART-17/generations.json')
baseline = read('docs/art/qa/ART-17-scope-baseline.json')
assert p['status'] == 'gerado', 'Preserve finalized records.'
technical = validate(['--task', TASK], 'docs/art/qa/ART-17-technical.json')
plan = validate(['--plan'], 'docs/art/qa/ART-17-plan-validation.json')
assets = []
for r in p['assets']:
    a = next(a for a in m['assets'] if a['id'] == r['id'])
    request = next(a for a in g['assets'] if a['id'] == r['id'])
    assert r['prompt'] == request['prompt'] == a['generation_prompt']
    assert r['sha256'] == sha(r['output_path']) and r['source_sha256'] == sha(r['source_path'])
    for ref, digest in r['reference_sha256'].items(): assert sha(ref) == digest, ref
    with Image.open(ROOT/r['output_path']) as im:
        assert im.mode == 'RGBA' and im.size == (512,512)
        profile = ImageCms.getProfileDescription(ImageCms.ImageCmsProfile(BytesIO(im.info['icc_profile']))).strip()
        assert 'srgb' in profile.lower()
        alpha = im.getchannel('A')
        assert alpha.getextrema() == (0,255)
        bbox = alpha.point(lambda x:255 if x>=128 else 0).getbbox()
        assert min(bbox[0], bbox[1], 512-bbox[2], 512-bbox[3]) >= 51
    r.update({'status':'validado', 'qa_record':'docs/art/qa/ART-17.json'})
    a.update({'status':'validado','qa':{
        'technical':True, 'visual':True, 'identity':True, 'identity_applicable':False,
        'identity_note':'Sem personagens; conteúdo comparado com os sprites atuais de cada produto e desenho do hambúrguer consistente entre os três.',
        'record':'docs/art/qa/ART-17.json', 'preview':r['display_preview'], 'details':r['native_preview']}})
    assets.append({k:r[k] for k in ['id','name','output_path','actual_size_px','sha256','output_bytes','source_path','source_sha256','source_size_px','visual_composition_proposal','measurements','native_preview','display_preview']})

task = next(t for t in m['tasks'] if t['id'] == TASK)
task.update({'status':'validado','qa_record':'docs/art/qa/ART-17.json','production_record':'docs/art/prompts/ART-17.json','delivery_preview':'art-source/previews/ART-17/ART-17-contact-sheet.png'})
next(t for t in index if t['id'] == TASK).update({k:task[k] for k in ['status','qa_record','production_record','delivery_preview']})
p.update({'status':'validado','qa_record':'docs/art/qa/ART-17.json'})
qa = {
    'task_id':TASK, 'date':'2026-10-14', 'status':'validado',
    'reviewer':'Codex — inspeção visual e técnica',
    'scope_resolution':'O usuário selecionou explicitamente Executar ART-17: produtos de lanches após esclarecimento da diferença com ART-05. Controles do ART-05 preservados.',
    'production':{'tool':'image_gen.imagegen','model':None,'model_note':'Identificador de modelo não exposto pela ferramenta integrada.','distinct_generations':3,'transparent_background':True,'prompts_record':'docs/art/prompts/ART-17.json','technical_export_source':'art-source/editable/ART-17/export-art17.cjs'},
    'technical':{'passed':technical['passed'],'asset_count':3,'report':'docs/art/qa/ART-17-technical.json','plan_report':'docs/art/qa/ART-17-plan-validation.json','prerequisite_report':'docs/art/qa/ART-17-prerequisites.json'},
    'visual':{
        'passed':True,'backgrounds':['#F5F0E8','#24113D'],
        'native_size_px':[512,512], 'display_box_px':[250,250],
        'display_fit':'contain/preserveAspect, escala uniforme 250/512',
        'contact_sheet':task['delivery_preview'],
        'inspected_previews':[task['delivery_preview']]+[r['native_preview'] for r in p['assets']],
        'checks':[
            'Todos os três produtos inspecionados a 512×512 e 250×250 sobre fundos marfim e ameixa; silhuetas legíveis, contornos limpos e sem halo perceptível.',
            'Simples contém apenas o hambúrguer; especial acrescenta cesta de fritas; completo acrescenta refrigerante, brownie e ramequim de molho. Hierarquia de composição segue os sprites atuais do catálogo, sem inferir receita ou nível a partir do preço.',
            'Mesmo vocabulário de hambúrguer com pão de gergelim, carne, queijo, ovo, bacon, batata palha, alface, tomate e pepino, bandeja coral e papel marfim liso nos três produtos.',
            'Cartoon editorial adulto com contornos marrom/ameixa, cores naturais apetitosas, luz superior esquerda e acabamento coerente com as ilustrações de cardápio do concept aprovado.',
            'Bandejas, alça da cesta, pão e canudo completos; sem corte de silhueta. Margem visual mínima de 51 px no export, pivot central e sem 9-slice.',
            'Sem nome, preço, número, logo, rótulo, dado econômico, interface, fundo quadriculado, moldura ou sombra larga incorporados. Canudo do completo tem listras vermelhas e marfim sem marca ou texto.',
            'Cada produto foi gerado individualmente. Não houve sprite sheet ou recorte de card. A montagem de QA contém apenas cópias de exibição dos três PNGs finais.',
            'Originais RGBA 1254×1254 preservados como referência; redução uniforme do frame completo para 410×410 e padding transparente [51,51,51,51] até 512×512, sem crop, alteração de alfa, pintura ou deformação entre eixos.',
            'Exterior com alfa zero e antialias original preservado; contorno externo do canvas totalmente transparente. Miolo visualmente sólido sobre os dois fundos.'
        ],
        'alpha_note':'O alfa predominante do miolo retornado pelo ImageGen é 253, com pixels 252–255. O alfa original foi preservado na exportação, sem forçar opacidade por código. Resíduos esparsos de alfa baixo permanecem preservados e não são perceptíveis na inspeção.',
        'composition_note':'As composições são propostas visuais baseadas nos sprites atuais; os nomes genéricos não definem receitas completas. Nenhum dado de produto foi alterado.'
    },
    'assets':assets,'total_bytes':sum(a['output_bytes'] for a in assets),
    'integration_proposal':{'textureType':'Sprite (2D and UI)','spriteMode':'Single','pixelsPerUnit':100,'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'display_box_px':[250,250],'fit':'contain/preserveAspect','maxTextureSize':512,'sRGBTexture':True,'alphaIsTransparency':True,'filterMode':'Bilinear','generateMipmaps':False,'compression':'Sem compressão na QA; ajustar por plataforma na integração.'},
    'pending':[],'integration_performed':False
}

changed = [rel for rel,digest in baseline['protected_files'].items() if not (ROOT/rel).is_file() or sha(rel) != digest]
audit = {
    'task_id':TASK,
    'other_asset_records_unchanged':baseline['other_assets']==[a for a in m['assets'] if a['task_id']!=TASK],
    'other_task_records_unchanged':baseline['other_tasks']==[t for t in m['tasks'] if t['id']!=TASK],
    'other_tasks_index_unchanged':baseline['other_tasks_index']==[t for t in index if t['id']!=TASK],
    'manifest_other_fields_unchanged':baseline['manifest_other_fields']=={k:v for k,v in m.items() if k not in ['assets','tasks']},
    'protected_file_count':len(baseline['protected_files']),
    'protected_files_unchanged':not changed,'changed_protected_files':changed
}
audit['passed'] = all(audit[k] for k in ['other_asset_records_unchanged','other_task_records_unchanged','other_tasks_index_unchanged','manifest_other_fields_unchanged','protected_files_unchanged'])
assert audit['passed'], json.dumps(audit)
write('docs/art/manifest.json',m)
write('docs/art/tasks.json',index)
write('docs/art/prompts/ART-17.json',p)
write('docs/art/qa/ART-17.json',qa)
write('docs/art/qa/ART-17-measurements.json',{'task_id':TASK,'date':'2026-10-14','assets':assets,'total_bytes':qa['total_bytes']})
write('docs/art/qa/ART-17-scope-audit.json',audit)
print(json.dumps({'status':'validado','png_count':3,'total_bytes':qa['total_bytes'],'technical_passed':technical['passed'],'plan_passed':plan['passed'],'scope_audit_passed':audit['passed'],'protected_files':len(baseline['protected_files']),'pending':qa['pending']},ensure_ascii=False))
