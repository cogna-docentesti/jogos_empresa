"""Launch the final graphical Windows Player with disposable storage and corpus."""
import datetime
import hashlib
import json
import os
import pathlib
import subprocess
import uuid
import time

ROOT=pathlib.Path(__file__).resolve().parents[2]
OUT=ROOT/'output/ui-update/fonts/FONT-10/windows-player'
EXE=ROOT/'Builds/FONT-10/Windows/Cogna.exe'

def main():
    assert EXE.is_file(), 'Build the final Windows Player first'
    OUT.mkdir(parents=True,exist_ok=True)
    assert not (OUT/'execution.json').exists(), 'Archive the previous run before reusing its evidence directory'
    corpus=json.loads((ROOT/'output/ui-update/fonts/FONT-01/glyph-corpus.json').read_text(encoding='utf-8'))
    env=os.environ.copy()
    env['COGNA_TEST_DATABASE_PATH']=str((OUT/f'qa-{uuid.uuid4()}.db').resolve())
    env['COGNA_TEST_IDENTIFICATION_KEY']=f'Cogna.Font10.Windows.{uuid.uuid4()}'
    env['COGNA_FONT10_OUTPUT']=str(OUT.resolve())
    env['COGNA_FONT10_CORPUS']=''.join(chr(x) for x in corpus['primary_codepoints'])
    args=[str(EXE),'--font10-qa','-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',str(OUT/'Player.log')]
    start=datetime.datetime.now(datetime.timezone.utc).isoformat()
    child=subprocess.Popen(args,cwd=ROOT,env=env)
    (OUT/'launch.json').write_text(json.dumps({'pid':child.pid,'args':args,'started_utc':start,'database':env['COGNA_TEST_DATABASE_PATH'],'key':env['COGNA_TEST_IDENTIFICATION_KEY'],'exe_sha256':hashlib.sha256(EXE.read_bytes()).hexdigest(),'graphical':True,'isolated':True},indent=2)+'\n',encoding='utf-8')
    print(f'Windows Player pid {child.pid}; evidence {OUT}',flush=True)
    try:
        ready_deadline=time.monotonic()+120
        while child.poll() is None and not (OUT/'font-assets.json').exists():
            if time.monotonic()>ready_deadline:raise subprocess.TimeoutExpired(args,120)
            time.sleep(1)
        code=child.wait(timeout=1800)
    except subprocess.TimeoutExpired:
        child.terminate()
        code=child.wait(timeout=30)
        (OUT/'process-exit.json').write_text(json.dumps({'exit_code':code,'finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'timed_out':True,'terminated_own_child':True},indent=2)+'\n',encoding='utf-8')
        raise RuntimeError(f'Player deadline exceeded; exit {code}')
    (OUT/'process-exit.json').write_text(json.dumps({'exit_code':code,'finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()},indent=2)+'\n',encoding='utf-8')
    print(f'Windows Player exit {code}',flush=True)
    raise SystemExit(code)

if __name__=='__main__':main()
