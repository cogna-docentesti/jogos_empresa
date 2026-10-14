"""Record ART-12 compact production; preserve other lots and map geometry."""
from pathlib import Path
import json,hashlib,argparse
R=Path(__file__).resolve().parents[3]
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,v):
 p=R/p;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def rh(v):return hashlib.sha256(json.dumps(v,ensure_ascii=False,sort_keys=True).encode()).hexdigest()
def publish():
 baseline=read('docs/art/qa/ART-12-scope-baseline.json');m=read('docs/art/manifest.json');layout=read('docs/art/layouts/map-layout.json')
 assert sha('docs/art/layouts/map-layout.json')==baseline['original_layout_sha256'] or all(a['status']=='gerado' for a in m['assets'] if a['task_id']=='ART-12')
 prod=read('docs/art/prompts/ART-12.json');exports=read('docs/art/qa/ART-12-exports.json')['assets'];dt=layout['display_transform'];s=dt['uniform_scale']
 compact={'schema_version':1,'task_id':'ART-12','date':'2026-10-14','master_size_px':[1920,1080],'coordinate_origin':'top_left_y_down',
  'position_semantics':'Canto superior esquerdo do PNG no master; X à direita e Y para baixo. Margem transparente incluída.',
  'active_contract':'docs/art/layouts/map-layout.json','common_door_anchor_px':layout['central_site']['door_anchor_px'],
  'common_ground_anchor_px':layout['central_site']['building_ground_anchor_px'],'selection_group':'restaurant_variant',
  'restaurant_rule':'Mostrar exatamente uma variante na ordem 70; foreground na ordem 90. Mesma câmera, implantação e porta. Recortes podem ter dimensões distintas; usar as posições individuais.',
  'unity_placement':'Parent master 1920x1080, âncoras top-left, pivot [0,1], anchoredPosition [x,-y], sizeDelta igual ao PNG. Aplicar a transformação uniforme do contrato ao parent.',
  'display_transform':dt,'assets':[],'integration_performed':False}
 for e in exports:
  a=next(a for a in m['assets'] if a['id']==e['id']);assert a['status'] in ['planejado','gerado']
  p=next(p for p in prod['assets'] if p['id']==e['id']);p.update(e);p['status']='gerado'
  w,h=e['actual_size_px'];x,y=e['position_px'];assert sha(e['output_path'])==e['sha256']
  a.update({'export_size_px':[w,h],'actual_size_px':[w,h],'display_size_px':[w*s,h*s],'status':'gerado','sha256':e['sha256'],
   'brief':a['brief'].replace('Full-frame transparente fora da construção.','PNG compacto transparente; posição de encaixe registrada no contrato.').replace('camada alfa alinhada à origem do mapa.','PNG compacto com posição de encaixe no master.'),
   'generation_prompt':p['prompt'],'production_method':'imagegen','generation_tool':prod['tool'],'generation_model':None,
   'generation_source':e['source_path'],'source_editable':None,'export_tool':'sharp 0.35.5 — uniform resize and lossless crop',
   'export_source':'art-source/editable/ART-12/export-compact.cjs','production_record':'docs/art/prompts/ART-12.json',
   'references':list(dict.fromkeys(a['references']+p['references']+[e['source_path']])),
   'texture_storage':'cropped','map_position_px':[x,y],'master_crop_rect_px':[x,y,w,h],'map_master_size_px':[1920,1080],
   'coordinate_contract':'docs/art/layouts/map-layout.json','placement_record':'docs/art/layouts/map-restaurants-compact.json',
   'qa':{'technical':False,'visual':False,'identity':False,'record':'docs/art/qa/ART-12.json'}})
  if e['id']!='MAP_FOREGROUND':a['map_door_anchor_px']=[1280,642]
  layer=next(l for l in layout['layers'] if l['asset_id']==e['id']);layer.update({'position_px':[x,y],'texture_size_px':[w,h],
   'texture_storage':'cropped','crop_rect_px':[x,y,w,h],'asset_path':e['output_path'],'status':'gerado'})
  compact['assets'].append({**e,'status':'gerado','catalog_id':a['catalog_id'],
   'unity_top_left_anchored_position_px':[x,-y],
   'unity_center_pivot_anchored_position_px':[x+w/2,-(y+h/2)],
   'canvas_top_left_px':[dt['offset_px'][0]+x*s,dt['offset_px'][1]+y*s],
   'viewport_local_top_left_px':[x*s,dt['viewport_local_offset_px'][1]+y*s],
   'display_size_px':[w*s,h*s]})
 layout['art12_revision']={'date':'2026-10-14','task_id':'ART-12','previous_contract':baseline['original_layout_path'],
  'previous_contract_sha256':baseline['original_layout_sha256'],'compact_contract':'docs/art/layouts/map-restaurants-compact.json',
  'master_geometry_unchanged':True,'request':'PNG compacto transparente com coordenadas exatas de encaixe, conforme preferência do usuário.'}
 layout['layer_notes']['future_status']='Camadas ART-11 e ART-12 usam PNGs compactos com posições explícitas. Os lotes futuros permanecem planejados.'
 note='Três variantes de restaurante central com mesma implantação e porta, mais foreground; quatro PNGs compactos transparentes, coordenadas em map-restaurants-compact.json.'
 for tasks in [m['tasks'],read('docs/art/tasks.json')]:
  next(t for t in tasks if t['id']=='ART-12').update({'status':'gerado','note':note,'qa_record':'docs/art/qa/ART-12.json','delivery_record':'docs/art/ART-12-entregas.md'})
  if tasks is not m['tasks']:write('docs/art/tasks.json',tasks)
 prod['status']='gerado';prod['export_source']='art-source/editable/ART-12/export-compact.cjs'
 write('docs/art/layouts/map-layout.json',layout);compact['active_contract_sha256']=sha('docs/art/layouts/map-layout.json')
 write('docs/art/layouts/map-restaurants-compact.json',compact);write('docs/art/manifest.json',m);write('docs/art/prompts/ART-12.json',prod)
 lines=['ART-12 — Restaurantes centrais e camada frontal do mapa (PNG compacto)','Escopo: núcleo','Pré-requisitos: ART-10, ART-11','',note,
  'A especificação full-frame foi substituída por recortes e coordenadas, conforme pedido do usuário. Master, câmera, zonas, porta e hit-areas permanecem os mesmos.','',
  'COMANDO PARA O CODEX','Use $cogna-artista para executar somente ART-12, seguindo manifesto e concepts aprovados. Produzir os três restaurantes centrais e o foreground com alfa real, PNGs compactos separados, mesma porta/implantação/escala e coordenadas registradas. Não integrar cenas.','',
  'LEITURAS','docs/art/manifest.json, tokens.json, characters.json, sources.json','docs/art/layouts/map-layout.json','docs/art/layouts/map-restaurants-compact.json','art-source/references/concepts/01-hub.png e 02-abertura.png','',
  'ENTREGÁVEIS']
 for e in exports:lines += [e['id']+' | '+e['path'],f"Export {e['actual_size_px'][0]}x{e['actual_size_px'][1]} px | alfa recorte | pivot [0,1] | bordas [0,0,0,0]",'Posição top-left no master: '+str(e['position_px'])+' | escala 1','']
 lines+=['ENCAIXE','Master 1920x1080, origem top_left_y_down. Restaurantes mutuamente exclusivos na ordem 70; foreground na ordem 90. Porta comum [1280,642], referência de chão [1172,657].',
  'Transformação de exibição compartilhada: escala 2/3, offset absoluto de canvas [24,176], viewport [24,112,1280,848]. Não esticar cada sprite para a viewport.',
  'Conservar câmera, silhueta estrutural, porta e footprint entre variantes; diferenças de decoração/interior. Sem texto, nomes, logos, pessoas ou UI incorporada.','',
  'CRITÉRIOS','Cada PNG no manifesto, RGBA sRGB com alfa real, dimensões e SHA256 reais. Revisão sobre fundos marfim/ameixa e montagem de cada variante com ART-10/ART-11 e foreground.',
  'Preservar alfa gerado. Recorte técnico inclui todo pixel com alfa não zero mais margem de 2 px. Registrar fontes e prompt exato. Nada de outros lotes ou integração.',
  'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-12','python art-source/editable/qa/ART-12-preview.py']
 (R/'docs/art/tasks/ART-12.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 print(json.dumps({'status':'gerado','assets':len(exports),'png_bytes':sum(e['output_bytes'] for e in exports)}))
def audit():
 b=read('docs/art/qa/ART-12-scope-baseline.json');m=read('docs/art/manifest.json');old=read(b['original_layout_path']);cur=read('docs/art/layouts/map-layout.json')
 fields=['master_size_px','coordinate_origin','camera','display_transform','zones','hit_areas','central_site_bounds_px','central_site','central_hit_area','canonical_master']
 changed=[p for p,h in b['protected_files'].items() if not (R/p).is_file() or sha(p)!=h]
 checks={'other_asset_records_unchanged':rh([a for a in m['assets'] if a['task_id']!='ART-12'])==b['other_asset_records_sha256'],
  'other_task_records_unchanged':rh([t for t in m['tasks'] if t['id']!='ART-12'])==b['other_task_records_sha256'],
  'other_tasks_index_unchanged':rh([t for t in read('docs/art/tasks.json') if t['id']!='ART-12'])==b['other_tasks_index_sha256'],
  'geometry_unchanged':all(old[k]==cur[k] for k in fields),
  'other_layers_unchanged':[l for l in old['layers'] if l['production_task']!='ART-12']==[l for l in cur['layers'] if l['production_task']!='ART-12'],
  'protected_files_unchanged':not changed,'changed_protected_files':changed,'protected_file_count':len(b['protected_files'])}
 checks['passed']=all(v for k,v in checks.items() if k.endswith('_unchanged'));write('docs/art/qa/ART-12-scope-audit.json',checks);print(json.dumps(checks));assert checks['passed']
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('mode',choices=['publish','audit']);args=p.parse_args();{'publish':publish,'audit':audit}[args.mode]()
