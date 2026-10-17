"""Run a fresh, read-only Editor census and isolated native-flow typography baseline."""
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/ui-update/fonts/FONT-01'
EDITOR = Path(r'C:\Program Files\Unity Editors\6000.3.20f1\Editor\Unity.exe')
OUT.mkdir(parents=True, exist_ok=True)
if (ROOT / 'Temp/UnityLockfile').exists():
    raise SystemExit('Project already open: use Tools > Jogos de Empresa > FONT-01 > Inventory and capture in open Editor (Ctrl+Alt+F1). Do not launch another Editor or save the current scene.')
env = dict(os.environ, COGNA_TEST_DATABASE_PATH=str(OUT / 'qa-game.db'),
           COGNA_TEST_IDENTIFICATION_KEY='Cogna.Font01.QA.Identification')
startup = subprocess.STARTUPINFO()
startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
startup.wShowWindow = subprocess.SW_HIDE
result = subprocess.run([str(EDITOR), '-projectPath', str(ROOT), '-buildTarget', 'Win64',
                         '-executeMethod', 'Font01InventoryValidation.RunAndExit',
                         '-logFile', str(OUT / 'editor.log')], cwd=ROOT, env=env, startupinfo=startup)
print(f'FONT-01 Unity exit {result.returncode}')
sys.exit(result.returncode)
