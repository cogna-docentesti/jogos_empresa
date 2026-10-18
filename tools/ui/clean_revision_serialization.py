"""Remove incidental Editor cache edits and trailing whitespace after QA exits."""
from pathlib import Path
import subprocess
import os

root=Path(__file__).resolve().parents[2]
changed=subprocess.check_output(['git','-c','core.safecrlf=false','diff','--name-only'],cwd=root,text=True).splitlines()
restored=0
trimmed=0
incidental={
    'ProjectSettings/ShaderGraphSettings.asset',
    'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset',
}
def write_atomic(path,data):
    temporary=path.with_name(path.name+'.ui-revision.tmp')
    if not temporary.resolve().is_relative_to(root):raise ValueError(temporary)
    temporary.write_bytes(data)
    os.replace(temporary,path)
for name in changed:
    path=(root/name).resolve()
    if not path.is_relative_to(root):raise ValueError(name)
    original=subprocess.check_output(['git','show','HEAD:'+name],cwd=root)
    current=path.read_bytes()
    if name in incidental or (name.startswith('Assets/Art/new/') and path.suffix=='.meta' and original.split()==current.split()):
        write_atomic(path,original);restored+=1
    elif path.suffix in {'.unity','.prefab','.asset','.cs','.json','.txt'}:
        cleaned=b'\n'.join(line.rstrip(b' \t\r') for line in current.split(b'\n'))
        if cleaned!=current:write_atomic(path,cleaned);trimmed+=1
print('Restored incidental cache/import changes:',restored)
print('Normalized whitespace:',trimmed)
