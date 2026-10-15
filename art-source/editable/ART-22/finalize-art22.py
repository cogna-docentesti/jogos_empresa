"""Finalize only a visually reviewed, real equipment batch and audit its scope."""
from pathlib import Path
from io import BytesIO
import argparse,hashlib,json,subprocess,sys
from PIL import Image,ImageCms
ROOT=Path(__file__).resolve().parents[3]
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def write(p,d):
    f=ROOT/p;f.parent.mkdir(parents=True,exist_ok=True)
    f.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def validate(args,dest):
    proc=subprocess.run([sys.executable,'-B',str(ROOT/'tools/art/validate_assets.py'),'--project',str(ROOT)]+args,cwd=ROOT,capture_output=True,encoding='utf-8')
    assert proc.returncode==0,proc.stdout+proc.stderr
    report=json.loads(proc.stdout);write(dest,report);return report
parser=argparse.ArgumentParser();parser.add_argument('--task',required=True);args=parser.parse_args();tid=args.task
assert tid in ['ART-22']
m=read('docs/art/manifest.json');index=read('docs/art/tasks.json')
p=read(f'docs/art/prompts/{tid}.json');g=read(f'art-source/editable/{tid}/generations.json')
baseline=read(f'docs/art/qa/{tid}-scope-baseline.json');review=read(f'art-source/editable/{tid}/visual-review.json')
assert p['status']=='gerado' and review['passed'] is True
assert set(review['asset_ids'])=={r['id'] for r in p['assets']}
for preview in review['inspected_previews']:assert (ROOT/preview).is_file(),preview
technical=validate(['--task',tid],f'docs/art/qa/{tid}-technical.json')
plan=validate(['--plan'],f'docs/art/qa/{tid}-plan-validation.json')
assets=[]
for r in p['assets']:
    a=next(a for a in m['assets'] if a['id']==r['id']);req=next(a for a in g['assets'] if a['id']==r['id'])
    assert a['task_id']==tid and req['prompt']==r['prompt']==a['generation_prompt']
    assert sha(r['output_path'])==r['sha256'] and sha(r['source_path'])==r['source_sha256']
    for ref,digest in r['reference_sha256'].items():assert sha(ref)==digest,ref
    with Image.open(ROOT/r['output_path']) as im:
        assert im.mode=='RGBA' and im.size==(512,512)
        profile=ImageCms.getProfileDescription(ImageCms.ImageCmsProfile(BytesIO(im.info['icc_profile']))).strip()
        assert 'srgb' in profile.lower()
        alpha=im.getchannel('A');assert alpha.getextrema()==(0,255)
        box=alpha.point(lambda x:255 if x>=128 else 0).getbbox()
        assert min(box[0],box[1],512-box[2],512-box[3])>=51
    r.update({'status':'validado','qa_record':f'docs/art/qa/{tid}.json'})
    a.update({'status':'validado','qa':{'technical':True,'visual':True,'identity':True,'identity_applicable':False,
        'identity_note':'Sem personagens; tipologia e componentes do equipamento comparados ao sprite atual do catálogo e concept de gestão.',
        'record':f'docs/art/qa/{tid}.json','preview':r['display_preview'],'details':r['native_preview']}})
    assets.append({k:r[k] for k in ['id','name','output_path','actual_size_px','sha256','output_bytes','source_path','source_sha256','source_size_px','visual_composition_proposal','measurements','native_preview','display_preview']})
