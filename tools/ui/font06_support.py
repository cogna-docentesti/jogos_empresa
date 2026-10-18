"""FONT-06 evidence and scoped staging helpers. Baselines remain local."""
import hashlib
import json
import pathlib
import shutil
import sys
import inspect
import re
import font05_support as common

ROOT = common.ROOT
OUT = ROOT / "output/ui-update/fonts/FONT-06"
common.OUT = OUT

def baseline():
    if (OUT / "baseline.json").exists():
        raise RuntimeError("Baseline already recorded")
    common.baseline()
    for p in (ROOT / "Assets/TextMesh Pro").rglob("*"):
        if p.is_file() and p.suffix in (".asset", ".mat", ".ttf", ".otf"):
            dst = OUT / "baseline" / p.relative_to(ROOT)
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, dst)

def scene_index():
    src=inspect.getsource(common.scene_index).replace('FONT-05','FONT-06').replace('font05_shared','font06_no_shared')
    src=src.replace('scopes={"Canvas/Panel_Identification","Canvas/Panel_Location","Canvas/Panel_Menu","Canvas/SharedComponentsPreview","Canvas/MenuAccess","Canvas/HubHistory"}', 'scopes={"Canvas/Panel_Restaurant","Canvas/Panel_MenuPricing","Canvas/Panel_Establishment","Canvas/Panel_Review"}')
    src=src.replace('Other panels, art metadata, restaurant catalogs and unsaved Editor extras are excluded.', 'All HEAD blocks outside the four panels are retained. Required facade/catalog composition dependencies are staged explicitly; the unsaved Editor extra is excluded.')
    # Prefab roots can be stripped YAML records without m_Name. Frozen component identities
    # include their child labels even when the text-only hierarchy cannot reconstruct that root.
    src=src.replace('    selected=set(selected_go)', '''    for consumer in coverage["consumers"]:
        if consumer["kind"]!="serialized" or consumer["owner"]!="FONT-06" or consumer["asset"]!="Assets/Scenes/GameScene.unity":continue
        cid=int(consumer["global_id"].split("-")[-2])
        if cid in component_go:selected_go.add(component_go[cid])
    selected=set(selected_go)''')
    scope=dict(vars(common));scope['OUT']=OUT
    exec(src,scope);scope['scene_index']()

def preservation():
    src=inspect.getsource(common.preservation).replace('FONT-05','FONT-06').replace('font05_layout','font06_layout').replace('font05_geometry','font06_geometry')
    src=src.replace('fields = {','fields = {"role",')
    src=src.replace('if "size" in layout:', 'if layout.get("native_flow_height"): data.pop("m_PreferredHeight",None)\n        if "size" in layout:')
    src=src.replace('if rid:authorized[rid]=layout', 'if rid:authorized[rid]=layout\n            for lid,raw in b.items():\n                if ref(raw,"m_GameObject")==ref(b[cid],"m_GameObject") and "m_PreferredHeight:" in raw:authorized[lid]=layout')
    scope=dict(vars(common));scope['OUT']=OUT
    exec(src,scope);scope['preservation']()

def patches():
    import difflib
    import subprocess
    name='Assets/Editor/UI/ConceptUiRevision.cs'
    a=(OUT/'baseline'/name).read_text(encoding='utf-8').splitlines(True)
    b=(ROOT/name).read_text(encoding='utf-8').splitlines(True)
    (OUT/'ConceptUiRevision.font06.patch').write_text(''.join(difflib.unified_diff(a,b,fromfile='a/'+name,tofile='b/'+name)),encoding='utf-8')
    result=subprocess.run(['git','diff','--no-index','--no-ext-diff','--',str(OUT/'scene-head.unity'),str(OUT/'scene-for-index.unity')],cwd=ROOT,capture_output=True)
    lines=result.stdout.decode('utf-8').splitlines(True)
    for i,line in enumerate(lines):
        if line.startswith('diff --git '):lines[i]='diff --git a/Assets/Scenes/GameScene.unity b/Assets/Scenes/GameScene.unity\n'
        elif line.startswith('--- '):lines[i]='--- a/Assets/Scenes/GameScene.unity\n'
        elif line.startswith('+++ '):lines[i]='+++ b/Assets/Scenes/GameScene.unity\n'
    (OUT/'scene-index.patch').write_text(''.join(lines),encoding='utf-8',newline='')
    print('Prepared scoped scene patch and FONT-06 builder delta.')

