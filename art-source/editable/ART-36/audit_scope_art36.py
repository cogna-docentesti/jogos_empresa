"""Check ART-36 scope and evidence before the single task commit."""
from pathlib import Path
import hashlib
import json
import subprocess
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
BASE='5d18996a6ff6cebc5e6dab0c04eaef4d5e93e107'
def git(*args):return subprocess.check_output(['git','-C',str(ROOT),*args],text=True,encoding='utf-8').strip()
def read(rel):return json.loads((ROOT/rel).read_text(encoding='utf-8'))
def sha(rel):return hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()

assert git('rev-parse','HEAD')==BASE
assert git('branch','--show-current')=='dev/new-screens'
files=set(git('diff','--name-only',BASE).splitlines())|set(git('ls-files','--others','--exclude-standard').splitlines())
files.discard('docs/art/qa/ART-36-scope-audit.json')
explicit={'docs/art/README.md','docs/art/manifest.json','docs/art/tasks.json','docs/art/ART-36-entregas.md','docs/art/prompts/ART-36.json'}
def allowed(name):
    return name in explicit or name.startswith(('art-source/editable/ART-36/','art-source/previews/ART-36/')) or name.startswith('docs/art/qa/ART-36') and name.endswith('.json')
errors=[f'Outside ART-36 scope: {n}' for n in files if not allowed(n)]
before=json.loads(git('show',BASE+':docs/art/manifest.json'));after=read('docs/art/manifest.json')
if before['assets']!=after['assets']:errors.append('A prior/future asset record changed')
if before['characters']!=after['characters']:errors.append('Canonical identities changed')
task_changes=[b['id'] for a,b in zip(before['tasks'],after['tasks']) if a!=b]
if task_changes!=['ART-36']:errors.append('Unrelated tasks changed')
root_changes=[k for k in before if k!='tasks' and before[k]!=after[k]]
if root_changes!=['phase']:errors.append('Unexpected manifest root change')
before_tasks=json.loads(git('show',BASE+':docs/art/tasks.json'));after_tasks=read('docs/art/tasks.json')
if [b['id'] for a,b in zip(before_tasks,after_tasks) if a!=b]!=['ART-36']:errors.append('Unrelated task mirror change')
index=read('docs/art/qa/ART-36-preview-index.json')
actual_pngs={p.relative_to(ROOT).as_posix() for p in (ROOT/'art-source/previews/ART-36').glob('*.png')}
if actual_pngs!={p['path'] for p in index['pages']}:errors.append('Preview file/index mismatch')
for page in index['pages']:
    with Image.open(ROOT/page['path']) as im:
        im.verify()
        if im.size!=(1920,1080):errors.append('Wrong preview size '+page['path'])
    if sha(page['path'])!=page['sha256'] or not page.get('directly_inspected'):errors.append('Preview changed/unreviewed '+page['path'])
if len(index['coverage'])!=265 or any(not v for v in index['coverage'].values()):errors.append('Incomplete visual evidence')
for file in files:
    if file.endswith('.json'):read(file)
inventory=read('docs/art/qa/ART-36-inventory.json')
imports=read('docs/art/qa/ART-36-import-audit.json')
for a in inventory['assets']:
    if sha(a['path'])!=a['sha256']:errors.append('Audited PNG changed '+a['path'])
for a in imports['assets']:
    if sha(a['path'])!=a['sha256']:errors.append('Audited meta changed '+a['path'])
subprocess.run(['git','-C',str(ROOT),'diff','--check'],check=True)
report={'task_id':'ART-36','base_commit':BASE,'passed':not errors,'errors':errors,
 'no_production_png_or_meta_changes':not any(p.startswith('Assets/') for p in files),
 'asset_and_character_records_unchanged':before['assets']==after['assets'] and before['characters']==after['characters'],
 'changed_tasks':task_changes,'preview_count':len(index['pages']),'png_and_meta_sha256_rechecked':265,
 'candidate_file_count_excluding_this_report':len(files),'total_bytes_excluding_this_report':sum((ROOT/f).stat().st_size for f in files),
 'paths':sorted(files)}
(ROOT/'docs/art/qa/ART-36-scope-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='paths'}))
assert not errors,errors
