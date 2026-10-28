"""Record visually inspected ART-16 deliverables and validate without changing art."""
from pathlib import Path
from io import BytesIO
import hashlib,json,subprocess,sys
from PIL import Image,ImageCms
ROOT=Path(__file__).resolve().parents[3]
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def write(p,d):
    (ROOT/p).parent.mkdir(parents=True,exist_ok=True)
    (ROOT/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def validate(args,dest):
    proc=subprocess.run([sys.executable,str(ROOT/'tools/art/validate_assets.py'),'--project',str(ROOT)]+args,cwd=ROOT,capture_output=True,encoding='utf-8')
    assert proc.returncode==0,proc.stdout+proc.stderr
    report=json.loads(proc.stdout);write(dest,report);return report

m=read('docs/art/manifest.json');p=read('docs/art/prompts/ART-16.json');g=read('art-source/editable/ART-16/generations.json');baseline=read('docs/art/qa/ART-16-scope-baseline.json');index=read('docs/art/tasks.json')
assert p['status']=='gerado','Preserve already finalized records.'
technical=validate(['--task','ART-16'],'docs/art/qa/ART-16-technical.json')
plan=validate(['--plan'],'docs/art/qa/ART-16-plan-validation.json')
notes={
 'VIGNETTE_BANK':'Banco de dois pavimentos em marfim, três faixas envidraçadas azuis e entrada central com escadaria, jardineiras e calçada arborizada.',
 'VIGNETTE_CONDOMINIUM':'Seis casas de dois pavimentos, paredes claras, telhados terracota e portas/janelas azuis, integradas por jardins, caminhos e uma via residencial curva.',
 'VIGNETTE_MARKETING':'Conjunto de escritórios com torre alta ao fundo e dois blocos frontais menores, vidro azul, estrutura marfim e praça conectada entre as entradas.',
 'VIGNETTE_STORE':'Comércio de dois pavimentos, térreo coral, andar superior marfim, vitrines azuis e entrada central, com passeio público e arborização lateral.',
 'VIGNETTE_UNIVERSITY':'Universidade com cúpula azul, cupulim, alas laterais de telhados terracota, pórtico de colunas, escadaria e jardins laterais; composição acadêmica preservada.'}
assets=[]
for r in p['assets']:
    a=next(a for a in m['assets'] if a['id']==r['id']);request=next(a for a in g['assets'] if a['id']==r['id'])
    assert r['prompt']==request['prompt']==a['generation_prompt']
    assert r['sha256']==sha(r['output_path']) and r['source_sha256']==sha(r['source_path'])
    for ref,value in r['reference_sha256'].items(): assert sha(ref)==value,ref
    with Image.open(ROOT/r['output_path']) as im:
        assert im.mode=='RGBA' and im.size==(960,540)
        profile=ImageCms.getProfileDescription(ImageCms.ImageCmsProfile(BytesIO(im.info['icc_profile']))).strip()
        assert 'srgb' in profile.lower(),profile
        assert im.getchannel('A').getextrema()==(0,255)
    r.update({'status':'validado','qa_record':'docs/art/qa/ART-16.json'})
    a['status']='validado'
    a['qa']={'technical':True,'visual':True,'identity':True,'identity_applicable':False,'identity_note':'Sem pessoas; identidade arquitetônica comparada com a construção canônica ART-11.','record':'docs/art/qa/ART-16.json','preview':r['display_preview'],'details':r['native_preview'],'architecture_comparison':'art-source/previews/ART-16/ART-16-architecture-comparison.png'}
    assets.append({'id':r['id'],'name':r['name'],'path':r['output_path'],'actual_size_px':r['actual_size_px'],'bytes':r['output_bytes'],'sha256':r['sha256'],'srgb_profile':profile,'technical':True,'visual':True,'notes':notes[r['id']],'source_path':r['source_path'],'source_sha256':r['source_sha256'],'source_size_px':r['source_size_px'],'canonical_map_asset':r['canonical_map_asset'],'measurements':r['measurements'],'display_preview':r['display_preview'],'native_preview':r['native_preview']})

task=next(t for t in m['tasks'] if t['id']=='ART-16')
task.update({'status':'validado','qa_record':'docs/art/qa/ART-16.json','production_record':'docs/art/prompts/ART-16.json','delivery_preview':'art-source/previews/ART-16/ART-16-contact-sheet.png'})
next(t for t in index if t['id']=='ART-16').update({k:task[k] for k in ['status','qa_record','production_record','delivery_preview']})
p.update({'status':'validado','qa_record':'docs/art/qa/ART-16.json'})
qa={'task_id':'ART-16','date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual e técnica','scope_resolution':'Executado ART-16 — Cinco vinhetas de localização, seguindo o número do lote e o manifesto, como na resolução explícita do ART-15. O trecho repetido sobre controles corresponde ao ART-05, preservado.','technical':{'passed':technical['passed'],'asset_count':5,'report':'docs/art/qa/ART-16-technical.json','plan_report':'docs/art/qa/ART-16-plan-validation.json','prerequisite_report':'docs/art/qa/ART-16-prerequisites.json'},'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'native_size_px':[960,540],'display_box_px':[520,292],'display_fit':'contain/preserveAspect, escala uniforme min(520/960,292/540), com arredondamento inteiro do raster e sem esticar eixos','contact_sheet':'art-source/previews/ART-16/ART-16-contact-sheet.png','architecture_comparison':'art-source/previews/ART-16/ART-16-architecture-comparison.png','inspected_previews':[a[k] for a in assets for k in ['display_preview','native_preview']],'checks':['Cada vinheta é um PNG individual com um ambiente conectado e exterior transparente, sem card, moldura ou fundo retangular incorporado.','Silhuetas completas: telhados, cúpula, árvores, fachadas e piso conferidos em tamanho nativo e na caixa 520×292 sobre marfim e ameixa.','Comparação com os cinco sprites principais de ART-11 confirma cores, materiais, tipologia, proporções e silhuetas arquitetônicas reconhecíveis. As vinhetas têm composição própria e não alteram as coordenadas do mapa.','Banco e universidade conservam entradas e escadarias livres; conjunto corporativo conserva hierarquia de alturas e conexão entre blocos.','Bairro residencial contém seis casas em um único ambiente com jardins e via; não é uma prancha de sprites separados.','Calçadas, árvores e jardins dão contexto ambiental, com sombras locais contidas na área da vinheta e sem sombra externa destacada.','Nenhuma pessoa, palavra, número, rótulo, pin, ícone, logo, gráfico, preço, veículo ou elemento de interface incorporado aos PNGs.','Cartoon editorial adulto, contornos ameixa, luz superior esquerda, edifícios marfim, vidro azul, vegetação verde/teal e terracota/coral conforme os concepts e construções canônicas.','Transparência real e bordas externas alfa zero; ausência de checkerboard, halo aparente ou fragmentos visíveis fora do ambiente.','Fontes originais 1672×941 preservadas; redução uniforme para 912×513 e padding transparente [24,13,24,14] até 960×540. Não houve crop, pintura, limpeza de alfa por código ou deformação independente de eixos.'],'alpha_notes':['A geração entrega o miolo visualmente sólido, com alfa predominantemente 252–254 e alguns pixels 255. O alfa original foi preservado, sem forçar opacidade por código.','Resíduos esparsos de alfa muito baixo ampliam o bounding box total da geração; permanecem preservados e não são perceptíveis nos fundos de QA.'],'model_note':'Modelo específico não exposto pela ferramenta integrada; registrado como null.'},'assets':assets,'total_bytes':sum(a['bytes'] for a in assets),'integration_proposal':{'textureType':'Sprite (2D and UI)','spriteMode':'Single','pivot':[0.5,0.5],'nine_slice_lbrt_px':[0,0,0,0],'display_box_px':[520,292],'fit':'contain/preserveAspect','maxTextureSize':1024,'sRGBTexture':True,'alphaIsTransparency':True,'filterMode':'Bilinear','generateMipmaps':False,'compression':'Sem compressão na QA; revisar na integração por plataforma.'},'pending':[],'integration_performed':False}
write('docs/art/manifest.json',m);write('docs/art/tasks.json',index);write('docs/art/prompts/ART-16.json',p);write('docs/art/qa/ART-16.json',qa)
write('docs/art/qa/ART-16-measurements.json',{'task_id':'ART-16','date':'2026-10-14','assets':assets,'total_bytes':qa['total_bytes']})

changed=[rel for rel,h in baseline['protected_files'].items() if not (ROOT/rel).is_file() or sha(rel)!=h]
audit={'task_id':'ART-16','other_asset_records_unchanged':baseline['other_assets']==[a for a in m['assets'] if a['task_id']!='ART-16'],'other_task_records_unchanged':baseline['other_tasks']==[t for t in m['tasks'] if t['id']!='ART-16'],'other_tasks_index_unchanged':baseline['other_tasks_index']==[t for t in index if t['id']!='ART-16'],'manifest_other_fields_unchanged':baseline['manifest_other_fields']=={k:v for k,v in m.items() if k not in ['assets','tasks']},'protected_file_count':len(baseline['protected_files']),'protected_files_unchanged':not changed,'changed_protected_files':changed}
audit['passed']=all(audit[k] for k in ['other_asset_records_unchanged','other_task_records_unchanged','other_tasks_index_unchanged','manifest_other_fields_unchanged','protected_files_unchanged'])
write('docs/art/qa/ART-16-scope-audit.json',audit);assert audit['passed'],json.dumps(audit)
print(json.dumps({'status':'validado','png_count':5,'total_bytes':qa['total_bytes'],'technical_passed':technical['passed'],'plan_passed':plan['passed'],'scope_audit_passed':audit['passed'],'protected_files':len(baseline['protected_files'])},ensure_ascii=False))
