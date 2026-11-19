"""One-time path migration after the verified PowerShell directory moves."""
from pathlib import Path
import json

PROJECT = Path('D:/Cogna/jogos_empresa')
PLAN = PROJECT / 'docs/art'
SKILL = Path('C:/Users/Pichau/.codex/skills/cogna-artista')
if json.loads((PLAN/'manifest.json').read_text(encoding='utf-8'))['schema_version'] != 1:
    raise SystemExit('Migração já aplicada; não executar novamente.')

def replace_paths(text):
    # HTML references are relative to docs/art, while manifest references are
    # relative to the repository. Replace the HTML form before the generic form.
    for old, new in [
        ('../_references/concepts/', '../../art-source/references/concepts/'),
        ('Assets/Art/new/_production/previews', 'art-source/previews'),
        ('Assets/Art/new/_production', 'docs/art'),
        ('Assets/Art/new/_references', 'art-source/references'),
        ('_production/previews', 'art-source/previews'),
        ('_production', 'docs/art'),
        ('_references', 'art-source/references'),
        ('masters canônicos em characters/CHxx', 'masters canônicos em Assets/Art/new/characters/CHxx'),
    ]:
        text = text.replace(old, new)
    return text

def transform(value):
    if isinstance(value, str):
        return replace_paths(value)
    if isinstance(value, list):
        return [transform(item) for item in value]
    if isinstance(value, dict):
        return {key: transform(item) for key, item in value.items()}
    return value

for file in PLAN.rglob('*'):
    if not file.is_file():
        continue
    if file.suffix == '.json':
        content = transform(json.loads(file.read_text(encoding='utf-8')))
        if file.name == 'manifest.json':
            content['schema_version'] = 2
            content['asset_root'] = 'Assets/Art/new'
            content['plan_root'] = 'docs/art'
            content['reference_root'] = 'art-source/references'
            content['editable_root'] = 'art-source/editable'
            content['preview_root'] = 'art-source/previews'
            content['path_conventions'] = {
                'roots': 'relative to repository root',
                'assets.path': 'relative to asset_root',
                'characters.canonical_asset': 'relative to asset_root',
                'assets.references': 'relative to repository root',
                'assets.source_editable': 'relative to repository root',
                'tasks.auxiliary_deliverables.path': 'relative to repository root',
                'tasks.auxiliary_deliverables.schema': 'relative to repository root',
            }
            for asset in content['assets']:
                if asset.get('source_editable'):
                    asset['source_editable'] = 'art-source/editable/' + asset['source_editable']
        file.write_text(json.dumps(content, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    elif file.suffix in {'.txt', '.html'}:
        content = replace_paths(file.read_text(encoding='utf-8'))
        if file.suffix == '.txt' and file.parent.name == 'tasks':
            content = content.replace('ENTREGÁVEIS\n', 'ENTREGÁVEIS\nCaminhos dos PNGs relativos a Assets/Art/new. Referências, fontes editáveis e contratos relativos à raiz do projeto, conforme o manifesto.\n')
            content += '\nValidador: python tools/art/validate_assets.py --task ' + file.stem + '\n'
        if file.suffix == '.html':
            content = content.replace('<footer>Assets/Art/new ·', '<footer>Plano: docs/art · Arte final: Assets/Art/new ·')
            content = content.replace('<section><div class="grid">', '<section><p><strong>Pastas:</strong> plano e registros em <code>docs/art</code>; concepts, fontes editáveis e previews em <code>art-source</code>; ferramentas em <code>tools/art</code>; PNGs finais em <code>Assets/Art/new</code>.</p><div class="grid">', 1)
        file.write_text(content, encoding='utf-8')

for file in [SKILL/'SKILL.md', SKILL/'references/project.md']:
    content = replace_paths(file.read_text(encoding='utf-8'))
    content = content.replace('seguindo o manifesto em Assets/Art/new', 'seguindo o manifesto em docs/art')
    content = content.replace('Copies dos concepts', 'Cópias dos concepts')
    content = content.replace('`scripts/validate_assets.py --root D:/Cogna/jogos_empresa/Assets/Art/new --task ART-XX`', '`python D:/Cogna/jogos_empresa/tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-XX`')
    if file.name == 'SKILL.md':
        content = content.replace('- Salve cada arquivo no caminho definido no manifesto.', '- Salve cada PNG no caminho definido no manifesto, relativo a `Assets/Art/new`. Referências, `source_editable` e contratos usam caminhos relativos à raiz do projeto. Fontes SVG/código ficam em `art-source/editable`, previews em `art-source/previews` e prompts/QA em `docs/art`.')
    else:
        content += '\n## Organização e caminhos\n\n`Assets/Art/new` contém apenas arte final importável pelo Unity. `docs/art` contém o plano, tarefas, manifesto, schemas, contratos de montagem e registros. `art-source/references` guarda concepts; `art-source/editable` guarda fontes SVG/código; `art-source/previews` guarda provas visuais. `tools/art/validate_assets.py` é o validador do projeto.\n\nNo manifesto v2, `path` e `canonical_asset` são relativos a `asset_root`; `references`, `source_editable` e caminhos de contratos/schemas são relativos à raiz do projeto.\n'
    file.write_text(content, encoding='utf-8')

# Preserve the original bootstrap as historical tooling, but prevent it from
# overwriting the live manifest and resetting generated/validated statuses.
archive = PROJECT/'tools/art/archive/build_initial_plan.py'
content = replace_paths(archive.read_text(encoding='utf-8'))
content = content.replace("PROD = ROOT / 'docs/art'", "PROD = PROJECT / 'docs/art'")
content = content.replace("REF = ROOT/'art-source/references/concepts'", "REF = PROJECT/'art-source/references/concepts'")
content = content.replace("a['source_editable']=str(Path(path)", "a['source_editable']='art-source/editable/'+str(Path(path)")
archive.write_text('"""Historical bootstrap; the live manifest is maintained in docs/art."""\nraise SystemExit("Arquivo histórico: não executar. Use docs/art/manifest.json e tools/art/validate_assets.py.")\n\n' + content, encoding='utf-8')

(SKILL/'scripts/validate_assets.py').write_text('''"""Compatibility entry point; the maintained validator lives in the project."""
from pathlib import Path
import runpy

validator = Path('D:/Cogna/jogos_empresa/tools/art/validate_assets.py')
if not validator.is_file():
    raise SystemExit('Validador não encontrado: ' + str(validator))
runpy.run_path(str(validator), run_name='__main__')
''', encoding='utf-8')

print('Updated manifest v2, 40 task briefs, HTML, schemas/records, skill and archived bootstrap.')
