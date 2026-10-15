"""Prepare ART-19 only; current French sprites define the food motifs."""
from pathlib import Path
import hashlib, importlib.util, json, subprocess

ROOT=Path(__file__).resolve().parents[3]
TASK='ART-19'
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):
    f=ROOT/p;assert not f.exists(),f'Preserve existing record: {f}'
    f.parent.mkdir(parents=True,exist_ok=True)
    f.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

m=read('docs/art/manifest.json')
assert next(t for t in m['tasks'] if t['id']==TASK)['title']=='Produtos franceses'
assert next(t for t in m['tasks'] if t['id']=='ART-15')['status']=='validado'
for a in m['assets']:
    if a['task_id']==TASK:
        assert a['status']=='planejado' and not (ROOT/m['asset_root']/a['path']).exists()
protected={}
for folder in ['Assets/Art/new','Assets/Scenes','Assets/Scripts','Assets/Resources','ProjectSettings','Packages','docs/art/layouts','docs/art/prompts','docs/art/qa','art-source']:
    for path in (ROOT/folder).rglob('*'):
        if path.is_file():protected[path.relative_to(ROOT).as_posix()]=sha(path)
for p in ['docs/art/characters.json','docs/art/tokens.json','docs/art/sources.json','docs/art/catalogos.json']:
    protected[p]=sha(ROOT/p)
write('docs/art/qa/ART-19-scope-baseline.json',{
    'task_id':TASK,'date':'2026-10-14',
    'scope_resolution':'Follow ART-19 French products in the manifest, continuing the prior user clarification to execute numbered product batches; repeated controls text corresponds to validated ART-05.',
    'protected_files':protected,
    'other_assets':[a for a in m['assets'] if a['task_id']!=TASK],
    'other_tasks':[t for t in m['tasks'] if t['id']!=TASK],
    'other_tasks_index':[t for t in read('docs/art/tasks.json') if t['id']!=TASK],
    'manifest_other_fields':{k:v for k,v in m.items() if k not in ['assets','tasks']},
    'git_status':subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True)
})
spec=importlib.util.spec_from_file_location('art_validator',ROOT/'tools/art/validate_assets.py')
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
report=v.validate_task(ROOT,m,'ART-15');assert report['passed'],report['errors']
write('docs/art/qa/ART-19-prerequisites.json',report)

common='''Composition/camera: one compact plated French food product, completely inside a SQUARE 1:1 canvas, desired 1024x1024 source. Three-quarter food illustration with visible top surfaces and a readable front edge, viewed slightly from above; not a flat overhead diagram. Center the entire food and serving surface, approximately 80 percent of canvas width and height, leaving at least 10 percent genuinely transparent margin on every side. No cropped plate, shell, tray, vegetable fan, handle or garnish. Designed for final export 512x512 and contain display 250x250 with clearly separated broad ingredient shapes. Plausible adult food proportions and coherent shallow perspective. Local shadows only where food touches its serving surface.
Style/medium: polished hand-drawn adult editorial cartoon from the approved Cogna opening concept, especially its French restaurant plate illustration. Use Image 3 only for the established finished food rendering, edge clarity, upper-left light and elegant dark blue-teal ceramic material, never its sushi content. Confident clean muted dark-plum/brown contours, controlled cel shading, broad near-flat color regions with restrained warm highlights. Food has natural appetizing color: golden ivory butter, warm beige shell, browned cooked food, tomato coral, olive green zucchini, aubergine plum, warm cream ceramic or honey-colored wood. Avoid heavy black comic outlines, exaggerated childish portions, plastic 3D gloss, photographic texture, painterly grain, blurry edges, wet overgloss and noisy microtexture. Purple only as natural aubergine color and subtle shadow accent. Soft warm upper-left light with small contact shadows confined to the plate/tray.
Transparency: genuine RGBA cutout with alpha zero outside the entire plated meal silhouette, clean antialiased edges. Opaque food and serving-surface interiors. No backdrop, table, floor, ground patch, outside cast shadow, wide drop shadow, halo, glow, mist or steam. No fake transparency checkerboard. No shadow outside the plate or tray silhouette.
Constraints: exactly ONE finished product sprite, never a sprite sheet, collage of separated stickers, multiple views or UI card. No people, hands, labels, lettering, words, numbers, prices, badges, logos, heraldic emblems, watermarks, interface, icons, frame, border, outer black ring or checkered fabric/paper. No utensils, wine glasses, beverages, napkin, decorative props, floating ingredients or scattered crumbs outside the serving surface. Food and garnish remain on one connected plate or serving tray. Do not copy source backgrounds, checkerboard, text, cutlery or symbols. Preserve the primary catalog food motif assigned to this ID rather than changing game data or interpreting generic product names as complete recipes.'''