task=next(t for t in m['tasks'] if t['id']==tid)
task.update({'status':'validado','qa_record':f'docs/art/qa/{tid}.json','production_record':f'docs/art/prompts/{tid}.json','delivery_preview':f'art-source/previews/{tid}/{tid}-contact-sheet.png'})
next(t for t in index if t['id']==tid).update({k:task[k] for k in ['status','qa_record','production_record','delivery_preview']})
p.update({'status':'validado','qa_record':task['qa_record']})
qa={
    'task_id':tid,'title':task['title'],'date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual e técnica',
    'scope_resolution':'Executado somente ART-22 — Equipamentos de lanches, conforme manifesto e esclarecimento anterior do usuário sobre os lotes numerados. Trecho repetido sobre controles corresponde ao ART-05, preservado, assim como lotes anteriores.',
    'production':{'tool':'image_gen.imagegen','model':None,'model_note':'Identificador de modelo não exposto pela ferramenta integrada.','distinct_generations':len(assets),'transparent_background':True,'prompts_record':task['production_record'],'technical_export_source':'art-source/editable/ART-22/export-art22.cjs'},
    'technical':{'passed':technical['passed'],'asset_count':len(assets),'report':f'docs/art/qa/{tid}-technical.json','plan_report':f'docs/art/qa/{tid}-plan-validation.json','prerequisite_report':f'docs/art/qa/{tid}-prerequisites.json'},
    'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'native_size_px':[512,512],'display_box_px':[250,250],'display_fit':'contain/preserveAspect, escala uniforme 250/512','contact_sheet':task['delivery_preview'],'inspected_previews':review['inspected_previews'],'checks':review['checks'],
        'alpha_note':'Alfa retornado pelo ImageGen preservado, inclusive antialias e eventuais resíduos esparsos de alfa baixo. Miolos visualmente sólidos e contornos conferidos sobre marfim e ameixa; medições completas por arquivo.',
        'technical_export_note':'Frame completo reduzido uniformemente para caixa máxima 410×410, com padding transparente até 512×512. Sem crop, pintura, recolor, limpeza de alfa ou deformação independente dos eixos.',
        'framing_note':'Ícones com enquadramento comum e proporções comerciais plausíveis, câmera de três quartos e peso visual comparável para exibição em caixas 250×250. Preservar proporção; não esticar os eixos.'},
    'assets':assets,'total_bytes':sum(a['output_bytes'] for a in assets),
    'integration_proposal':{'textureType':'Sprite (2D and UI)','spriteMode':'Single','pixelsPerUnit':100,'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'display_box_px':[250,250],'fit':'contain/preserveAspect','maxTextureSize':512,'sRGBTexture':True,'alphaIsTransparency':True,'filterMode':'Bilinear','generateMipmaps':False,'compression':'Sem compressão na QA; ajustar por plataforma na integração.'},
    'pending':[],'integration_performed':False
}
# The current batch's own production requests are editable by authorization.
# Preserve all pre-existing art, prior/future batch records and shared tooling.
own_prefixes=[f'art-source/editable/{tid}/',f'art-source/previews/{tid}/',f'art-source/references/generations/{tid}/',f'docs/art/prompts/{tid}.json',f'docs/art/qa/{tid}']
owned=lambda rel:any(rel.startswith(prefix) for prefix in own_prefixes)
protected={rel:digest for rel,digest in baseline['protected_files'].items() if not owned(rel)}
changed=[rel for rel,digest in protected.items() if not (ROOT/rel).is_file() or sha(rel)!=digest]
authorized_updates=[rel for rel,digest in baseline['protected_files'].items() if owned(rel) and (not (ROOT/rel).is_file() or sha(rel)!=digest)]
audit={'task_id':tid,'other_asset_records_unchanged':baseline['other_assets']==[a for a in m['assets'] if a['task_id']!=tid],
    'other_task_records_unchanged':baseline['other_tasks']==[t for t in m['tasks'] if t['id']!=tid],
    'other_tasks_index_unchanged':baseline['other_tasks_index']==[t for t in index if t['id']!=tid],
    'manifest_other_fields_unchanged':baseline['manifest_other_fields']=={k:v for k,v in m.items() if k not in ['assets','tasks']},
    'snapshot_file_count':len(baseline['protected_files']),'protected_file_count':len(protected),'authorized_current_task_source_updates':authorized_updates,
    'protected_files_unchanged':not changed,'changed_protected_files':changed}
audit['passed']=all(audit[k] for k in ['other_asset_records_unchanged','other_task_records_unchanged','other_tasks_index_unchanged','manifest_other_fields_unchanged','protected_files_unchanged'])
assert audit['passed'],json.dumps(audit)
write('docs/art/manifest.json',m);write('docs/art/tasks.json',index)
write(task['production_record'],p);write(task['qa_record'],qa)
write(f'docs/art/qa/{tid}-measurements.json',{'task_id':tid,'date':'2026-10-14','assets':assets,'total_bytes':qa['total_bytes']})
write(f'docs/art/qa/{tid}-scope-audit.json',audit)
print(json.dumps({'task':tid,'status':'validado','png_count':len(assets),'total_bytes':qa['total_bytes'],'technical_passed':technical['passed'],'plan_passed':plan['passed'],'scope_audit_passed':audit['passed'],'protected_files':len(protected),'pending':qa['pending']},ensure_ascii=False))
