"""Freeze existing ART-16 inputs and earlier production before illustration work."""
from pathlib import Path
import hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[3]
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):
    dest=ROOT/p
    assert not dest.exists(), 'Preserve existing record: '+p
    dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
m=read('docs/art/manifest.json');task=next(t for t in m['tasks'] if t['id']=='ART-16')
assert task['status']=='planejado'
for dep in task['dependencies']:
    assert next(t for t in m['tasks'] if t['id']==dep)['status']=='validado',dep
for a in m['assets']:
    if a['task_id']=='ART-16': assert not (ROOT/m['asset_root']/a['path']).exists(),a['id']
protected={}
for directory in ['Assets/Art/new','Assets/Scenes','Assets/Scripts','Assets/Resources','ProjectSettings','Packages','docs/art','art-source/references','art-source/editable','art-source/previews']:
    for path in (ROOT/directory).rglob('*'):
        if not path.is_file(): continue
        rel=path.relative_to(ROOT).as_posix()
        if rel in ['docs/art/manifest.json','docs/art/tasks.json'] or '/ART-16/' in rel or rel.startswith('docs/art/qa/ART-16') or rel=='docs/art/prompts/ART-16.json': continue
        protected[rel]=sha(path)
write('docs/art/qa/ART-16-scope-baseline.json',{'task_id':'ART-16','date':'2026-10-14','protected_files':protected,'other_assets':[a for a in m['assets'] if a['task_id']!='ART-16'],'other_tasks':[t for t in m['tasks'] if t['id']!='ART-16'],'other_tasks_index':[t for t in read('docs/art/tasks.json') if t['id']!='ART-16'],'manifest_other_fields':{k:v for k,v in m.items() if k not in ['assets','tasks']},'git_status':subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True)})
proc=subprocess.run([sys.executable,str(ROOT/'tools/art/validate_assets.py'),'--task','ART-11'],cwd=ROOT,capture_output=True,encoding='utf-8');assert proc.returncode==0,proc.stdout
write('docs/art/qa/ART-16-prerequisites.json',{'task_id':'ART-16','prerequisite':'ART-11','report':json.loads(proc.stdout)})
print(json.dumps({'protected_files':len(protected),'prerequisite_passed':True,'requested_assets':len(task['asset_ids'])}))