items=[
 ('PRODUCT_FRA_MENU_CLASSICO','Menu clássico','french_img_1.png','fra_menu_classico-source-01.png',
  'ONE cooked escargot in its large warm cream-and-tan spiral shell, resting on ONE shallow oval dark blue-teal ceramic plate with a thin warm ivory rim. Preserve the current catalog motif, not multiple new portions. The whole spiral shell is visible, curved tip receding toward the upper right; the shell opening faces the lower left and viewer. Inside the opening show a small cooked snail portion in golden melted garlic-herb butter flecked with finely grouped green parsley. The spiral has restrained light tan bands and soft warm highlights, not photographic stripes. A small parsley sprig and one modest lemon wedge sit naturally on the plate at the left of the shell. No live snail body, head, eyes, antennae or animal face: this is a cooked served French dish. No fork, escargot tongs, second snail, bread, bowl or other course. Keep the plate low and elegant, one plate only.',
  'Escargot cozido em concha espiral bege com manteiga de ervas, ramo de salsa e pequeno gomo de limão, sobre prato oval azul-petróleo com filete marfim. Motivo do sprite french_img_1 preservado; garfo excluído da proposta visual.'),
 ('PRODUCT_FRA_MENU_PREMIUM','Menu premium','french_img_2.png','fra_menu_premium-source-01.png',
  'ONE neatly plated ratatouille vegetable arrangement on ONE shallow oval dark blue-teal ceramic plate with a thin warm ivory rim. Preserve the primary motif of the current catalog sprite: a low tidy circular overlapping fan/spiral of alternating tomato slices, pale-green zucchini rounds and aubergine slices with natural dark plum skins, gathered around a modest central herb sprig. Vegetables look softly roasted, with golden olive-oil sheen and a few finely grouped green herb flecks. Small garlic slices near the center, and one tiny golden herb-butter accent near the upper center. Use adult plausible portions with clean individual slices readable at 250x250, no excessively tall tower, no scattered raw ingredients, no exaggerated mound. A restrained warm sauce under the vegetables stays within the plate. No meat, fish, escargot, fork, extra bowl or additional course. Use the same elegant plate material and rendering as the classic French product.',
  'Ratatouille em leque circular de tomate, abobrinha e berinjela, com ervas, pequenas lâminas de alho e toque dourado de manteiga/molho, em prato oval azul-petróleo. Motivo do sprite french_img_2 preservado; talher excluído da proposta visual.'),
 ('PRODUCT_FRA_PRATO_EXECUTIVO','Prato executivo','french_img_3.png','fra_prato_executivo-source-01.png',
  'ONE coherent French tasting-meal arrangement on ONE shallow round honey-colored wooden serving tray with a restrained low rim, viewed as a three-quarter ellipse. This ID uses the current catalog motif of a varied French food selection, even though its generic name is Prato executivo. Simplify the busy catalog image into SIX compact dishes all resting on the same tray: back left, an ivory ramekin of golden-white beans with one browned duck/chicken leg; back right, an ivory bowl of rich brown beef stew with a few clear mushroom slices; center, an ivory shallow bowl of orange seafood stew with a pink shrimp, two dark mussels and a golden fish portion; front left, a small low oval plate of tomato/zucchini/aubergine ratatouille fan; front right, one small round golden quiche tart with a plain crimped pastry rim and tiny herb garnish; front center, a small ivory handled ramekin of onion soup with a golden melted-cheese toast. Arrange dishes in loose tidy staggered rows with coherent perspective and clear spacing; each dish remains completely readable and its container is fully on the tray. Keep the complete tray a single connected meal-group silhouette. Do not add a bread basket, butter disc, truffles, wine, utensils, napkin, logos, decorative emblems or lettering from the reference. No free-floating plates and no separate dish stickers. Broad warm food shapes should remain legible at 250x250, with less tiny detail than in the source.',
  'Seleção de seis pratos franceses em bandeja redonda de madeira: cassoulet com coxa, ensopado de carne e cogumelos, caldo de frutos do mar, ratatouille, quiche e sopa de cebola gratinada. Proposta simplificada do sprite french_img_3, sem bebida, cesta de pães, manteiga com símbolo, trufas, toalha ou talheres; quantidades visuais não alteram dados do jogo.')
]
requests=[]
for aid,name,sprite,source,subject,proposal in items:
    prompt=('Use case: stylized-concept\nAsset type: ONE isolated French food product illustration for the approved Cogna business game, '+aid+'.\n'
        'Input images: Image 1 is the current catalog sprite, primary food CONTENT and dish motif reference only. Image 2 is the approved Cogna opening concept, adult editorial cartoon style and French-food finish reference only. Image 3 is the finished Japanese nigiri from ART-18, shared food rendering, light and ceramic plate material reference only: never copy its sushi, seaweed, rice or fish into the French dishes. Never copy source backgrounds, outer rings, UI, text, checkerboard, emblems or cutlery.\nPrimary request: '+subject+'\n'+common)
    refs=['Assets/Art/Sprites/RestaurantScene/'+sprite,'art-source/references/concepts/02-abertura.png','Assets/Art/new/menu_pricing/products/jap/jap_combinado_medio.png']
    requests.append({'id':aid,'name':name,'prompt':prompt,'visual_composition_proposal':proposal,
        'references':refs,'tool':'image_gen.imagegen','model':None,'model_note':'Specific model identifier is not exposed by the integrated tool.',
        'transparent_background':True,'source_path':'art-source/references/generations/ART-19/'+source})
write('art-source/editable/ART-19/generations.json',{'task_id':TASK,'date':'2026-10-14','assets':requests})

export=(ROOT/'art-source/editable/ART-18/export-art18.cjs').read_text(encoding='utf-8')
export=export.replace('ART-18','ART-19').replace('art18','art19').replace('Produtos japoneses','Produtos franceses').replace('Follow ART-19 Japanese products in the manifest','Follow ART-19 French products in the manifest')
dest=ROOT/'art-source/editable/ART-19/export-art19.cjs';assert not dest.exists();dest.write_text(export,encoding='utf-8')
print(json.dumps({'task':TASK,'protected_files':len(protected),'requests':len(requests),'prerequisite_passed':report['passed']}))
