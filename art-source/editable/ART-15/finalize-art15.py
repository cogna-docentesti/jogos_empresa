"""Record inspected ART-15 art and run the repository's read-only validation."""
from pathlib import Path
import hashlib, json, subprocess, sys
from PIL import Image, ImageCms

ROOT=Path(__file__).resolve().parents[3]
def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8'))
def write(path,data):
    (ROOT/path).parent.mkdir(parents=True,exist_ok=True)
    (ROOT/path).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(path):
    return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()
def validate(flag,dest):
    args=[sys.executable,str(ROOT/'tools/art/validate_assets.py'),'--project',str(ROOT)]+flag
    proc=subprocess.run(args,cwd=ROOT,encoding='utf-8',capture_output=True)
    if proc.returncode:
        raise RuntimeError(proc.stdout+proc.stderr)
    report=json.loads(proc.stdout)
    write(dest,report)
    return report

m=read('docs/art/manifest.json')
p=read('docs/art/prompts/ART-15.json')
b=read('docs/art/qa/ART-15-scope-baseline.json')
index=read('docs/art/tasks.json')
assert p['status']=='gerado', 'Finalization already recorded; preserve reviewed records.'
b['other_tasks_index']=[t for t in index if t['id']!='ART-15']
write('docs/art/qa/ART-15-scope-baseline.json',b)

technical=validate(['--task','ART-15'],'docs/art/qa/ART-15-technical.json')
plan=validate(['--plan'],'docs/art/qa/ART-15-plan-validation.json')
assets=[]
for r in p['assets']:
    a=next(a for a in m['assets'] if a['id']==r['id'])
    assert r['sha256']==sha(r['output_path'])
    with Image.open(ROOT/r['output_path']) as im:
        assert im.mode=='RGBA' and im.size==(1024,768) and im.info.get('icc_profile')
        hist=im.getchannel('A').histogram()
        alpha_summary={'transparent_fraction':hist[0]/(im.width*im.height),'fully_opaque_fraction':hist[255]/(im.width*im.height),'alpha_250_or_more_fraction':sum(hist[250:])/(im.width*im.height),'dominant_nonzero_alpha':sorted([(i,hist[i]) for i in range(1,256)],key=lambda x:-x[1])[:4]}
    # Generated interiors are near-opaque (mostly alpha 253-254), not forced to 255 by code.
    measurement=r['measurements']
    if 'antialiased_fraction' in measurement:
        measurement['semitransparent_fraction']=measurement.pop('antialiased_fraction')
    measurement.update(alpha_summary)
    r['status']='validado'
    r['qa_record']='docs/art/qa/ART-15.json'
    r['display_preview']='art-source/previews/ART-15/'+r['id']+'-display-light-dark.png'
    r['native_preview']='art-source/previews/ART-15/'+r['id']+'-native-light-dark.png'
    a['status']='validado'
    a['qa']={'technical':True,'visual':True,'identity':True,'identity_applicable':False,'identity_note':'Sem personagens; continuidade arquitetônica conferida com o restaurante canônico do ART-12.','record':'docs/art/qa/ART-15.json','preview':r['display_preview'],'details':r['native_preview']}
    assets.append({'id':r['id'],'path':r['output_path'],'actual_size_px':r['actual_size_px'],'bytes':r['output_bytes'],'sha256':r['sha256'],'technical':True,'visual':True,'source_sha256':r['source_sha256'],'source_size_px':r['source_size_px'],'measurements':measurement,'display_preview':r['display_preview'],'native_preview':r['native_preview']})

task=next(t for t in m['tasks'] if t['id']=='ART-15')
task.update({'status':'validado','qa_record':'docs/art/qa/ART-15.json','production_record':'docs/art/prompts/ART-15.json','delivery_preview':'art-source/previews/ART-15/ART-15-contact-sheet.png'})
task_index=next(t for t in index if t['id']=='ART-15')
task_index.update({k:task[k] for k in ['status','qa_record','production_record','delivery_preview']})
p['status']='validado'
p['qa_record']='docs/art/qa/ART-15.json'
write('docs/art/manifest.json',m)
write('docs/art/tasks.json',index)
write('docs/art/prompts/ART-15.json',p)
write('docs/art/qa/ART-15-measurements.json',{'task_id':'ART-15','date':'2026-10-14','assets':assets,'total_bytes':sum(a['bytes'] for a in assets)})

