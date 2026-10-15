"""Finalize the three real Japanese product sprites after visual review."""
from pathlib import Path
from io import BytesIO
import hashlib, json, subprocess, sys
from PIL import Image, ImageCms

ROOT=Path(__file__).resolve().parents[3]
TASK='ART-18'
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def write(p,d):
    f=ROOT/p;f.parent.mkdir(parents=True,exist_ok=True)
    f.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def validate(args,dest):
    proc=subprocess.run([sys.executable,'-B',str(ROOT/'tools/art/validate_assets.py'),'--project',str(ROOT)]+args,cwd=ROOT,capture_output=True,encoding='utf-8')
    assert proc.returncode==0,proc.stdout+proc.stderr
    result=json.loads(proc.stdout);write(dest,result);return result

m=read('docs/art/manifest.json');index=read('docs/art/tasks.json')
p=read('docs/art/prompts/ART-18.json');g=read('art-source/editable/ART-18/generations.json')
baseline=read('docs/art/qa/ART-18-scope-baseline.json')
assert p['status']=='gerado','Preserve finalized records.'
technical=validate(['--task',TASK],'docs/art/qa/ART-18-technical.json')
plan=validate(['--plan'],'docs/art/qa/ART-18-plan-validation.json')
assets=[]
for r in p['assets']:
    a=next(a for a in m['assets'] if a['id']==r['id'])
    req=next(a for a in g['assets'] if a['id']==r['id'])
    assert req['prompt']==r['prompt']==a['generation_prompt']
    assert sha(r['output_path'])==r['sha256'] and sha(r['source_path'])==r['source_sha256']
    for ref,digest in r['reference_sha256'].items(): assert sha(ref)==digest,ref
    with Image.open(ROOT/r['output_path']) as im:
        assert im.mode=='RGBA' and im.size==(512,512)
        profile=ImageCms.getProfileDescription(ImageCms.ImageCmsProfile(BytesIO(im.info['icc_profile']))).strip()
        assert 'srgb' in profile.lower()
        alpha=im.getchannel('A');assert alpha.getextrema()==(0,255)
        box=alpha.point(lambda x:255 if x>=128 else 0).getbbox()
        assert min(box[0],box[1],512-box[2],512-box[3])>=51
    r.update({'status':'validado','qa_record':'docs/art/qa/ART-18.json'})
    a.update({'status':'validado','qa':{'technical':True,'visual':True,'identity':True,'identity_applicable':False,
        'identity_note':'Sem personagens; motivos de comida comparados com os sprites atuais e acabamento consistente entre os três produtos.',
        'record':'docs/art/qa/ART-18.json','preview':r['display_preview'],'details':r['native_preview']}})
    assets.append({k:r[k] for k in ['id','name','output_path','actual_size_px','sha256','output_bytes','source_path','source_sha256','source_size_px','visual_composition_proposal','measurements','native_preview','display_preview']})

