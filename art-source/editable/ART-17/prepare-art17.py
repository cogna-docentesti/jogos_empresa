"""Capture authorized ART-17 scope and exact integrated ImageGen requests."""
from pathlib import Path
import hashlib, importlib.util, json, subprocess

ROOT = Path(__file__).resolve().parents[3]
TASK = 'ART-17'

def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p, data):
    dest = ROOT/p
    assert not dest.exists(), f'Preserve existing record: {dest}'
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

m = read('docs/art/manifest.json')
task = next(t for t in m['tasks'] if t['id'] == TASK)
assert task['title'] == 'Produtos de lanches'
assert next(t for t in m['tasks'] if t['id'] == 'ART-15')['status'] == 'validado'
for a in m['assets']:
    if a['task_id'] == TASK:
        assert a['status'] == 'planejado' and not (ROOT/m['asset_root']/a['path']).exists()
protected = {}
for folder in ['Assets/Art/new', 'Assets/Scenes', 'Assets/Scripts', 'Assets/Resources', 'ProjectSettings', 'Packages', 'docs/art/layouts']:
    for p in (ROOT/folder).rglob('*'):
        if p.is_file(): protected[p.relative_to(ROOT).as_posix()] = sha(p)
for p in ['docs/art/characters.json', 'docs/art/tokens.json', 'docs/art/sources.json', 'docs/art/catalogos.json']:
    protected[p] = sha(ROOT/p)
write('docs/art/qa/ART-17-scope-baseline.json', {
    'task_id': TASK, 'date': '2026-10-14',
    'scope_resolution': 'User explicitly selected Executar ART-17: produtos de lanches after the numbering/content mismatch was explained.',
    'protected_files': protected,
    'other_assets': [a for a in m['assets'] if a['task_id'] != TASK],
    'other_tasks': [t for t in m['tasks'] if t['id'] != TASK],
    'other_tasks_index': [t for t in read('docs/art/tasks.json') if t['id'] != TASK],
    'manifest_other_fields': {k:v for k,v in m.items() if k not in ['assets','tasks']},
    'git_status': subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True)
})
spec = importlib.util.spec_from_file_location('art_validator', ROOT/'tools/art/validate_assets.py')
validator = importlib.util.module_from_spec(spec); spec.loader.exec_module(validator)
report = validator.validate_task(ROOT, m, 'ART-15')
assert report['passed'], report['errors']
write('docs/art/qa/ART-17-prerequisites.json', report)

common = '''Composition/camera: one compact meal group, completely inside a SQUARE 1:1 canvas, desired 1024x1024 source. Three-quarter food illustration with visible top surfaces and front ingredients, viewed slightly from above, coherent shallow perspective. Center the complete group, occupying about 80 percent of width and height, at least 10 percent genuinely transparent margin on every side. No cropped tray, handle, bun, fries or drink. Designed for final export 512x512 and contain display 250x250, clear at small size. The meal rests on ONE shallow muted coral-red rectangular serving tray with short rounded corners and a thin front rim, lined with plain warm ivory paper, no pattern. No plate under the tray, no raised pedestal. Tray proportions and food scale should feel plausible for a real adult meal.
Food language: golden sesame bun, one substantial grilled beef patty, melted golden cheese, one fried egg, two folded bacon strips, a restrained layer of thin shoestring potato sticks, crisp green lettuce, tomato and pale-green cucumber slices; discreet red and creamy sauces tucked between layers. A cohesive appetizing Brazilian snack-bar burger; layered ingredients neatly overlap, no detached floating food and no excessive dripping or exploded assembly. Clearly visible front silhouette and readable ingredient layers.
Style/medium: polished hand-drawn adult editorial cartoon from the approved Cogna opening concept, especially its menu illustrations. Confident clean muted dark-plum/brown contours, controlled cel shading, broad near-flat color regions, subtle restrained highlights, crisp edges, warm appetizing natural food colors. Avoid heavy black comic outlines, overstuffed childish proportions, photographic texture, painterly grain, plastic 3D gloss or noise. Warm upper-left light, gentle shadows only where ingredients meet or on the tray. Warm amber #E7B65A, ivory #F5F0E8, ink #30243C, coral-red tray, teal/olive greens. Purple is a restrained shadow accent; food keeps natural colors.
Transparency: genuine RGBA cutout with alpha zero outside the complete meal and tray, clean antialiased edges. Opaque food and tray interiors. No backdrop, floor, ground plane, outside cast shadow, wide drop shadow, halo, glow or haze. No fake transparency checkerboard. No shadow beyond tray silhouette.
Constraints: exactly ONE finished product sprite, never a sprite sheet, collage, multiple views or UI card. No people, hands, labels, lettering, words, numbers, prices, quantity badges, logos, watermarks, interface, icons, frame or border. Paper liner and sauce containers entirely blank. Do not copy the reference sprite background, outer black ring, checkerboard, labels or excessive ingredient height. No decorative props or scattered crumbs.'''

