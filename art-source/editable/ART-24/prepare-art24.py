"""Capture scope and validate real prerequisites for an authorized equipment batch."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,subprocess
ROOT=Path(__file__).resolve().parents[3]
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
parser=argparse.ArgumentParser();parser.add_argument('--task',required=True);args=parser.parse_args();tid=args.task
assert tid in ['ART-24']
m=read('docs/art/manifest.json');task=next(t for t in m['tasks'] if t['id']==tid)
selected=[a for a in m['assets'] if a['task_id']==tid]
assert len(selected)==3 and all(a['kind']=='equipamento' for a in selected)
for a in selected:assert a['status']=='planejado' and not (ROOT/m['asset_root']/a['path']).exists(),a['id']
for dep in task['dependencies']:assert next(t for t in m['tasks'] if t['id']==dep)['status']=='validado',dep
protected={}
for folder in ['Assets/Art/new','Assets/Scenes','Assets/Scripts','Assets/Resources','ProjectSettings','Packages','docs/art/layouts','docs/art/prompts','docs/art/qa','art-source']:
    for path in (ROOT/folder).rglob('*'):
        if path.is_file():protected[path.relative_to(ROOT).as_posix()]=sha(path)
for p in ['docs/art/characters.json','docs/art/tokens.json','docs/art/sources.json','docs/art/catalogos.json']:protected[p]=sha(ROOT/p)
baseline={'task_id':tid,'date':'2026-10-14','scope_resolution':'Execute only ART-24 French equipment in the manifest, continuing the prior clarification about numbered batches. ART-05 controls and previous batches preserved.','protected_files':protected,'other_assets':[a for a in m['assets'] if a['task_id']!=tid],'other_tasks':[t for t in m['tasks'] if t['id']!=tid],'other_tasks_index':[t for t in read('docs/art/tasks.json') if t['id']!=tid],'manifest_other_fields':{k:v for k,v in m.items() if k not in ['assets','tasks']},'git_status':subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True)}
dest=ROOT/f'docs/art/qa/{tid}-scope-baseline.json';assert not dest.exists();dest.write_text(json.dumps(baseline,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
spec=importlib.util.spec_from_file_location('art_validator',ROOT/'tools/art/validate_assets.py');v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
reports=[v.validate_task(ROOT,m,dep) for dep in task['dependencies']]
assert all(r['passed'] for r in reports)
(ROOT/f'docs/art/qa/{tid}-prerequisites.json').write_text(json.dumps({'task_id':tid,'passed':True,'reports':reports},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'task':tid,'asset_count':len(selected),'protected_files':len(protected),'prerequisite_passed':True}))