def font_cache():
    rows=[]
    for saved in (OUT/'baseline/Assets/TextMesh Pro').rglob('*'):
        if not saved.is_file(): continue
        path=saved.relative_to(OUT/'baseline'); current=ROOT/path
        before=hashlib.sha256(saved.read_bytes()).hexdigest(); after=hashlib.sha256(current.read_bytes()).hexdigest()
        if before!=after: shutil.copyfile(saved,current)
        rows.append({'path':path.as_posix(),'before':before,'observed_after_qa':after,'cache_restored':before!=after,'final':hashlib.sha256(current.read_bytes()).hexdigest()})
    report={'passed':all(r['before']==r['final'] for r in rows),'restored':sum(r['cache_restored'] for r in rows),'assets':rows}
    (OUT/'font-cache-preservation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print({'font_files':len(rows),'cache_restored':report['restored']})

def source_audit():
    baseline=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    prefixes=('Assets/Scripts/Game/Domain/','Assets/Scripts/Game/Application/','Assets/Scripts/Game/Adapter/In/Controllers/','Assets/Scripts/Game/Infrastructure/','Assets/Resources/Restaurants/','Assets/Resources/Products/')
    paths=[p for p in baseline if p.startswith(prefixes)]
    changed=[p for p in paths if (ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=baseline[p]]
    product='Assets/Scripts/Game/Adapter/In/UI/ProductPricingItemView.cs'
    before=(OUT/'baseline'/product).read_text(encoding='utf-8');after=(ROOT/product).read_text(encoding='utf-8')
    def method(src,name):
        found=re.search(r'^        (?:public|private) (?:static )?[^\n]+\b'+name+r'\([^\n]*',src,re.M)
        start=found.start();brace=src.find('{',found.start());arrow=src.find('=>',found.start())
        if arrow>=0 and arrow<brace:return src[start:src.index(';',arrow)+1]
        depth=1;end=brace+1
        while depth:
            depth+=(src[end]=='{')-(src[end]=='}');end+=1
        return src[start:end]
    methods=['TryParsePrice','Money','Number','Bind','SetReadOnly','RestoreDraftInput','OnInputChanged','OnInputEndEdit','OnSliderChanged','Decrease','Increase','SetPrice','Refresh','LateUpdate']
    same={m:method(before,m)==method(after,m) for m in methods}
    report={'passed':not changed and all(same.values()),'files_compared':len(paths),'unexpected_changes':changed,'unchanged_product_price_methods':same,'note':'Compared with pre-task bytes, including existing catalog facade changes. No production model, ID, economic calculation, parser, callback or persistence implementation changed.'}
    (OUT/'source-audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    assert report['passed'];print({'files':len(paths),'price_methods':len(same),'passed':True})

def finalize():
    import html
    execution=json.loads((OUT/'execution.json').read_text(encoding='utf-8'))
    assert execution['passed'] and execution['scenePreserved']
    saved=json.loads((OUT/'saved-verification.json').read_text(encoding='utf-8'))
    captures=json.loads((OUT/'captures.json').read_text(encoding='utf-8'))
    tests=json.loads((OUT/'edit-tests.json').read_text(encoding='utf-8'))
    suites={}
    for folder,report in [('restaurant','playmode-restaurant.json'),('menu','playmode-menu-pricing.json'),('establishment','playmode-establishment.json')]:
        d=json.loads((OUT/folder/report).read_text(encoding='utf-8')); assert d['completed'] and not d['failures']; suites[folder]=len(d['checks'])
    focused=json.loads((OUT/'focused-checks.json').read_text(encoding='utf-8'));assert focused['completed'] and not focused['failures']
    manifest=[]
    for c in captures:
        path=pathlib.Path(c['path']);raw=path.read_bytes()
        manifest.append({'name':c['name'],'path':path.relative_to(ROOT).as_posix(),'resolution':c['resolution'],'frame':c['frame'],'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
    (OUT/'capture-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    coverage_path=ROOT/'docs/ui-update/font-coverage.json';coverage=json.loads(coverage_path.read_text(encoding='utf-8'))
    measured={(r['asset'],r['path']):r for r in saved['rows']}
    observed={r['path']:r for c in captures for r in c['texts']}
    role_keys={'ScreenTitle':'screen_title','SectionTitle':'section_title','TableTotal':'table_total','MapMarker':'map_marker'}
    font_specs={'Roboto-Regular':('Roboto',400,'Regular'),'Roboto-Bold':('Roboto',700,'Bold'),'RobotoCondensed-Regular':('Roboto Condensed',400,'Regular'),'RobotoCondensed-Medium':('Roboto Condensed',500,'Medium'),'RobotoCondensed-SemiBold':('Roboto Condensed',600,'SemiBold'),'RobotoCondensed-Bold':('Roboto Condensed',700,'Bold')}
    inherited=0;active=0;missing=[]
    for c in coverage['consumers']:
        key=(c['asset'],c['hierarchy_key'])
        if c['owner']!='FONT-06':continue
        r=measured.get(key) if c['kind']=='serialized' else observed.get(c['hierarchy'])
        evidence='saved-verification.json' if c['kind']=='serialized' else 'captures.json'
        is_observed=c['kind']=='runtime' and r is not None
        if c['kind']=='runtime' and r is None:
            r=measured.get((c['asset'],c['hierarchy']))
            if r is None and '/Card_ProductItem(Clone)/' in c['hierarchy']:
                r=measured.get(('Assets/Prefabs/Card_ProductItem.prefab','Card_ProductItem/'+c['hierarchy'].split('/Card_ProductItem(Clone)/',1)[1]))
            evidence='saved-verification.json'
            inherited+=r is not None
        active+=is_observed
        if r:
            c['font06']={'font':r['font'],'role':r['role'],'final_size':r['size'],'observed_active_in_qa':is_observed,'evidence':'output/ui-update/fonts/FONT-06/'+evidence}
            family,weight,face=font_specs[r['font']]
            c['target'].update(role=role_keys.get(r['role'],r['role'].lower()),family=family,weight=weight,face=face,font_asset='Assets/TextMesh Pro/Fonts/Concept/SDF/'+r['font']+'.asset')
            c['target']['final_size']=r['size'];c['target']['size_status']='FONT-06: salvo/reaberto ou runtime real; tamanho aferido nas três viewports'
            c['migration_state']='FONT-06: aplicado serializado e reaplicado na reentrada' if c['kind']=='serialized' else 'FONT-06: binding/clone observado em runtime' if is_observed else 'FONT-06: contraparte/template serializado verificado; consumidor inativo não apresentado como captura ativa'
        else:missing.append(c['id'])
    assert not missing,missing
    coverage['status']='FONT-01 preservado; FONT-04 providers e consumidores FONT-05/06 integrados e validados; FONT-07..10 pendentes'
    coverage['font06']={'status':'concluído','serialized_consumers':saved['count'],'runtime_entries_observed':active,'runtime_entries_verified_via_saved_counterpart':inherited,'edit_tests':tests['count'],'suite_checks':suites,'focused_checks':focused['count'],'captures':len(captures),'qa':'docs/ui-update/qa/FONT-06.json'}
    coverage_path.write_text(json.dumps(coverage,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    cp=ROOT/'docs/ui-update/font-contract.json';contract=json.loads(cp.read_text(encoding='utf-8'))
    contract['font06']={'qa':'docs/ui-update/qa/FONT-06.json','sizes':'Final sizes recorded per consumer in font-coverage.json. No global size reduction.','restaurant':{'name':32,'body':24,'labels':20,'selection':20,'audience':24,'layout':'Name and description use native TMP preferred height in the existing card scroll.'},'menu':{'product':32,'price_input':24,'numeric_limits':20,'status':20,'error':20,'restaurant_context':30},'overview':{'company_heading':32,'kpi':36,'checklist_heading':22,'detail':22,'details_body':24,'layout':'Native flow heights and scroll; facade and artwork proportions retained.'}}
    cp.write_text(json.dumps(contract,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    page=['<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>FONT-06 · QA real</title><style>body{font:16px system-ui;background:#171223;color:#eee;margin:24px}h1,h2{color:#d8baff}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:24px}figure{margin:0}img{width:100%;display:block}figcaption{padding:8px}.crop{aspect-ratio:16/9;position:relative;overflow:hidden}.crop img{position:absolute;width:200%;height:200%;max-width:none}.restaurant img{left:0;top:-100%}.menu img,.establishment img{left:-100%;top:-100%}.gallery{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}a{color:#d4bcff}</style><h1>FONT-06 · restaurante, cardápio, estabelecimento e revisão</h1><p>Capturas atuais do Unity 6000.3.20f1 com SQLite isolado. Os concepts orientam hierarquia, largura e peso aparente; conteúdo, preços e cálculos são os reais do catálogo.</p>']
    comparisons=[('restaurant','02-abertura.png','restaurant/podrao-low-'),('menu','02-abertura.png','menu/podrao-'),('establishment','03-gestao.png','establishment-podrao-top-')]
    for kind,concept,prefix in comparisons:
        page.append('<h2>'+kind+'</h2>')
        for size in ['1920x1080','1280x720','1600x1000']:
            file=prefix+size+'.png';assert (OUT/file).exists(),file
            page.append('<div class="pair"><figure><div class="crop '+kind+'"><img src="../../../../art-source/references/concepts/'+concept+'"></div><figcaption>Concept '+concept+'</figcaption></figure><figure><img src="'+file+'"><figcaption>Editor real · '+size+'</figcaption></figure></div>')
    page.append('<h2>Combinações, produtos, limites, erros, consulta, detalhes e reentrada</h2><div class="gallery">')
    for c in manifest:
        file=pathlib.Path(c['path']).relative_to(OUT.relative_to(ROOT)).as_posix()
        page.append('<figure><a href="'+file+'"><img loading="lazy" src="'+file+'"></a><figcaption>'+html.escape(c['name']+' · '+c['resolution'])+'</figcaption></figure>')
    page.append('</div><p>O TMP extra “Restaurante” permanece somente na cena suja. O renderer foi ocultado apenas no Player descartável. Build e Player independente pertencem à FONT-10.</p></html>')
    (OUT/'index.html').write_text('\n'.join(page),encoding='utf-8')
    print({'serialized':saved['count'],'tests':tests['count'],'suites':suites,'focused':focused['count'],'captures':len(captures)})

def stage_list():
    paths=[
        'Assets/Editor/UI/Font06Migration.cs','Assets/Editor/UI/Font06Migration.cs.meta',
        'Assets/Editor/Tests/Font06Validation.cs','Assets/Editor/Tests/Font06Validation.cs.meta',
        'Assets/Editor/Tests/UI06RestaurantValidation.cs','Assets/Editor/Tests/UI07MenuPricingValidation.cs','Assets/Editor/Tests/UI08EstablishmentValidation.cs',
        'Assets/Editor/Tests/UI06RestaurantTests.cs',
        'Assets/Editor/UI/RestaurantUiMigration.cs','Assets/Editor/UI/MenuPricingUiMigration.cs','Assets/Editor/UI/EstablishmentUiMigration.cs',
        'Assets/Scripts/Game/Adapter/In/UI/RestaurantScreenView.cs','Assets/Scripts/Game/Adapter/In/UI/MenuPricingScreenView.cs','Assets/Scripts/Game/Adapter/In/UI/ProductPricingItemView.cs',
        'Assets/Scripts/Game/Adapter/In/UI/SelectedEstablishmentView.cs','Assets/Scripts/Game/Adapter/In/UI/EstablishmentReviewContent.cs',
        'Assets/Scripts/Game/Adapter/In/UI/ConceptDetailsToggle.cs','Assets/Scripts/Game/Adapter/In/UI/ConceptDetailsToggle.cs.meta',
        'Assets/Prefabs/Card_Generic.prefab','Assets/Prefabs/Card_ProductItem.prefab','Assets/Prefabs/Button_Segment.prefab',
        'Assets/Resources/Restaurants/REST_Frances.asset','Assets/Resources/Restaurants/REST_Japones.asset','Assets/Resources/Restaurants/REST_Lanches.asset',
        'Assets/Art/new/restaurant/street-views.meta',
        'docs/ui-update/font-coverage.json','docs/ui-update/font-contract.json','docs/ui-update/qa/FONT-06.json',
        'docs/ui-update/font-tasks/README.md','docs/ui-update/font-tasks/FONT-06-restaurante-cardapio-e-revisao.md','tools/ui/font06_support.py',
    ]
    paths += [p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets/Art/new/restaurant/street-views').iterdir() if p.is_file()]
    evidence=['execution.json','edit-tests.json','saved-verification.json','focused-checks.json','captures.json','capture-manifest.json','preservation.json','font-cache-preservation.json','commit-scene-verification.json','scene-staging.json','staging-plan.json','index.html','ConceptUiRevision.font06.patch','source-audit.json','baseline-preservation.json']
    paths += ['output/ui-update/fonts/FONT-06/'+p for p in evidence]
    for folder,report,runtime in [('restaurant','playmode-restaurant.json','runtime-scene.json'),('menu','playmode-menu-pricing.json','runtime-scene.json'),('establishment','playmode-establishment.json','runtime.json')]:
        paths += ['output/ui-update/fonts/FONT-06/'+folder+'/'+name for name in [report,runtime]]
    paths += [c['path'] for c in json.loads((OUT/'capture-manifest.json').read_text(encoding='utf-8'))]
    paths=sorted(set(paths))
    for p in paths:
        assert p.endswith(('staging-plan.json','baseline-preservation.json')) or (ROOT/p).is_file(),p
        assert '/orchestration/' not in p and not p.endswith(('.db','.patch.raw')) and '/baseline/' not in p
    report={'task':'FONT-06','paths':paths,'scene_staged_separately':'Assets/Scenes/GameScene.unity','scene_snapshot_sha256':hashlib.sha256((OUT/'scene-for-index.unity').read_bytes()).hexdigest(),
        'minimum_dependencies':{
            'scene':'Only the four FONT-06 panels, frozen consumer IDs under stripped prefab roots, prefab override records and their required FeaturedDish children. HEAD blocks outside the selection remain byte-equivalent.',
            'facades':'Three preexisting street-view PNGs/importers and catalog facade references are required by the scoped composition; economic fields and IDs retain baseline bytes.',
            'ConceptDetailsToggle':'Preexisting untracked runtime type is required by the serialized compatibility toggle. Included with its stable meta GUID.',
            'UI06RestaurantTests':'Preexisting 1448x1086 facade expectation matches the three required whole-sprite dependencies. Economic assertions retained.',
            'UI08EstablishmentValidation':'Preexisting sprite count 280 includes those three facade dependencies. Fixed-control overlap now measures the actual target rect, with all overflow, scroll and input checks retained.',
            'ConceptUiRevision':'Preexisting untracked builder remains untracked; FONT-06 changes are applied in the workspace and delivered as a patch.'},
        'excluded':'All other previous source/art/data deltas, backups, SQLite, local dependencies, baseline snapshots, raw staging patches and mutable orchestration metadata.'}
    (OUT/'staging-plan.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (OUT/'commit-paths.nul').write_bytes(b'\0'.join(p.encode('utf-8') for p in paths)+b'\0')
    baseline=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    changed=[p for p,h in baseline.items() if (ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    authorized=set(paths)|{'Assets/Scenes/GameScene.unity','Assets/Editor/UI/ConceptUiRevision.cs'}
    unexpected=[p for p in changed if p not in authorized]
    preservation={'passed':not unexpected,'baseline_files':len(baseline),'changed_paths':changed,'unexpected_changes':unexpected,'orchestration_excluded':True,'source':'Pre-task SHA-256 baseline; all unselected tracked bytes preserved.'}
    (OUT/'baseline-preservation.json').write_text(json.dumps(preservation,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    assert not unexpected,unexpected
    print({'explicit_paths':len(paths),'scene':'staged separately','baseline_files_preserved':len(baseline)-len(changed)})

if __name__ == "__main__":
    if sys.argv[1] == "baseline": baseline()
    elif sys.argv[1] == "scene-index": scene_index()
    elif sys.argv[1] == "preservation": preservation()
    elif sys.argv[1] == "patches": patches()
    elif sys.argv[1] == "font-cache": font_cache()
    elif sys.argv[1] == "finalize": finalize()
    elif sys.argv[1] == "source-audit": source_audit()
    elif sys.argv[1] == "stage-list": stage_list()