items = [
    ('PRODUCT_LAN_COMBO_SIMPLES', 'Combo simples', 'podrao_img_1.png', 'lan_combo_simples-source-01.png',
     'ONE burger alone on its modest tray, with no separate side order, no drink, no dessert, no sauce cup and no utensils. Keep this the simplest composition. The burger is the clear sole food subject.',
     'Hambúrguer único com pão de gergelim, carne, queijo, ovo, bacon, batata palha, alface, tomate e pepino, em bandeja coral com papel marfim liso. Sem acompanhamento separado.'),
    ('PRODUCT_LAN_COMBO_ESPECIAL', 'Combo especial', 'podrao_img_2.png', 'lan_combo_especial-source-01.png',
     'ONE burger on the left and ONE modest silver wire basket of golden thick-cut fries on the right, together on the same tray. The fry basket has one simple short handle receding to the upper right; realistic clean basket structure with few legible wires. Match the completed simple product in Image 3 for burger design, ingredient stack, serving-tray material, perspective, light, linework and finish; adapt tray width just enough for the fries. No drink, dessert, sauce cup or utensils.',
     'Mesmo hambúrguer do simples, acompanhado por uma cesta metálica de batatas fritas à direita, na mesma bandeja. Sem bebida ou sobremesa.'),
    ('PRODUCT_LAN_COMBO_COMPLETO', 'Combo completo', 'podrao_img_3.png', 'lan_combo_completo-source-01.png',
     'ONE full meal group on one tray: the same burger on the left, a modest silver wire basket of golden thick-cut fries at the back right, one clear tall straight-sided glass of dark cola and two ice cubes near the back center with a plain unbranded light paper straw, one small square chocolate brownie in a shallow plain ivory dessert bowl at the front right, and one tiny blank ivory ramekin of red ketchup near the basket. Keep all elements readable without hiding the burger, no separate cutlery. Match the completed simple product in Image 3 for burger design, tray material, perspective, light, linework and finish. This is the fullest combination exactly as the catalog sprite composition, not a change to the game data. All containers and straw blank with no writing or logos.',
     'Mesmo hambúrguer, cesta de fritas, copo de refrigerante escuro com gelo e canudo liso, brownie em tigela rasa e ramequim de molho, em uma única bandeja. Composição do sprite atual simplificada para leitura a 250×250; sem talheres.')
]
requests = []
for aid, name, sprite, source, subject, visual in items:
    prompt = ('Use case: stylized-concept\nAsset type: ONE isolated food product illustration for the approved Cogna business game, '+aid+'.\n'
        'Input images: Image 1 is the current catalog sprite, CONTENT and relative meal-composition reference only. Image 2 is the approved opening concept, adult editorial cartoon style and food finish reference only. '
        +('Image 3, when supplied, is the completed simple product from this batch, shared burger design and rendering reference. ' if aid != 'PRODUCT_LAN_COMBO_SIMPLES' else '')
        +'Never copy background, UI, text, rings or checkerboard from any reference.\nPrimary request: '+subject+'\n'+common)
    refs = ['Assets/Art/Sprites/RestaurantScene/'+sprite, 'art-source/references/concepts/02-abertura.png']
    if aid != 'PRODUCT_LAN_COMBO_SIMPLES': refs.append('art-source/references/generations/ART-17/lan_combo_simples-source-01.png')
    requests.append({'id':aid, 'name':name, 'prompt':prompt, 'visual_composition_proposal':visual,
        'references':refs, 'tool':'image_gen.imagegen','model':None,
        'model_note':'Specific model identifier is not exposed by the integrated tool.',
        'transparent_background':True, 'source_path':'art-source/references/generations/ART-17/'+source})
write('art-source/editable/ART-17/generations.json', {'task_id':TASK, 'date':'2026-10-14', 'assets':requests})
print(json.dumps({'task':TASK, 'protected_files':len(protected), 'requests':len(requests), 'prerequisite_passed':report['passed']}))
