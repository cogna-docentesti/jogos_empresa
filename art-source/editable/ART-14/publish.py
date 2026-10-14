"""Publish ART-14 only, preserving all previous records, art and identity masters."""
from pathlib import Path
import json,hashlib,argparse
R=Path(__file__).resolve().parents[3]
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,d):(R/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def rh(v):return hashlib.sha256(json.dumps(v,ensure_ascii=False,sort_keys=True).encode()).hexdigest()
def audit():
 b=read('docs/art/qa/ART-14-scope-baseline.json');m=read('docs/art/manifest.json')
 changed=[p for p,h in b['protected_files'].items() if not (R/p).is_file() or sha(p)!=h]
 checks={'other_asset_records_unchanged':rh([a for a in m['assets'] if a['task_id']!='ART-14'])==b['other_asset_records_sha256'],
  'other_task_records_unchanged':rh([t for t in m['tasks'] if t['id']!='ART-14'])==b['other_task_records_sha256'],
  'other_tasks_index_unchanged':rh([t for t in read('docs/art/tasks.json') if t['id']!='ART-14'])==b['other_tasks_index_sha256'],
  'protected_files_unchanged':not changed,'changed_protected_files':changed,'protected_file_count':len(b['protected_files'])}
 checks['passed']=all(v for k,v in checks.items() if k.endswith('_unchanged'));write('docs/art/qa/ART-14-scope-audit.json',checks);assert checks['passed'];return checks
def publish():
 m=read('docs/art/manifest.json');prod=read('docs/art/prompts/ART-14.json')
 for r in prod['assets']:
  a=next(a for a in m['assets'] if a['id']==r['id']);assert a['task_id']=='ART-14' and a['status']=='planejado'
  assert r['sha256']==sha(r['output_path']) and r['actual_size_px']==a['export_size_px']
  a.update({'status':'gerado','sha256':r['sha256'],'actual_size_px':r['actual_size_px'],'generation_prompt':r['prompt'],
   'production_method':r['production_method'],'source_editable':r['source_editable'],'generation_tool':r['tool'],'generation_model':None,
   'production_record':'docs/art/prompts/ART-14.json','export_source':'art-source/editable/ART-14/export-art14.cjs',
   'component_contract':'docs/art/layouts/identification-layout.json',
   'references':list(dict.fromkeys(a['references']+r['references'])),
   'qa':{'technical':False,'visual':False,'identity':False,'record':'docs/art/qa/ART-14.json'}})
  if r['production_method']=='imagegen':a['generation_source']=r['source_path']
  if a['character_ids']:
   a['canonical_reference']=r['canonical_reference'];a['visible_content_rect_px']=r['visible_bbox_alpha_128_px']
 for tasks in [m['tasks'],read('docs/art/tasks.json')]:
  next(t for t in tasks if t['id']=='ART-14').update({'status':'gerado','qa_record':'docs/art/qa/ART-14.json','delivery_record':'docs/art/ART-14-entregas.md'})
  if tasks is not m['tasks']:write('docs/art/tasks.json',tasks)
 write('docs/art/manifest.json',m);assert audit()['passed']
 print(json.dumps({'task_id':'ART-14','status':'gerado','png_count':4}))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('mode',choices=['publish','audit']);a=p.parse_args();{'publish':publish,'audit':lambda:print(json.dumps(audit()))}[a.mode]()
