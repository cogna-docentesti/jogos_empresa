"""Prepare only ART-18; preserve catalog motifs and already delivered batches."""
from pathlib import Path
import hashlib, importlib.util, json, subprocess

ROOT = Path(__file__).resolve().parents[3]
TASK = 'ART-18'
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p, data):
    dest = ROOT/p
    assert not dest.exists(), f'Preserve existing record: {dest}'
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

m = read('docs/art/manifest.json')
assert next(t for t in m['tasks'] if t['id']==TASK)['title']=='Produtos japoneses'
assert next(t for t in m['tasks'] if t['id']=='ART-15')['status']=='validado'
for a in m['assets']:
    if a['task_id']==TASK:
        assert a['status']=='planejado' and not (ROOT/m['asset_root']/a['path']).exists()

protected = {}
for folder in ['Assets/Art/new','Assets/Scenes','Assets/Scripts','Assets/Resources','ProjectSettings','Packages','docs/art/layouts','docs/art/prompts','docs/art/qa','art-source']:
    for path in (ROOT/folder).rglob('*'):
        if path.is_file(): protected[path.relative_to(ROOT).as_posix()]=sha(path)
for p in ['docs/art/characters.json','docs/art/tokens.json','docs/art/sources.json','docs/art/catalogos.json']:
    protected[p]=sha(ROOT/p)
write('docs/art/qa/ART-18-scope-baseline.json',{
    'task_id':TASK,'date':'2026-10-14',
    'scope_resolution':'Follow ART-18 in the manifest, continuing the prior user clarification to execute numbered product batches; repeated controls text maps to already validated ART-05.',
    'protected_files':protected,
    'other_assets':[a for a in m['assets'] if a['task_id']!=TASK],
    'other_tasks':[t for t in m['tasks'] if t['id']!=TASK],
    'other_tasks_index':[t for t in read('docs/art/tasks.json') if t['id']!=TASK],
    'manifest_other_fields':{k:v for k,v in m.items() if k not in ['assets','tasks']},
    'git_status':subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True)
})
spec=importlib.util.spec_from_file_location('art_validator',ROOT/'tools/art/validate_assets.py')
v=importlib.util.module_from_spec(spec);spec.loader.exec_module(v)
r=v.validate_task(ROOT,m,'ART-15');assert r['passed'],r['errors']
write('docs/art/qa/ART-18-prerequisites.json',r)

common='''Composition/camera: one compact plated Japanese food product, completely inside a SQUARE 1:1 canvas, desired 1024x1024 source. Three-quarter food illustration with visible top surfaces and readable front cross sections, viewed slightly from above. Center the complete food and serving surface, approximately 80 percent of canvas width and height, leaving at least 10 percent genuinely transparent margin on every side. No cropped plate, wooden tray, sushi or garnish. Designed for final export 512x512 and contain display 250x250, with a simple clean silhouette at small size. Plausible adult food proportions and coherent perspective. Local shadows only where food meets its plate or tray.
Style/medium: polished hand-drawn adult editorial cartoon matching the approved Cogna opening concept, especially its Japanese restaurant sushi illustration. Use Image 3 only for shared polished food rendering, upper-left light, edge clarity and restrained warm highlights. Confident clean muted dark-plum/brown contours, controlled cel shading, broad near-flat color regions with limited highlights. Natural appetizing salmon coral-orange with clear ivory fat bands, warm ivory rice, dark charcoal-green nori, olive/teal greens and warm wood or dark blue-teal ceramic. Fine rice detail should form grouped readable shapes rather than photographic grain. Purple only a subtle shadow accent. No plastic 3D gloss, heavy black comic outlines, painterly grain, blur, excessive shiny wet surfaces or noisy microtexture.
Transparency: genuine RGBA cutout with alpha zero outside the complete food-and-serving-surface silhouette, clean antialiased edges. Opaque food and serving-surface interiors. No backdrop, floor, ground patch, outside cast shadow, wide drop shadow, halo, glow or haze. No fake transparency checkerboard. No shadow outside the plate or tray silhouette.
Constraints: exactly ONE finished product sprite, never a sprite sheet, collage, multiple views or UI card. No people, hands, labels, lettering, Japanese characters, words, numbers, prices, badges, logos, watermarks, interface, icons, frame, border, outer ring or scattered decorative props. No background checkerboard or checkered paper. No beverages, utensils or detached reusable accessories. Food and any small garnish stay on the single serving surface. Do not copy source background, black outer circle, checkerboard, UI or text. Preserve the primary catalog food motif assigned to this ID rather than interpreting the product name as a literal recipe or changing any game data.'''

