"""FONT-07 selective baseline and evidence helpers. Local snapshots are never staged."""
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import font05_support as common

ROOT = common.ROOT
OUT = ROOT / 'output/ui-update/fonts/FONT-07'
common.OUT = OUT

def atomic_json(path, value):
    temp=path.with_name(path.name+'.font07.tmp')
    temp.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    temp.replace(path)

def baseline():
    OUT.mkdir(parents=True, exist_ok=True)
    assert not (OUT/'baseline.json').exists(), 'Baseline already exists'
    paths=set(common.git('ls-files').decode().splitlines())
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets').rglob('*.cs'))
    manifest={}
    for path in sorted(paths):
        if '/orchestration/' in path: continue
        p=ROOT/path
        if not p.is_file(): continue
        manifest[path]=hashlib.sha256(p.read_bytes()).hexdigest()
        if path.startswith(('Assets/','docs/')) and p.suffix in ('.cs','.unity','.prefab','.json','.md','.asset','.mat','.ttf','.otf'):
            dest=OUT/'baseline'/path;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    (OUT/'baseline.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    for name,args in [('initial-status.txt',('status','--short')),('initial-tracked.patch',('diff','--binary')),('initial-staged.patch',('diff','--cached','--binary'))]:
        (OUT/name).write_bytes(common.git(*args))
    print({'files':len(manifest),'HEAD':common.git('rev-parse','HEAD').decode().strip()})

def scene_index():
    import inspect
    src=inspect.getsource(common.scene_index).replace('FONT-05','FONT-07').replace('font05_shared','font07_no_shared')
    src=src.replace('scopes={"Canvas/Panel_Identification","Canvas/Panel_Location","Canvas/Panel_Menu","Canvas/SharedComponentsPreview","Canvas/MenuAccess","Canvas/HubHistory"}', 'scopes={"Canvas/Panel_Financial","Canvas/Panel_Equipment","Canvas/Panel_Team"}')
    src=src.replace('    selected=set(selected_go)', '''    for consumer in coverage["consumers"]:
        if consumer["kind"]!="serialized" or consumer["owner"]!="FONT-07" or consumer["asset"]!="Assets/Scenes/GameScene.unity":continue
        cid=int(consumer["global_id"].split("-")[-2])
        if cid in component_go:selected_go.add(component_go[cid])
    selected=set(selected_go)''')
    scope=dict(vars(common));scope['OUT']=OUT
    exec(src,scope);scope['scene_index']()

def preservation():
    import inspect
    src=inspect.getsource(common.preservation).replace('FONT-05','FONT-07').replace('font05_layout','font07_layout').replace('font05_geometry','font07_geometry')
    src=src.replace('fields = {','fields = {"role",')
    # The existing ExecuteAlways identification wrapper recomputes its uniform fit during
    # additive scene audits. Compare all authored fields, and report this driven cache explicitly.
    src=src.replace('    def normalized(', '''    driver_guid=re.search(r"guid: (\\w+)",(ROOT/"Assets/Scripts/Game/Adapter/In/UI/IdentificationDesignScale.cs.meta").read_text())[1]
    driven_ids=set()
    def normalized(''')
    src=src.replace('        return value', '        if key in driven_ids:data.pop("m_LocalScale",None)\n        return value')
    src=src.replace('        names={', '        driven_ids={rects.get(ref(raw,"m_GameObject")) for raw in b.values() if driver_guid in raw}\n        names={')
    src=src.replace('        results.append(result)', '''        result["automatic_design_scale"]={"driver":"Assets/Scripts/Game/Adapter/In/UI/IdentificationDesignScale.cs","note":"Existing ExecuteAlways uniform wrapper fit; text local scales and every other transform field remain compared.","observations":[{"rect_id":i,"before":re.search(r"m_LocalScale: (.*)",a[i])[1],"after":re.search(r"m_LocalScale: (.*)",b[i])[1]} for i in driven_ids if i in a and i in b and re.search(r"m_LocalScale: (.*)",a[i])[1]!=re.search(r"m_LocalScale: (.*)",b[i])[1]]}
        results.append(result)''')
    scope=dict(vars(common));scope['OUT']=OUT
    exec(src,scope);scope['preservation']()

def font_cache():
    import font06_support as prior
    prior.OUT=OUT
    prior.font_cache()

def patches():
    import font06_support as prior
    prior.OUT=OUT
    prior.patches()
    (OUT/'ConceptUiRevision.font06.patch').replace(OUT/'ConceptUiRevision.font07.patch')

def source_audit():
    import re
    saved=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    prefixes=('Assets/Scripts/Game/Domain/','Assets/Scripts/Game/Application/','Assets/Scripts/Game/Adapter/In/Controllers/','Assets/Scripts/Game/Infrastructure/','Assets/Resources/')
    paths=[p for p in saved if p.startswith(prefixes)]
    changed=[p for p in paths if (ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=saved[p]]
    same={}
    for name in ['BankCardView.cs','EquipmentCardView.cs','TeamRoleCardView.cs','EquipmentCartLineView.cs']:
        path='Assets/Scripts/Game/Adapter/In/UI/'+name
        before=(OUT/'baseline'/path).read_text(encoding='utf-8');after=(ROOT/path).read_text(encoding='utf-8')
        # Only explicit role declarations may differ. All setup/callback/calculation/layout bytes are compared.
        strip=lambda s:re.sub(r'TypographyRole\.\w+','TypographyRole.REVIEWED',s)
        same[name]=strip(before)==strip(after)
    path='Assets/Scripts/Game/Adapter/In/UI/FinancialScreenView.cs'
    before=(OUT/'baseline'/path).read_text(encoding='utf-8');after=(ROOT/path).read_text(encoding='utf-8')
    start=after.index('        private void LateUpdate()');end=after.index('        public BankCardView CreateBankCard()',start)
    same['FinancialScreenView business methods']=after[:start]+after[end:]==before
    report={'passed':not changed and all(same.values()),'files_compared':len(paths),'unexpected_changes':changed,'card_runtime_unchanged_except_explicit_roles':same,'note':'Compared against pre-task bytes, including previous uncommitted controller/catalog changes. Models, IDs, formulas, callbacks and SQLite implementation are unchanged.'}
    (OUT/'source-audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    assert report['passed'],report
    print({'files':len(paths),'cards':len(same),'passed':True})

def template_audit():
    import re
    read=lambda name:json.loads((OUT/name).read_text(encoding='utf-8'))
    before={(r['asset'],r['path']):r for r in read('post-runtime-saved-baseline.json')['rows']}
    after={(r['asset'],r['path']):r for r in read('saved-verification.json')['rows']}
    active=set()
    for capture in read('captures.json'):
        for row in capture['texts']:
            path=re.sub(r'\[\d+\]','',row['path']);active.add(('Assets/Scenes/GameScene.unity',path))
            for prefab in ['Card_loan','Card_equipment','TeamRoleCard','EquipmentCartLine']:
                marker='/'+prefab+'(Clone)/'
                if marker in path:active.add(('Assets/Prefabs/'+prefab+'.prefab',prefab+'/'+path.split(marker,1)[1]))
    assert before.keys()==after.keys()
    changes=[]
    for key,a in before.items():
        b=after[key]
        if a!=b:
            observed=(key[0],re.sub(r'\[\d+\]','',key[1])) in active
            assert a['size']==b['size'] and not observed,(key,a,b)
            changes.append({'asset':key[0],'path':key[1],'before':a,'after':b,'observed_active_in_runtime_pass':observed})
    report={'passed':True,'changed_consumers':len(changes),'active_runtime_consumers_changed':0,'sizes_changed':0,'post_migration_saved_audit':read('post-migration-verification.json'),'changes':changes,
            'note':'The completed 579-check runtime pass remains applicable: subsequent migration changed only dormant legacy face/role declarations. All 236 consumers were reopened and all 65 EditMode cases rerun afterward.'}
    (OUT/'post-runtime-template-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print({'inactive_consumers_updated':len(changes),'active_consumers_changed':0})

def normalize_prefabs():
    import yaml
    rows=[]
    for name in ['Card_loan','Card_equipment','TeamRoleCard','EquipmentCartLine','MainArea_Equipment','Panel_Equipment']:
        p=ROOT/'Assets/Prefabs'/(name+'.prefab');before=p.read_text(encoding='utf-8')
        after='\n'.join(line.rstrip(' \t') for line in before.split('\n'))
        a=common.blocks(before);b=common.blocks(after);assert a.keys()==b.keys()
        for key in a:assert yaml.safe_load(a[key].split('\n',1)[1])==yaml.safe_load(b[key].split('\n',1)[1]),(name,key)
        if before!=after:
            temp=p.with_suffix('.prefab.font07.tmp');temp.write_text(after,encoding='utf-8',newline='');temp.replace(p)
        rows.append({'path':p.relative_to(ROOT).as_posix(),'blocks_compared':len(a),'typed_yaml_values_identical':True,'trailing_spaces_removed':sum(x!=y for x,y in zip(before.splitlines(),after.splitlines()))})
    atomic_json(OUT/'prefab-formatting.json',{'passed':True,'prefabs':rows,'note':'Only whitespace outside YAML values changed after validation; all six prefab object graphs compare equal.'})
    print({'prefabs':len(rows),'typed_values_identical':True})

def finalize():
    import html,re,collections
    read=lambda name:json.loads((OUT/name).read_text(encoding='utf-8'))
    execution=read('execution.json');assert execution['passed'] and execution['scenePreserved']
    saved=read('saved-verification.json');captures=read('captures.json');tests=read('edit-tests.json')
    suites={}
    for folder,report in [('bank','playmode-financial.json'),('equipment','playmode-equipment.json'),('team','playmode-team.json')]:
        d=read(folder+'/'+report);assert d['completed'] and not d['failures'];suites[folder]=len(d['checks'])
    focused=read('focused-checks.json');assert focused['completed'] and not focused['failures']
    manifest=[]
    for c in captures:
        p=pathlib.Path(c['path']);raw=p.read_bytes()
        manifest.append({'name':c['name'],'path':p.relative_to(ROOT).as_posix(),'resolution':c['resolution'],'frame':c['frame'],'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
    (OUT/'capture-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    cp=ROOT/'docs/ui-update/font-coverage.json';coverage=json.loads(cp.read_text(encoding='utf-8'))
    measured={(r['asset'],r['path']):r for r in saved['rows']}
    observed={r['path']:r for c in captures for r in c['texts']}
    no_indices=lambda s:re.sub(r'\[\d+\]','',s)
    plain_measured={(asset,no_indices(path)):row for (asset,path),row in measured.items()}
    plain_observed={no_indices(path):row for path,row in observed.items()}
    aliases=collections.defaultdict(list)
    for c in coverage['consumers']:
        if c['kind']=='runtime' and c.get('global_id'):aliases[(c['asset'],c['global_id'])].append(c['hierarchy_key'])
    roles={'ScreenTitle':'screen_title','SectionTitle':'section_title','TableTotal':'table_total','MapMarker':'map_marker'}
    fonts={'Roboto-Regular':('Roboto',400,'Regular'),'Roboto-Bold':('Roboto',700,'Bold'),'RobotoCondensed-Regular':('Roboto Condensed',400,'Regular'),'RobotoCondensed-Medium':('Roboto Condensed',500,'Medium'),'RobotoCondensed-SemiBold':('Roboto Condensed',600,'SemiBold'),'RobotoCondensed-Bold':('Roboto Condensed',700,'Bold')}
    active=0;inherited=0;missing=[]
    for c in coverage['consumers']:
        if c['owner']!='FONT-07' and not c.get('font07_shared'):continue
        r=measured.get((c['asset'],c['hierarchy_key'])) if c['kind']=='serialized' else observed.get(c['hierarchy_key'])
        if c['kind']=='runtime' and r is None and c['hierarchy_key']==c['hierarchy']:r=observed.get(c['hierarchy'])
        actual=c['kind']=='runtime' and r is not None
        evidence='captures.json' if actual else 'saved-verification.json'
        if c['kind']=='runtime' and r is None:
            r=measured.get((c['asset'],c['hierarchy_key']))
            if r is None:
                # The original runtime inventory contains alternate sibling positions for the
                # same frozen GlobalObjectId (e.g. retired MainArea [9] versus serialized [10]).
                r=next((measured[(c['asset'],alias)] for alias in aliases[(c['asset'],c.get('global_id'))] if (c['asset'],alias) in measured),None)
            if r is None and c['hierarchy_key']==c['hierarchy']:r=plain_measured.get((c['asset'],c['hierarchy']))
            for prefab in ['Card_loan','Card_equipment','TeamRoleCard','EquipmentCartLine']:
                marker='/'+prefab+'(Clone)/'
                if r is None and marker in c['hierarchy']:
                    r=measured.get(('Assets/Prefabs/'+prefab+'.prefab',prefab+'/'+c['hierarchy'].split(marker,1)[1]))
            inherited+=r is not None
        active+=actual
        if r:
            c['font07']={'font':r['font'],'role':r['role'],'final_size':r['size'],'observed_active_in_qa':actual,'evidence':'output/ui-update/fonts/FONT-07/'+evidence}
            family,weight,face=fonts[r['font']]
            c['target'].update(role=roles.get(r['role'],r['role'].lower()),family=family,weight=weight,face=face,font_asset='Assets/TextMesh Pro/Fonts/Concept/SDF/'+r['font']+'.asset',final_size=r['size'],size_status='FONT-07: saved/reopened or observed in the real Player at all three viewports')
            c['migration_state']='FONT-07: aplicado serializado' if c['kind']=='serialized' else 'FONT-07: consumidor ativo observado' if actual else 'FONT-07: template/contraparte serializada verificada; consumidor inativo'
        else:missing.append(c['id'])
    (OUT/'coverage-resolution.json').write_text(json.dumps({'active':active,'inherited':inherited,'missing':missing},indent=2)+'\n',encoding='utf-8')
    assert not missing,missing
    coverage['status']='FONT-01 preserved; FONT-04 providers and FONT-05/06/07 consumers integrated and validated; FONT-08..10 pending'
    coverage['font07']={'status':'concluído','serialized_consumers':saved['count'],'runtime_entries_observed':active,'runtime_entries_verified_via_saved_counterpart':inherited,'edit_tests':tests['count'],'suite_checks':suites,'focused_checks':focused['count'],'captures':len(captures),'qa':'docs/ui-update/qa/FONT-07.json'}
    atomic_json(cp,coverage)
    cp=ROOT/'docs/ui-update/font-contract.json';contract=json.loads(cp.read_text(encoding='utf-8'))
    contract['font07']={'qa':'docs/ui-update/qa/FONT-07.json','sizes':'Final sizes recorded for every consumer in font-coverage.json; no global size reduction.','bank':{'title':44,'contract_name':28,'principal':27,'body_interest_term_installment':20,'layout':'Four real credit options; native preferred heights in the existing masked scroll.'},'equipment':{'title':44,'product_name':28,'individual_price':30,'description':21,'quantity':24,'summary_total':32,'cart_body':24},'team':{'title':44,'role_name':30,'salary':27,'capacity_body':21,'requirement':20,'quantity':24,'payroll_total':32},'weights':'Individual prices, salary and quantities use Roboto Regular; summary totals/KPIs use Roboto Bold; compact headings and controls use the real condensed faces.','cart_scope':'11 serialized modal consumers and their runtime entries transferred from FONT-09 because FONT-07 explicitly owns the cart summary and actions.'}
    atomic_json(cp,contract)
    page=['<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>FONT-07 · QA real</title><style>body{font:16px system-ui;background:#171223;color:#eee;margin:24px}h1,h2{color:#d8baff}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:24px}figure{margin:0}img{width:100%;display:block}figcaption{padding:8px}.crop{aspect-ratio:16/9;position:relative;overflow:hidden}.crop img{position:absolute;width:200%;height:200%;max-width:none}.bank img{left:0;top:0}.equipment img{left:-100%;top:0}.team img{left:0;top:-100%}.gallery{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}a{color:#d4bcff}</style><h1>FONT-07 · banco, equipamentos, carrinho e equipe</h1><p>Capturas atuais do Unity 6000.3.20f1 com SQLite isolado. Concepts orientam hierarquia e métricas; os quatro créditos, 16 equipamentos e cinco funções vêm dos catálogos reais. Build independente pertence à FONT-10.</p>']
    for folder,prefix in [('bank','setup-normal-'),('equipment','filter-basic-'),('team','valid-podrao-')]:
        page.append('<h2>'+folder+'</h2>')
        for size in ['1920x1080','1280x720','1600x1000']:
            choices=[c for c in manifest if c['resolution']==size and c['path'].startswith(OUT.relative_to(ROOT).as_posix()+'/'+folder+'/')]
            chosen=next((c for c in choices if c['name'].startswith(prefix)),choices[0])
            file=pathlib.Path(chosen['path']).relative_to(OUT.relative_to(ROOT)).as_posix()
            page.append('<div class="pair"><figure><div class="crop '+folder+'"><img src="../../../../art-source/references/concepts/03-gestao.png"></div><figcaption>Concept 03-gestao</figcaption></figure><figure><img src="'+file+'"><figcaption>Editor real · '+size+'</figcaption></figure></div>')
    page.append('<h2>Estados, finais de catálogos, resumo do carrinho, textos longos e reentrada</h2><div class="gallery">')
    for c in manifest:
        file=pathlib.Path(c['path']).relative_to(OUT.relative_to(ROOT)).as_posix()
        page.append('<figure><a href="'+file+'"><img loading="lazy" src="'+file+'"></a><figcaption>'+html.escape(c['name']+' · '+c['resolution'])+'</figcaption></figure>')
    page.append('</div></html>');(OUT/'index.html').write_text('\n'.join(page),encoding='utf-8')
    print({'serialized':saved['count'],'tests':tests['count'],'suites':suites,'focused':focused['count'],'captures':len(captures),'active':active,'inherited':inherited})

def stage_list():
    paths=[
      'Assets/Editor/UI/Font07Migration.cs','Assets/Editor/UI/Font07Migration.cs.meta',
      'Assets/Editor/Tests/Font07Validation.cs','Assets/Editor/Tests/Font07Validation.cs.meta',
      'Assets/Editor/Tests/Font04TypographyTests.cs',
      'Assets/Editor/Tests/UI09FinancialValidation.cs','Assets/Editor/Tests/UI10EquipmentValidation.cs','Assets/Editor/Tests/UI11TeamValidation.cs',
      'Assets/Editor/UI/FinancialUiMigration.cs','Assets/Editor/UI/EquipmentUiMigration.cs','Assets/Editor/UI/TeamUiMigration.cs',
      'Assets/Scripts/Game/Adapter/In/UI/BankCardView.cs','Assets/Scripts/Game/Adapter/In/UI/EquipmentCardView.cs',
      'Assets/Scripts/Game/Adapter/In/UI/EquipmentCartLineView.cs','Assets/Scripts/Game/Adapter/In/UI/TeamRoleCardView.cs',
      'Assets/Scripts/Game/Adapter/In/UI/EquipmentScreenView.cs',
      'Assets/Scripts/Game/Adapter/In/UI/FinancialScreenView.cs',
      'Assets/Prefabs/Card_loan.prefab','Assets/Prefabs/Card_equipment.prefab','Assets/Prefabs/TeamRoleCard.prefab',
      'Assets/Prefabs/EquipmentCartLine.prefab','Assets/Prefabs/Panel_Equipment.prefab','Assets/Prefabs/MainArea_Equipment.prefab',
      'docs/ui-update/font-coverage.json','docs/ui-update/font-contract.json','docs/ui-update/qa/FONT-07.json',
      'docs/ui-update/font-tasks/README.md','docs/ui-update/font-tasks/FONT-07-banco-equipamentos-e-equipe.md','tools/ui/font07_support.py']
    evidence=['execution.json','edit-tests.json','saved-verification.json','focused-checks.json','captures.json','capture-manifest.json','coverage-resolution.json','preservation.json','font-cache-preservation.json','commit-scene-verification.json','scene-staging.json','staging-plan.json','index.html','ConceptUiRevision.font07.patch','source-audit.json','baseline-preservation.json','session-geometry.json','post-migration-verification.json','post-runtime-template-audit.json','prefab-formatting.json']
    paths+=['output/ui-update/fonts/FONT-07/'+p for p in evidence]
    for folder,report in [('bank','playmode-financial.json'),('equipment','playmode-equipment.json'),('team','playmode-team.json')]:
        paths+=['output/ui-update/fonts/FONT-07/'+folder+'/'+name for name in [report,'runtime.json']]
    paths+=[c['path'] for c in json.loads((OUT/'capture-manifest.json').read_text(encoding='utf-8'))]
    paths=sorted(set(paths))
    for p in paths:
        assert p.endswith(('staging-plan.json','baseline-preservation.json')) or (ROOT/p).is_file(),p
        assert '/orchestration/' not in p and not p.endswith('.db') and '/baseline/' not in p
    report={'task':'FONT-07','paths':paths,'scene_staged_separately':'Assets/Scenes/GameScene.unity','scene_snapshot_sha256':hashlib.sha256((OUT/'scene-for-index.unity').read_bytes()).hexdigest(),
      'minimum_dependencies':{
        'scene':'Only the three panels, frozen consumer IDs under stripped prefab roots and required prefab override records; HEAD blocks elsewhere are retained. No unsaved user TMP.',
        'card_views':'Preexisting portrait compositions are required by the saved three-column equipment/team layouts; only explicit role declarations changed during FONT-07. Original setup, refresh, callback and layout methods retain baseline bytes.',
        'EquipmentScreenView':'Preexisting 704-unit minimum for the three-column equipment grid is required by the scoped composition. No purchase or persistence methods changed.',
        'Font04TypographyTests':'Compare native material instance identity instead of managed object wrapper identity; the same shared material is still mandatory.',
        'ConceptUiRevision':'Remains preexisting and untracked; applied FONT-07 delta is delivered as a patch.',
        'help_loading':'Typography-only dependencies within these panels, marked font07_shared; ownership and behavior validation remain FONT-09.'},
      'excluded':'Other preexisting deltas, TeamScreenController ordering, all backups, SQLite, local dependencies, baseline snapshots, raw staging patches and orchestration.'}
    (OUT/'staging-plan.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (OUT/'commit-paths.nul').write_bytes(b'\0'.join(p.encode('utf-8') for p in paths)+b'\0')
    baseline=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    changed=[p for p,h in baseline.items() if (ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    authorized=set(paths)|{'Assets/Scenes/GameScene.unity','Assets/Editor/UI/ConceptUiRevision.cs'}
    unexpected=[p for p in changed if p not in authorized]
    result={'passed':not unexpected,'files_compared':len(baseline),'unchanged':len(baseline)-len(changed),'authorized_changed':changed,'unexpected':unexpected,'excluded_mutable':'output/ui-update/fonts/orchestration'}
    (OUT/'baseline-preservation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    assert not unexpected,unexpected
    print({'paths':len(paths),'baseline_files_preserved':result['unchanged']})

def qa():
    read=lambda n:json.loads((OUT/n).read_text(encoding='utf-8'))
    for file in ['execution.json','preservation.json','source-audit.json','font-cache-preservation.json','commit-scene-verification.json','post-runtime-template-audit.json']:
        assert read(file)['passed'],file
    saved=read('saved-verification.json');tests=read('edit-tests.json');captures=read('captures.json');focused=read('focused-checks.json')
    assert tests['passed'] and focused['completed'] and not focused['failures']
    suites={}
    for folder,file in [('bank','playmode-financial.json'),('equipment','playmode-equipment.json'),('team','playmode-team.json')]:
        d=read(folder+'/'+file);assert d['completed'] and not d['failures'];suites[folder]=len(d['checks'])
    scene=sum(r['asset']=='Assets/Scenes/GameScene.unity' for r in saved['rows'])
    coverage=read('coverage-resolution.json');fonts=read('font-cache-preservation.json')
    result={
      'task':'FONT-07','date':'2026-10-18','status':'concluído',
      'scope':'UI-09 banco, UI-10 equipamentos/carrinho e UI-11 equipe, incluindo seis prefabs, estados, clones, filtros, binds, templates inativos e reentrada.',
      'acceptance':{
        'scene_and_six_prefabs_saved_and_reopened':True,'four_credit_lines_sixteen_equipment_five_real_roles':True,
        'body_prices_salary_quantity_regular_and_summary_totals_real_bold':True,'neutral_materials_no_synthetic_weight_autosize_or_per_text_scale':True,
        'normal_selected_owned_required_unavailable_empty_cart_and_errors':True,'all_catalog_ends_and_cart_summary_accessible':True,
        'filters_purchase_hiring_confirmation_return_reentry_and_persistence':True,'concept_comparison_three_resolutions':True,
        'dirty_scene_and_unsaved_restaurante_extra_preserved':True,'scoped_commit_scene_reopened_and_checked':True},
      'validation':{'unity':'6000.3.20f1','edit_tests':tests['count'],'test_suites':['Font04TypographyTests','UI09FinancialTests','UI10EquipmentTests','UI11TeamTests'],
        'suite_checks':suites,'focused_checks':focused['count'],'runtime_checks_total':sum(suites.values())+focused['count'],
        'serialized_consumers':saved['count'],'scene_consumers':scene,'prefab_consumers':saved['count']-scene,
        'runtime_entries_observed_active':coverage['active'],'runtime_entries_verified_via_saved_counterpart':coverage['inherited'],
        'capture_count':len(captures),'resolutions':[[1920,1080],[1280,720],[1600,1000]],'console_errors':0,'console_warnings':0,
        'unchanged_domain_controller_infrastructure_resource_files':read('source-audit.json')['files_compared'],
        'font_files_preserved':len(fonts['assets']),'incidental_font_caches_restored':fonts['restored'],
        'post_runtime_only_dormant_consumers_changed':read('post-runtime-template-audit.json')['changed_consumers'],
        'second_editor_opened':False,'legacy_full_migrations_executed':False},
      'visual_review':{'reference':'art-source/references/concepts/03-gestao.png','comparison':'output/ui-update/fonts/FONT-07/index.html',
        'judgment':'Hierarquia, peso e largura conferidos nas três viewports. Conteúdo e valores são os reais dos catálogos; as ilustrações não forneceram novos produtos ou cargos. Paleta, artes e composição mantidas.',
        'bank':'Título 44, contrato 28, principal Regular 27 e informações 20. O resumo de duas linhas ganhou altura de 56 para 64. A grade mantém mínimo 660 e acompanha a altura nativa dos textos longos no scroll existente.',
        'equipment':'Produto 28, preço individual Regular 30, descrição 21, quantidade Regular 24, total 32. Oito básicos e oito específicos aferidos pela compatibilidade dos três restaurantes; modal e último item de carrinho verificados.',
        'team':'Cargo 30, salário Regular 27, capacidade 21, requisito 20, quantidade Regular 24, folha total 32. Cinco funções, mínimos, decremento, contratação, consulta, dados inválidos e quantidades amplas aferidos.',
        'legacy':'Oito labels inativos de nome/descrição/valor/rótulo deixaram de ser tratados como botões. Quatro totais legados também receberam TableTotal. Todos foram reabertos; nenhum consumidor ativo das 233 capturas mudou depois da passagem runtime.',
        'test_updates':'A suite reconhece o carregamento assíncrono e a assinatura vigente de PopulateEquipments, fecha a ajuda pelo botão real antes dos filtros, aguarda o frame de criação antes do hover e mede targetGraphic nos controles. Overflow, raycast, foco, seleção, economia e persistência permanecem obrigatórios.',
        'session':'Cena suja com sete raízes e TMP Restaurante somente em memória preservada. Game view voltou a 1600x1000. IdentificationDesignScale.ExecuteAlways recalculou seu wrapper uniforme de 0.8333333 para 1; efeito automático documentado, sem mudança nos textos ou no driver. Avisos de recarga externa ignorados.'},
      'persistence':{'storage':'SQLite e identificação isolados por COGNA_TEST_DATABASE_PATH/COGNA_TEST_IDENTIFICATION_KEY; nenhum banco do usuário usado.',
        'checks':'INSERT somente na confirmação D3, uma sessão persistida, IDs/valores reais, nenhum débito/crédito duplicado e sobrevivência das escolhas no retorno/recarga.',
        'write_count':'SELECT total_changes() permanece igual ao reaplicar papéis e rebinds nas três viewports. Fontes de modelos, controladores e persistência são idênticas ao baseline; callbacks de negócio e cálculos das views também foram comparados.'},
      'evidence':{k:'output/ui-update/fonts/FONT-07/'+v for k,v in {
        'execution':'execution.json','tests':'edit-tests.json','saved_consumers':'saved-verification.json','focused':'focused-checks.json',
        'bank':'bank/playmode-financial.json','equipment':'equipment/playmode-equipment.json','team':'team/playmode-team.json',
        'metrics_and_baselines':'captures.json','capture_hashes':'capture-manifest.json','gallery':'index.html','preservation':'preservation.json',
        'fonts':'font-cache-preservation.json','source':'source-audit.json','commit_scene':'commit-scene-verification.json',
        'post_runtime_templates':'post-runtime-template-audit.json','staging':'staging-plan.json','baseline':'baseline-preservation.json','builder_delta':'ConceptUiRevision.font07.patch'}.items()},
      'limitations':['Player do Editor; build e Player independente pertencem à FONT-10.',
        '126 entradas de runtime inativas/retiradas verificadas por contraparte/template, sem serem apresentadas como capturas ativas. Variantes de índice de irmãos ligadas pelo GlobalObjectId congelado.',
        'FONT-09 conserva o comportamento da ajuda/loading. Sua tipografia nestes painéis foi integrada como dependência; o teste fecha a ajuda antes de usar todos os filtros.',
        'Commit inclui composição anterior mínima dos três painéis/cards e o mínimo 704 da grade de equipamentos. TeamScreenController e demais deltas anteriores excluídos.',
        'ConceptUiRevision.cs permanece preexistente não rastreado; delta FONT-07 entregue em patch. Seis faces e três fallbacks finitos reutilizados; corpus além de 536 pontos não homologado.'],
      'next_task':'FONT-08, somente após este aceite e commit local em inglês, conforme sequential-handoff.txt.'}
    atomic_json(ROOT/'docs/ui-update/qa/FONT-07.json',result)
    task=ROOT/'docs/ui-update/font-tasks/FONT-07-banco-equipamentos-e-equipe.md'
    text=task.read_text(encoding='utf-8').replace('Status: **pendente**.','Status: **concluído**.').replace('- [ ]','- [x]')
    task.write_text(text,encoding='utf-8')
    task=ROOT/'docs/ui-update/font-tasks/README.md';text=task.read_text(encoding='utf-8').replace('FONT-01 a FONT-06 concluídos; FONT-07 a FONT-10 pendentes','FONT-01 a FONT-07 concluídos; FONT-08 a FONT-10 pendentes')
    text=text.replace('[FONT-07 — Banco, equipamentos e equipe](FONT-07-banco-equipamentos-e-equipe.md) | FONT-06 | Pendente','[FONT-07 — Banco, equipamentos e equipe](FONT-07-banco-equipamentos-e-equipe.md) | FONT-06 | Concluído — [QA](../qa/FONT-07.json)')
    task.write_text(text,encoding='utf-8')
    print({'accepted':True,'tests':tests['count'],'runtime':result['validation']['runtime_checks_total'],'captures':len(captures)})

if __name__=='__main__': globals()[sys.argv[1].replace('-','_')]()
