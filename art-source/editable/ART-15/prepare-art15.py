"""Capture scope before ART-15 production and prepare exact ImageGen requests."""
from pathlib import Path
import hashlib, json, subprocess

ROOT = Path(__file__).resolve().parents[3]
def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8'))
def write(path, data):
    dest=ROOT/path
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.exists():
        raise RuntimeError(f'Preserve existing record: {path}')
    dest.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

m=read('docs/art/manifest.json')
task=next(t for t in m['tasks'] if t['id']=='ART-15')
for dep in task['dependencies']:
    assert next(t for t in m['tasks'] if t['id']==dep)['status']=='validado', dep
for a in m['assets']:
    if a['task_id']=='ART-15':
        assert a['status']=='planejado' and not (ROOT/m['asset_root']/a['path']).exists(), a['id']

protected={}
for directory in ['Assets/Art/new','Assets/Scenes','Assets/Scripts','Assets/Resources','ProjectSettings','Packages','docs/art/layouts']:
    folder=ROOT/directory
    if folder.is_dir():
        for path in folder.rglob('*'):
            if path.is_file():
                protected[path.relative_to(ROOT).as_posix()]=sha(path)
for path in ['docs/art/characters.json','docs/art/tokens.json','docs/art/sources.json','docs/art/catalogos.json']:
    protected[path]=sha(ROOT/path)
write('docs/art/qa/ART-15-scope-baseline.json', {'task_id':'ART-15','date':'2026-10-14','protected_files':protected,'other_assets':[a for a in m['assets'] if a['task_id']!='ART-15'],'other_tasks':[t for t in m['tasks'] if t['id']!='ART-15'],'manifest_other_fields':{k:v for k,v in m.items() if k not in ['assets','tasks']},'git_status':subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True)})

prompt='''Use case: stylized-concept
Asset type: ONE isolated restaurant facade illustration for the approved Cogna business game, FACADE_REST_LANCHES.
Input images: Image 1 is the canonical casual restaurant from ART-12, architecture and materials reference. Image 2 is the approved opening concept, adult editorial cartoon style reference. Image 3 is the approved management concept, linework and finish reference. Never copy their UI, text, people, meals or scenes.
Primary request: depict the SAME low contemporary neighborhood snack restaurant as Image 1 from a LOWER, mostly frontal three-quarter view, with broad storefront on the left and a narrower entrance facade receding to the right. Keep a single story, flat grey roof, low ivory parapet, one modest roof air-conditioning unit, blank broad violet fascia wrapping the corner, purple fabric awnings, ivory structural columns, thin stone plinth and dark plum window frames. Two broad glass windows on the main face; narrow glazed entrance on the right-hand face close to the front corner. Show the whole freestanding building, uncropped.
Camera/composition: landscape 4:3, desired 1024x768 pixels. Lower architectural front three-quarter view rather than a top-down map view: vertical walls upright, main fascia almost horizontal and the right return clearly visible. Roof remains a shallow visible plane. Center the full building with 6-8 percent truly transparent margin on every side. Stable centered footprint and adult realistic architectural proportions, no exaggerated lens or miniature pedestal. Fill about 86 percent of canvas width. This exterior and view will be reused for French and Japanese fitout variants.
Fitout: popular, welcoming and practical casual restaurant. Warm amber light inside, simple timber service counter, several plain round stools and compact square tables/chairs visible naturally through the windows. Empty counter and empty table surfaces; absolutely no food, meals, plates, drinks, packaging, menu boards or extra props. Furniture belongs inside the building, not outside.
Style/medium: polished hand-drawn adult editorial cartoon matching the approved Cogna concepts. Confident dark muted-plum contours, clean controlled cel-shaded color regions, crisp material edges, two or three broad tones per material, subtle limited gradient only where useful. Avoid painted grain or fine mottled texture. Cozy clear glazing with simple reflections, legible at display size 480x360.
Palette and light: Cogna violet #6D28D9, plum #24113D, lavender #B99AE8, warm ivory #F5F0E8, ink #30243C, timber brown and warm amber #E7B65A. Soft upper-left lighting with shadows on the architecture only. All fascia and awnings remain purple. No exterior ground shadow.
Transparency: genuine RGBA cutout, alpha zero all around the building and clean antialiased silhouette. Opaque building and glazing; transparent exterior. No background, floor, pavement patch, landscape, street, backdrop, haze, halo, shadow outside silhouette, checkerboard or border. Keep the transparent margins.
Constraints: ONE building sprite only, never a sheet or a card. No lettering, words, numbers, symbols, logos, signs with writing, people, faces, food, dishes, exterior plants, outdoor furniture, props, neighboring buildings, scenery or interface. No placeholder shapes. The plain violet fascia is entirely blank.'''
write('art-source/editable/ART-15/generations.json',{'task_id':'ART-15','date':'2026-10-14','assets':[{'id':'FACADE_REST_LANCHES','prompt':prompt,'references':['art-source/references/generations/ART-12/map_rest_lanches-source.png','art-source/references/concepts/02-abertura.png','art-source/references/concepts/03-gestao.png'],'tool':'image_gen.imagegen','model':None,'transparent_background':True,'source_path':'art-source/references/generations/ART-15/rest_lanches-source-01.png'}]})
print(json.dumps({'protected_files':len(protected),'prerequisite':'ART-12 validado','requested_png_count':3,'scope_conflict_resolved':'User chose ART-15 facades.'}))
