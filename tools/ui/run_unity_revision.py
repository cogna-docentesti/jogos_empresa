"""Run the installed Editor against this project, with isolated QA storage."""
import os
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
EDITOR = pathlib.Path(r'C:\Program Files\Unity Editors\6000.3.20f1\Editor\Unity.exe')
mode = sys.argv[1] if len(sys.argv) > 1 else 'apply'
methods = {
    'apply': 'Game.EditorTools.ConceptUiRevision.ApplyAndExit',
    'verify': 'ConceptRevisionValidation.RunAndExit',
    'audit': 'Game.EditorTools.ConceptUiRevision.AuditAndExit',
    'tests': None,
    'states': 'ConceptRevisionValidation.RunAndExit',
    'polish': 'ConceptRevisionValidation.RunAndExit',
    'save_polish': 'Game.EditorTools.ConceptUiRevision.PolishAndExit',
}
out = ROOT / 'output/ui-update/concept-revision'
out.mkdir(parents=True, exist_ok=True)
env = os.environ.copy()
env['COGNA_TEST_DATABASE_PATH'] = str(out / 'qa-game.db')
env['COGNA_TEST_IDENTIFICATION_KEY'] = 'Cogna.ConceptRevision.QA.Identification'
if mode in ('states', 'polish'):
    env['COGNA_CONCEPT_SUITE'] = mode
if mode == 'polish':
    env['COGNA_CONCEPT_PANELS'] = 'Panel_Equipment,Panel_Menu'
args = [str(EDITOR), '-projectPath', str(ROOT), '-buildTarget', 'Win64', '-executeMethod', methods[mode], '-logFile', str(out / (mode + '-editor.log'))]
if mode == 'tests':
    args = [str(EDITOR), '-projectPath', str(ROOT), '-buildTarget', 'Win64', '-runTests', '-testPlatform', 'EditMode', '-testResults', str(out / 'editmode.xml'), '-logFile', str(out / 'tests-editor.log')]
if mode not in ('verify', 'states', 'polish'):
    args.insert(1, '-batchmode')
startup = subprocess.STARTUPINFO()
startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
startup.wShowWindow = subprocess.SW_HIDE
result = subprocess.run(args, cwd=ROOT, env=env, startupinfo=startup)
print(f'{mode}: Unity exit {result.returncode}')
sys.exit(result.returncode)