items=[
 ('PRODUCT_JAP_COMBINADO_MEDIO','Combinado médio','japa_img_1.png','jap_combinado_medio-source-01.png',
  'ONE generous salmon nigiri on ONE small shallow oval dark blue-teal ceramic plate with a thin warm ivory rim. This product uses the current catalog motif of a single salmon nigiri even though its generic catalog name is Combinado medio: preserve that motif. A compact elongated mound of white rice supports a thick curved fresh salmon slice with several ivory fat bands; one broad dark-green nori band wraps across its center. A tiny green wasabi dab rests on the salmon at the left; a small shiso leaf, two short chive blades and a few orange roe pearls sit near the far upper edge of the nori band. Keep garnish compact and ingredient scale realistic, not an exaggerated huge rice block. No other sushi, sashimi, soy bowl or additional dish. Keep the oval plate low and elegant, not a stack of plates.',
  'Um nigiri de salmão com faixa de nori, arroz, pequeno toque de wasabi, folha de shiso, cebolinha e ovas, sobre prato oval azul-petróleo com filete marfim. Preserva o motivo do sprite japa_img_1; o nome genérico não foi usado para inventar uma quantidade de combinado.'),
 ('PRODUCT_JAP_COMBINADO_PREMIUM','Combinado premium','japa_img_2.png','jap_combinado_premium-source-01.png',
  'ONE generous salmon-and-avocado temaki hand roll resting diagonally on ONE low shallow oval dark blue-teal ceramic plate with a thin warm ivory rim. Preserve the primary motif from the catalog source: a crisp charcoal-green nori cone, pointed end receding toward the upper right, open filled mouth facing the lower left and the viewer. White rice at the opening, thick coral salmon strips with ivory bands, curved pale-green avocado slices, one small shiso leaf, a restrained cluster of orange roe and green scallion rings. Keep the filling neatly contained in the cone. The temaki is complete, uncropped and has a clear tapered silhouette. No additional sushi, second temaki, soy bowl, cutlery or drink. Use the same plate materials, warm light and food rendering as the rest of this Japanese batch.',
  'Um temaki de salmão e abacate com arroz, shiso, cebolinha e ovas, cone de nori em diagonal sobre prato oval azul-petróleo. Motivo do sprite japa_img_2 preservado; composição de apresentação proposta sem receita ou quantidade novas no catálogo.'),
 ('PRODUCT_JAP_SUSHI_EXECUTIVO','Sushi executivo','japa_img_3.png','jap_sushi_executivo-source-01.png',
  'ONE compact varied sushi assortment on ONE shallow round warm bamboo serving tray seen as a clear three-quarter ellipse. Preserve the catalog motif of a mixed sushi tray: several neatly separated salmon and tuna nigiri, one yellow tamago nigiri with a slim nori strip, dark nori maki with cucumber/salmon cross sections, two rice-outside uramaki rolls with salmon/avocado centers, and one small orange-roe-topped rice roll. Arrange in orderly loose rows with enough negative space that every piece reads at 250x250; about ten clearly distinct sushi pieces rather than a crowded heap. Include one small folded pale-pink pickled-ginger rosette and a tiny green wasabi dab at opposite inner edges, both sitting directly on the tray. The tray has a restrained thin bamboo rim and a plain warm wood surface with a few broad wood lines. No checkered liner, outer black circle, spoon, chopsticks, soy-sauce dish, second plate or drink. This is one ready-to-serve product illustration, not separate sushi stickers.',
  'Seleção compacta de nigiris de salmão/atum/tamago, makis e uramakis em bandeja redonda de bambu, com gengibre e pequeno toque de wasabi. Composição simplificada do sprite japa_img_3 para leitura a 250×250, sem papel quadriculado, hashis, colher ou tigela de shoyu; quantidades visuais não alteram dados do jogo.')
]
requests=[]
for aid,name,sprite,source,subject,proposal in items:
    prompt=('Use case: stylized-concept\nAsset type: ONE isolated Japanese food product illustration for the approved Cogna business game, '+aid+'.\n'
        'Input images: Image 1 is the current catalog sprite, primary food CONTENT reference only. Image 2 is the approved Cogna opening concept, adult editorial cartoon style and Japanese-food presentation reference only. Image 3 is an approved finished food sprite from ART-17, shared rendering quality and light reference only: do not copy its burger, fries, red tray or ingredients. Never copy backgrounds, UI, text, outer rings or checkerboard from references.\nPrimary request: '+subject+'\n'+common)
    refs=['Assets/Art/Sprites/RestaurantScene/'+sprite,'art-source/references/concepts/02-abertura.png','Assets/Art/new/menu_pricing/products/lan/lan_combo_simples.png']
    requests.append({'id':aid,'name':name,'prompt':prompt,'visual_composition_proposal':proposal,
        'references':refs,'tool':'image_gen.imagegen','model':None,'model_note':'Specific model identifier is not exposed by the integrated tool.',
        'transparent_background':True,'source_path':'art-source/references/generations/ART-18/'+source})
write('art-source/editable/ART-18/generations.json',{'task_id':TASK,'date':'2026-10-14','assets':requests})

# Reuse the established technical-only export pipeline without changing ART-17.
export=(ROOT/'art-source/editable/ART-17/export-art17.cjs').read_text(encoding='utf-8')
export=export.replace('ART-17','ART-18').replace('art17','art18').replace('Produtos de lanches','Produtos japoneses').replace('User explicitly chose ART-18 products after clarification; ART-05 controls remain preserved.','Follow ART-18 Japanese products in the manifest, continuing the prior clarification about numbered product batches; ART-05 controls remain preserved.')
dest=ROOT/'art-source/editable/ART-18/export-art18.cjs';assert not dest.exists();dest.write_text(export,encoding='utf-8')
print(json.dumps({'task':TASK,'protected_files':len(protected),'requests':len(requests),'prerequisite_passed':r['passed']}))