qa={'task_id':'ART-15','date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual e técnica','scope_clarification':{'original_request':'ART-15 com descrição de controles/campos/seleção','resolution':'Usuário escolheu explicitamente ART-15: fachadas dos três restaurantes. ART-05 preservado.'},'technical':{'passed':technical['passed'],'asset_count':3,'report':'docs/art/qa/ART-15-technical.json','plan_report':'docs/art/qa/ART-15-plan-validation.json'},'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'display_size_px':[480,360],'display_fit':'contain, escala uniforme 0.46875 da textura inteira','contact_sheet':'art-source/previews/ART-15/ART-15-contact-sheet.png','inspected_previews':[a[key] for a in assets for key in ['display_preview','native_preview']],'checks':['Três fachadas individuais completas, sem card, moldura, pessoas, alimentos, pratos, textos ou símbolos incorporados.','Construção contemporânea de um pavimento coerente com os masters do mapa ART-12: platibanda clara, cobertura plana com condensadora, faixa e toldos roxos, vidro âmbar e porta no retorno direito.','Vista frontal em três quartos mais baixa que a câmera do mapa, com proporções coerentes entre as três variantes. A comparação não exige igualdade pixel a pixel entre gerações.','Francês: mesas com toalhas claras, cadeiras ameixa e molduras discretas de bistrô, com garrafeira sem rótulos integrada ao interior.','Japonês: madeira ripada, balcão com bancos quadrados e luminárias simples, mantendo o mesmo envelope arquitetônico.','Lanches: balcão prático, bancos redondos e mesas compactas em interior acolhedor.','Silhuetas e contornos inspecionados sobre claro/escuro em tamanho nativo e em 480×360; sem checkerboard, fundo incorporado, halo aparente ou cortes de arquitetura.','Alfa de geração preservado. As áreas internas são quase opacas, majoritariamente alpha 253–254; nenhum fechamento para alpha 255 foi aplicado por código.','Redução uniforme da fonte completa 1448×1086 para 960×720, com padding transparente [32,24,32,24] até 1024×768. Sem esticar e sem limpar alfa por código.','Paleta violeta Cogna, cartoon editorial adulto e iluminação superior esquerda coerentes com os concepts aprovados.'],'notes':['Resíduos esparsos de alfa muito baixo herdados da geração ampliam o alpha_bbox; não são perceptíveis nas prévias e foram preservados.','As duas variantes derivam visualmente da fachada de lanches para manter câmera e proporções. Pequenas diferenças de rasterização não afetam o encaixe contain.']},'prerequisites':{'ART-12':'validado','architectural_references':[{'path':r,'sha256':sha(r)} for r in ['Assets/Art/new/location/restaurants/map_rest_frances.png','Assets/Art/new/location/restaurants/map_rest_japones.png','Assets/Art/new/location/restaurants/map_rest_lanches.png']]},'assets':assets,'total_bytes':sum(a['bytes'] for a in assets),'integration_proposal':{'pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'display_size_px':[480,360],'display_fit':'contain/preserveAspect','textureType':'Sprite (2D and UI)','spriteMode':'Single','maxTextureSize':1024,'sRGBTexture':True,'alphaIsTransparency':True,'filterMode':'Bilinear','generateMipmaps':False,'compression':'Sem compressão na QA; revisar por plataforma na integração.'},'pending':[],'integration_performed':False}
write('docs/art/qa/ART-15.json',qa)

changed=[path for path,old in b['protected_files'].items() if not (ROOT/path).is_file() or sha(path)!=old]
audit={'task_id':'ART-15','other_asset_records_unchanged':b['other_assets']==[a for a in m['assets'] if a['task_id']!='ART-15'],'other_task_records_unchanged':b['other_tasks']==[t for t in m['tasks'] if t['id']!='ART-15'],'other_tasks_index_unchanged':b['other_tasks_index']==[t for t in index if t['id']!='ART-15'],'manifest_other_fields_unchanged':b['manifest_other_fields']=={k:v for k,v in m.items() if k not in ['assets','tasks']},'protected_file_count':len(b['protected_files']),'protected_files_unchanged':not changed,'changed_protected_files':changed}
audit['passed']=all(audit[k] for k in ['other_asset_records_unchanged','other_task_records_unchanged','other_tasks_index_unchanged','manifest_other_fields_unchanged','protected_files_unchanged'])
write('docs/art/qa/ART-15-scope-audit.json',audit)
assert audit['passed'], json.dumps(audit)
print(json.dumps({'status':'validado','asset_count':3,'total_bytes':qa['total_bytes'],'technical_passed':technical['passed'],'plan_passed':plan['passed'],'scope_audit_passed':audit['passed']},ensure_ascii=False))