task=next(t for t in m['tasks'] if t['id']==TASK)
task.update({'status':'validado','qa_record':'docs/art/qa/ART-18.json','production_record':'docs/art/prompts/ART-18.json','delivery_preview':'art-source/previews/ART-18/ART-18-contact-sheet.png'})
next(t for t in index if t['id']==TASK).update({k:task[k] for k in ['status','qa_record','production_record','delivery_preview']})
p.update({'status':'validado','qa_record':'docs/art/qa/ART-18.json'})
qa={
    'task_id':TASK,'date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual e técnica',
    'scope_resolution':'Executado ART-18 — Produtos japoneses conforme manifesto, continuando o esclarecimento anterior do usuário sobre lotes numerados de produtos. O trecho repetido sobre controles corresponde ao ART-05 e permanece preservado.',
    'production':{'tool':'image_gen.imagegen','model':None,'model_note':'Identificador de modelo não exposto pela ferramenta integrada.','distinct_generations':3,'transparent_background':True,'prompts_record':'docs/art/prompts/ART-18.json','technical_export_source':'art-source/editable/ART-18/export-art18.cjs'},
    'technical':{'passed':technical['passed'],'asset_count':3,'report':'docs/art/qa/ART-18-technical.json','plan_report':'docs/art/qa/ART-18-plan-validation.json','prerequisite_report':'docs/art/qa/ART-18-prerequisites.json'},
    'visual':{
        'passed':True,'backgrounds':['#F5F0E8','#24113D'],
        'native_size_px':[512,512],'display_box_px':[250,250],'display_fit':'contain/preserveAspect, escala uniforme 250/512',
        'contact_sheet':task['delivery_preview'],
        'inspected_previews':[task['delivery_preview']]+[r['native_preview'] for r in p['assets']],
        'checks':[
            'Os três produtos foram inspecionados em tamanho nativo 512×512 e exibição 250×250, sobre fundos marfim e ameixa. Silhuetas completas, contornos limpos e sem halo perceptível.',
            'Combinado médio conserva o motivo do sprite atual japa_img_1: um nigiri de salmão com nori, arroz, wasabi, shiso, cebolinha e ovas em prato azul-petróleo.',
            'Combinado premium conserva o motivo de japa_img_2: temaki de salmão e abacate com nori, arroz, shiso, cebolinha e ovas. Prato azul-petróleo proposto para coerência de apresentação e requisito de prato/bandeja.',
            'Sushi executivo conserva o motivo de japa_img_3: seleção de nigiris, makis e uramakis em bandeja de bambu. Há dez peças visualmente distintas, gengibre e wasabi, organizados sem sobreposição confusa no tamanho de exibição.',
            'Cada produto tem composição própria reconhecível. Nomes genéricos não foram tratados como receitas completas; quantidades e ingredientes visuais são propostas artísticas, sem alteração de dados de produto ou economia.',
            'Cartoon editorial adulto coerente com o concept aprovado e a família de produtos ART-17; salmão coral, arroz marfim, nori verde escuro, cerâmica azul-petróleo e madeira âmbar. Luz superior esquerda e acabamento consistentes.',
            'Nigiri e temaki partilham prato, contorno e materiais. O temaki usa a geração do nigiri como referência de acabamento, sem copiar sua comida.',
            'Nenhum label, palavra, caractere japonês, número, preço, logo, moldura, interface ou checkerboard incorporado. Papel quadriculado, círculos externos, hashis, colher e tigela de shoyu da referência foram excluídos da composição proposta.',
            'Gerações independentes, sem sprite sheet nem recorte de card. Os fundos e rótulos aparecem somente nas provas de QA, não nos sprites.',
            'Originais RGBA 1254×1254 preservados. Frame completo reduzido uniformemente a 410×410 e padding transparente [51,51,51,51] até 512×512, sem crop, pintura, alteração de alfa ou deformação entre eixos.',
            'Pratos, bandeja, cone e guarnições completos; margem visual mínima 51 px, pivot central [0.5,0.5], sem 9-slice, contorno externo do canvas com alfa zero.'
        ],
        'alpha_note':'O alfa predominante do miolo retornado pelo ImageGen é 253, com pixels 252–255. O alfa original foi preservado. Resíduos esparsos de alfa baixo permanecem no original e não são perceptíveis na inspeção sobre os dois fundos.',
        'composition_note':'Os sprites atuais vinculam Combinado médio a nigiri único, Combinado premium a temaki e Sushi executivo à seleção de sushis. Esses motivos foram mantidos nos mesmos IDs, sem reinterpretar a associação pelo nome ou preço.'
    },
    'assets':assets,'total_bytes':sum(a['output_bytes'] for a in assets),
    'integration_proposal':{'textureType':'Sprite (2D and UI)','spriteMode':'Single','pixelsPerUnit':100,'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'display_box_px':[250,250],'fit':'contain/preserveAspect','maxTextureSize':512,'sRGBTexture':True,'alphaIsTransparency':True,'filterMode':'Bilinear','generateMipmaps':False,'compression':'Sem compressão na QA; ajustar por plataforma na integração.'},
    'pending':[],'integration_performed':False
}
changed=[rel for rel,digest in baseline['protected_files'].items() if not (ROOT/rel).is_file() or sha(rel)!=digest]
audit={
    'task_id':TASK,
    'other_asset_records_unchanged':baseline['other_assets']==[a for a in m['assets'] if a['task_id']!=TASK],
    'other_task_records_unchanged':baseline['other_tasks']==[t for t in m['tasks'] if t['id']!=TASK],
    'other_tasks_index_unchanged':baseline['other_tasks_index']==[t for t in index if t['id']!=TASK],
    'manifest_other_fields_unchanged':baseline['manifest_other_fields']=={k:v for k,v in m.items() if k not in ['assets','tasks']},
    'protected_file_count':len(baseline['protected_files']),'protected_files_unchanged':not changed,'changed_protected_files':changed
}
audit['passed']=all(audit[k] for k in ['other_asset_records_unchanged','other_task_records_unchanged','other_tasks_index_unchanged','manifest_other_fields_unchanged','protected_files_unchanged'])
assert audit['passed'],json.dumps(audit)
write('docs/art/manifest.json',m);write('docs/art/tasks.json',index)
write('docs/art/prompts/ART-18.json',p);write('docs/art/qa/ART-18.json',qa)
write('docs/art/qa/ART-18-measurements.json',{'task_id':TASK,'date':'2026-10-14','assets':assets,'total_bytes':qa['total_bytes']})
write('docs/art/qa/ART-18-scope-audit.json',audit)
print(json.dumps({'status':'validado','png_count':3,'total_bytes':qa['total_bytes'],'technical_passed':technical['passed'],'plan_passed':plan['passed'],'scope_audit_passed':audit['passed'],'protected_files':len(baseline['protected_files']),'pending':qa['pending']},ensure_ascii=False))
