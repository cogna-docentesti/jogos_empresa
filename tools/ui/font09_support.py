"""FONT-09 selective preservation, inventory and evidence helpers."""
import hashlib
import json
import pathlib
import shutil
import sys
import font05_support as common

ROOT = common.ROOT
OUT = ROOT / 'output/ui-update/fonts/FONT-09'

def atomic_json(path, value):
    temp = path.with_name(path.name + '.font09.tmp')
    temp.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    temp.replace(path)

def baseline():
    OUT.mkdir(parents=True, exist_ok=True)
    assert not (OUT/'baseline.json').exists(), 'Baseline already exists'
    paths = set(common.git('ls-files').decode().splitlines())
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets').rglob('*.cs'))
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets/TextMesh Pro').rglob('*') if p.is_file())
    manifest = {}
    for path in sorted(paths):
        if '/orchestration/' in path: continue
        p=ROOT/path
        if not p.is_file(): continue
        manifest[path]=hashlib.sha256(p.read_bytes()).hexdigest()
        if path.startswith(('Assets/', 'docs/')):
            dest=OUT/'baseline'/path
            dest.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(p,dest)
    atomic_json(OUT/'baseline.json',manifest)
    for name,args in [('initial-status.txt',('--no-optional-locks','status','--short')),('initial-tracked.patch',('diff','--binary')),('initial-staged.patch',('diff','--cached','--binary'))]:
        (OUT/name).write_bytes(common.git(*args))
    print({'files':len(manifest),'HEAD':common.git('rev-parse','HEAD').decode().strip()})

def prepare():
    coverage_path=ROOT/'docs/ui-update/font-coverage.json'
    coverage=json.loads(coverage_path.read_text(encoding='utf-8'))
    # These are explicit inventory identities. Explanatory effects use the body face
    # in both the saved template and clones; the compact options heading is a title.
    for c in coverage['consumers']:
        if c['owner']!='FONT-09': continue
        if c['hierarchy_key'].endswith('/OptionsHeading'): c['target']['role']='section_title'
    atomic_json(coverage_path,coverage)
    src=(ROOT/'Assets/Editor/UI/Font08Migration.cs').read_text(encoding='utf-8').replace('FONT-08','FONT-09').replace('Font08','Font09').replace('font08','font09')
    src=src.replace('((string)c["owner"] == "FONT-09" || (bool?)c["font09_shared"] == true)', '(string)c["owner"] == "FONT-09"')
    src=src.replace('else if (action == "bankruptcy") Font09Validation.RunBankruptcyAndFocused();','else if (action == "focused") Font09Validation.RunFocused();')
    (ROOT/'Assets/Editor/UI/Font09Migration.cs').write_text(src,encoding='utf-8')
    src=(ROOT/'Assets/Editor/Tests/Font08Validation.cs').read_text(encoding='utf-8').replace('FONT-08','FONT-09').replace('Font08','Font09').replace('font08','font09')
    src=src.replace('RunBankruptcyAndFocused','RunFocused')
    src=src.replace('typeof(UI13MonthlyResultTests), typeof(UI14QuarterlyResultTests), typeof(UI15FinalReportTests), typeof(UI16FinalReportTests)','typeof(UI12EventTests), typeof(UI18DialogTests)')
    src=src.replace('typeof(UI13MonthlyResultValidation),typeof(UI14QuarterlyResultValidation),typeof(UI15FinalReportValidation),typeof(UI16BankruptcyValidation)', 'typeof(UI12EventValidation),typeof(UI17FeedbackValidation),typeof(UI18DialogValidation)')
    src=src.replace('new[] {"monthly", "quarterly", "final", "bankruptcy"}', 'new[] {"events", "feedback", "dialogs"}')
    src=src.replace('{"playmode.json","playmode.json","playmode.json","playmode.json"}', '{"playmode-events.json","playmode.json","playmode.json"}')
    src=src.replace('if(suite==0)UI13MonthlyResultValidation.Run(); else if(suite==1)UI14QuarterlyResultValidation.Run();else if(suite==2)UI15FinalReportValidation.Run();else UI16BankruptcyValidation.Run();', 'if(suite==0)UI12EventValidation.Run(); else if(suite==1)UI17FeedbackValidation.Run();else UI18DialogValidation.Run();')
    src=src.replace('if(++suite<4)', 'if(++suite<3)')
    # A focused retry is useful during layout iteration; it cannot claim old captures.
    a=src.index('        if(SessionState.GetBool(Key+"RetainPassedSuites",false))',src.index('static void Reload'))
    b=src.index('        isolationDeadline=',a)
    src=src[:a]+src[b:]
    src=src.replace('if(stage==-1){suite=SessionState.GetBool(Key+"RetainPassedSuites",false)?3:0;StartSuite();stage=1;return;}', 'if(stage==-1){if(SessionState.GetBool(Key+"RetainPassedSuites",false)){Fresh();Plan();stage=2;}else{suite=0;StartSuite();stage=1;}return;}')
    src=src.replace('retainedPassedSuites=SessionState.GetBool(Key+"RetainPassedSuites",false)?new[]{"monthly","quarterly","final"}:Array.Empty<string>(),', 'focusedOnly=SessionState.GetBool(Key+"RetainPassedSuites",false),')
    src=src.replace('(float?)(consumer["font09_layout"]?["size"]) ?? (float)consumer["target"]["start_size"]', '(float?)(consumer["font09_layout"]?["size"]) ?? (float?)(consumer["target"]["final_size"]) ?? (float)consumer["target"]["start_size"]')
    a=src.index('        var panels =',src.index('static void CaptureObserved'));b=src.index('        foreach(var t in relevant)',a)
    src=src[:a]+'''        var owned=Font09Migration.Consumers.Where(c=>(string)c["asset"]=="Assets/Scenes/GameScene.unity").Select(c=>(string)c["hierarchy_key"]).ToHashSet();
        var relevant=Root.GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled&&!string.IsNullOrEmpty(t.text) &&
            (owned.Contains(PathOf(t.transform)) || t.GetComponentInParent<EventOptionCardView>()!=null ||
             (SharedModalFocus.Active!=null && t.transform.IsChildOf(SharedModalFocus.Active.transform)))).ToArray();
''' +src[b:]
    a=src.index('        var quarter=Root.Find(',src.index('static void CaptureObserved'));b=src.index('        captures.Add(',a);src=src[:a]+src[b:]
    a=src.index('    static void Plan()');b=src.index('    static void Tick()',a)
    src=src[:a]+'''    static void Plan()
    {
        steps.Clear();
        foreach(var size in Sizes)
        {
            var s=size;
            Add("Focused viewport "+s,()=>{resolution=s.x+"x"+s.y;UI01Validation.SetGameView(s.x,s.y);});
            Add("Long tooltip and isolated session",()=>{
                typeof(UI17FeedbackValidation).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                var panel=Root.Find("Panel_Financial/CreditTooltip");
                panel.Find("Message").GetComponent<TMP_Text>().text="O crédito entra no caixa. "+string.Join(" ",Enumerable.Repeat("As parcelas incluem amortização e juros e são pagas no fechamento mensal. Confira as condições antes de confirmar sua escolha.",4));
                Root.Find("Panel_Financial/CreditHelp").GetComponent<Button>().Select();
            });
            Add("Long tooltip remains readable",()=>{Capture("long-tooltip");Root.Find("Panel_Financial/CreditHelp").GetComponent<SharedTooltip>().Dismiss();});
            Add("Long feedback native face",()=>{FeedbackToast.Show(string.Join(" ",Enumerable.Repeat("Não foi possível concluir a operação. Confira suas escolhas e tente novamente; seu progresso continua preservado.",4)),FeedbackKind.Error);});
            Add("Long feedback close and reentry",()=>{
                Capture("long-toast");int before=Writes;var toast=FeedbackToast.Current;
                for(int i=0;i<8;i++){toast.Hide();toast.Display("Operação concluída; dados preservados.",FeedbackKind.Success);}
                Expect(before==Writes,"Toast reentry wrote SQLite");Capture("toast-reentry");toast.Hide();
            });
            Add("Inactive bindings and reapply",()=>{
                int before=Writes;
                foreach(var row in Font09Migration.Consumers.Where(c=>(string)c["asset"]=="Assets/Scenes/GameScene.unity"))
                {
                    var path=((string)row["hierarchy_key"]).Substring("Canvas/".Length);var text=Root.Find(path)?.GetComponent<TMP_Text>();
                    Expect(text!=null,"Missing inactive text "+path);var binding=text.GetComponent<TypographyRoleBinding>();
                    Role(text,FontRoleMigration.ParseRole((string)row["target"]["role"]));binding.Reapply();Role(text,binding.Role);
                }
                Expect(before==Writes,"Inactive typography wrote SQLite");
            });
        }
    }
''' +src[b:]
    (ROOT/'Assets/Editor/Tests/Font09Validation.cs').write_text(src,encoding='utf-8')
    p=ROOT/'Assets/Scripts/Game/Adapter/In/UI/EventOptionCardView.cs';s=p.read_text(encoding='utf-8').replace('KeepSize(effectsText,TypographyRole.Caption)','KeepSize(effectsText,TypographyRole.Body)');p.write_text(s,encoding='utf-8')
    for number,name in [(12,'Event'),(17,'Feedback'),(18,'Dialog')]:
        p=ROOT/f'Assets/Editor/Tests/UI{number}{name}Validation.cs';s=p.read_text(encoding='utf-8');task=f'UI-{number}'
        s=s.replace('t.font.name == "InterTight-Variable"','t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>() != null').replace('t.font.name=="InterTight-Variable"','t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>()!=null')
        if number==12:
            import re
            s=s.replace('new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720) }','UiValidationContext.Resolutions')
            s=s.replace('"output/ui-update/UI-12"','UiValidationContext.DirectoryFor("UI-12")')
            s=re.sub('"output/ui-update/UI-12/([^"\\n]+)"',lambda m:f'UiValidationContext.FileFor("UI-12","{m[1]}")',s)
            s=s.replace('"../UI-12/" + name','UiValidationContext.CaptureName("UI-12",name)')
            s=s.replace('using var hash =','UiValidationContext.OnCapture?.Invoke(name,path); using var hash =')
            # A context is resolved when Run begins, not at assembly load.
            s=s.replace('private static string reportPath = UiValidationContext.FileFor("UI-12","playmode-events.json"),', 'private static string reportPath,')
        p.write_text(s,encoding='utf-8')
    print('Prepared FONT-09 migration and three real runtime suites.')

def builders():
    base=ROOT/'Assets/Editor/UI'
    p=base/'EventUiMigration.cs';s=p.read_text(encoding='utf-8')
    s=s.replace('"Opção", 24, 20, 1072, 36, GameTypography.Tokens.BodyStrong,true','"Opção", 24, 20, 1072, 36, new TypographyToken(TypographyRole.SectionTitle,24),true')
    s=s.replace('"", 24, 68, 1072, 32, GameTypography.Tokens.Label','"", 24, 68, 1072, 32, new TypographyToken(TypographyRole.Body,22)')
    s=s.replace('"", 24, 112, 1072, 64, GameTypography.Tokens.Label','"", 24, 112, 1072, 64, new TypographyToken(TypographyRole.Body,22)')
    s=s.replace('"EVENTOS DO MÊS", 1000, 28, 896, 40, GameTypography.Tokens.Body','"EVENTOS DO MÊS", 1000, 28, 896, 40, new TypographyToken(TypographyRole.Caption,24)')
    s=s.replace('"Concept", "", 0, 0, 1150, 30, GameTypography.Tokens.Caption','"Concept", "", 0, 0, 1150, 30, new TypographyToken(TypographyRole.Body,20)')
    s=s.replace('"OptionsHeading", "Escolha uma opção", 0, 0, 1150, 40, GameTypography.Tokens.BodyStrong,true','"OptionsHeading", "Escolha uma opção", 0, 0, 1150, 40, new TypographyToken(TypographyRole.SectionTitle,24),true')
    p.write_text(s,encoding='utf-8')
    p=base/'DialogUiMigration.cs';s=p.read_text(encoding='utf-8').replace('"",GameTypography.Tokens.Label,false,GamePalette.ErrorText','"",new TypographyToken(TypographyRole.Body,22),false,GamePalette.ErrorText');p.write_text(s,encoding='utf-8')
    p=base/'FeedbackUiMigration.cs';s=p.read_text(encoding='utf-8').replace('message.fontSize = GameTypography.Tokens.Caption','GameTypography.Apply(message, new TypographyToken(TypographyRole.Body,20))');p.write_text(s,encoding='utf-8')
    p=base/'ConceptUiRevision.cs';s=p.read_text(encoding='utf-8');old='Move(p,"TitleText",40,112,1300,64);Type(p,"TitleText",46)';new='Move(p,"TitleText",40,112,1300,64);Type(p,"TitleText",new TypographyToken(TypographyRole.ScreenTitle,46))';assert old in s;s=s.replace(old,new);p.write_text(s,encoding='utf-8')
    p=ROOT/'docs/ui-update/font-coverage.json';c=json.loads(p.read_text(encoding='utf-8'));roles=json.loads((ROOT/'docs/ui-update/font-contract.json').read_text(encoding='utf-8'))['roles']
    for row in c['consumers']:
        if row['owner']!='FONT-09': continue
        role=roles[row['target']['role']]
        for k in ('family','weight','face','font_asset'):row['target'][k]=role[k]
    atomic_json(p,c)
    print('Updated event, dialog, feedback builders and scoped concept title.')

def scene_index():
    import inspect
    import font07_support as prior
    src=inspect.getsource(prior.scene_index).replace('FONT-07','FONT-09').replace('font07','font09')
    src=src.replace('scopes={"Canvas/Panel_Financial","Canvas/Panel_Equipment","Canvas/Panel_Team"}',
        'scopes={"Canvas/Panel_Event","Canvas/ExitUI","Canvas/Feedback","Canvas/Panel_Establishment/PanelReset"}')
    scope=dict(vars(prior));scope['OUT']=OUT
    exec(src,scope);scope['scene_index']()

def preservation():
    import inspect
    import font07_support as prior
    src=inspect.getsource(prior.preservation).replace('FONT-07','FONT-09').replace('font07','font09')
    scope=dict(vars(prior));scope['OUT']=OUT
    exec(src,scope);scope['preservation']()

def patches():
    import font06_support as prior
    prior.OUT=OUT;prior.patches()
    (OUT/'ConceptUiRevision.font06.patch').replace(OUT/'ConceptUiRevision.font09.patch')

def font_cache():
    import font06_support as prior
    prior.OUT=OUT;prior.font_cache()

def prefab_formatting():
    import re
    rows=[]
    for path in ['Assets/Prefabs/EventOptionCard.prefab','Assets/Prefabs/Panel_Equipment.prefab']:
        p=ROOT/path;s=p.read_text(encoding='utf-8');clean=re.sub(r'[ \t]+$','',s,flags=re.M)
        before=(OUT/'baseline'/path).read_text(encoding='utf-8')
        if clean==re.sub(r'[ \t]+$','',before,flags=re.M):shutil.copyfile(OUT/'baseline'/path,p)
        else:p.write_text(clean,encoding='utf-8',newline='')
        rows.append({'path':path,'semantic_change':clean!=re.sub(r'[ \t]+$','',before,flags=re.M),'operation':'Restore baseline bytes when only trailing whitespace changed; otherwise trim trailing whitespace after Editor save.'})
    atomic_json(OUT/'prefab-formatting.json',{'passed':True,'rows':rows})

def source_audit():
    saved=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    prefixes=('Assets/Scripts/Game/Domain/','Assets/Scripts/Game/Application/','Assets/Scripts/Game/Adapter/In/Controllers/','Assets/Scripts/Game/Infrastructure/','Assets/Resources/')
    files=[p for p in saved if p.startswith(prefixes)]
    changed=[p for p in files if (ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=saved[p]]
    bodies={}
    for name in ['EventScreenView','EventOptionCardView','FeedbackToast','SharedTooltip','SharedLoading','ContextualHelp','ExitDialogController']:
        path=f'Assets/Scripts/Game/Adapter/In/UI/{name}.cs'
        before=(OUT/'baseline'/path).read_text(encoding='utf-8');after=(ROOT/path).read_text(encoding='utf-8')
        if name=='EventOptionCardView':after=after.replace('KeepSize(effectsText,TypographyRole.Body)','KeepSize(effectsText,TypographyRole.Caption)')
        if name=='SharedTooltip':
            a=after.index('        private TMPro.TMP_Text message;');b=after.index('        public void Dismiss()',a)
            original=before[before.index('        private void Show()'):before.index('        public void Dismiss()')]
            after=after[:a]+original+after[b:]
        if name=='ContextualHelp':
            a=after.index('        private TMPro.TMP_Text message;');b=after.index('        private void Awake()',a)
            after=after[:a]+after[b:]
            after=after.replace('            if(show)Reflow();\n','')
        bodies[name]=before==after
    result={'passed':not changed and all(bodies.values()),'files_compared':len(files),'unexpected':changed,'callback_and_view_bytes':bodies,
        'note':'Compared to the FONT-09 baseline. Event IDs/options/effects, controllers, simulation, prices and persistence retained. View action callbacks retain their bytes; the option effects face changes from Caption to Body and tooltip/help add native text/surface sizing.'}
    atomic_json(OUT/'source-audit.json',result)
    assert result['passed'],result
    print({'source_files':len(files),'passed':True})

def evidence():
    import re
    import html
    read=lambda n:json.loads((OUT/n).read_text(encoding='utf-8'))
    assert read('execution.json')['passed']
    assert not read('execution.json')['focusedOnly']
    retained=read('retained-suites.json')
    assert hashlib.sha256((ROOT/'Assets/Scenes/GameScene.unity').read_bytes()).hexdigest()==retained['production_scene_sha256']
    assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in retained['production_sources_sha256'].items())
    assert all(hashlib.sha256((OUT/n/v['report']).read_bytes()).hexdigest()==v['sha256'] for n,v in retained['suites'].items())
    saved=read('saved-verification.json');tests=read('edit-tests.json');captures=read('captures.json');focused=read('focused-checks.json')
    suites={}
    for folder,report in [('events','playmode-events.json'),('feedback','playmode.json'),('dialogs','playmode.json')]:
        r=read(folder+'/'+report);assert r['completed'] and r['failures']==0
        suites[folder]=len(r['checks'])
    manifest=[]
    for c in captures:
        p=ROOT/c['path'];assert p.is_file()
        dimensions=[int.from_bytes(p.read_bytes()[i:i+4],'big') for i in (16,20)]
        assert 'x'.join(map(str,dimensions))==c['resolution']
        manifest.append({'name':c['name'],'path':p.relative_to(ROOT).as_posix(),'resolution':c['resolution'],'frame':c['frame'],'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    for size in ['1920x1080','1280x720','1600x1000']:
        rows=[c for c in manifest if c['resolution']==size]
        assert sum(c['name'].startswith('catalog-') and '/events/' in c['path'] for c in rows)==20,('catalog',size)
        assert sum(c['name'].startswith('options-') for c in rows)==20,('options',size)
        for name in ['tooltip-keyboard','tooltip-touch','catalog-loading','catalog-failed','catalog-recovered','toast-warning','exit-draft','exit-save-error','reset-error','newgame-completed','newgame-bankrupt','long-tooltip','long-help','long-toast','history-populated-top','history-populated-bottom']:
            assert any(c['name']==name or c['name']==name+'-'+size for c in rows),(name,size)
    atomic_json(OUT/'capture-manifest.json',manifest)
    cp=ROOT/'docs/ui-update/font-coverage.json';coverage=json.loads(cp.read_text(encoding='utf-8'))
    measured={(r['asset'],r['path']):r for r in saved['rows']};observed={};viewports={}
    plain=lambda path:re.sub(r'\[\d+\]','',path)
    aliases={}
    for row in coverage['consumers']:aliases.setdefault((row['asset'],row.get('global_id')),[]).append(row['hierarchy_key'])
    for c in captures:
        for row in c['texts']:
            observed[row['path']]=row
            viewports.setdefault(row['path'],set()).add(c['resolution'])
            if row['path'].startswith('Canvas/PanelReset/'):
                canonical=row['path'].replace('Canvas/PanelReset/','Canvas/Panel_Establishment/PanelReset/',1)
                observed[canonical]=row;viewports.setdefault(canonical,set()).add(c['resolution'])
    roles={'Brand':'brand','ScreenTitle':'screen_title','SectionTitle':'section_title','Body':'body','Button':'button','Caption':'caption','Kpi':'kpi','Label':'label','Table':'table','TableTotal':'table_total','Input':'input','Navigation':'navigation'}
    fonts={'Roboto-Regular':('Roboto',400,'Regular'),'Roboto-Bold':('Roboto',700,'Bold'),'RobotoCondensed-Bold':('Roboto Condensed',700,'Bold'),'RobotoCondensed-SemiBold':('Roboto Condensed',600,'SemiBold'),'RobotoCondensed-Medium':('Roboto Condensed',500,'Medium'),'RobotoCondensed-Regular':('Roboto Condensed',400,'Regular')}
    missing=[];active=0;inherited=0;resolution_rows=[]
    for c in coverage['consumers']:
        if c['owner']!='FONT-09':continue
        key=(c['asset'],c['hierarchy_key']);r=measured.get(key) if c['kind']=='serialized' else observed.get(c['hierarchy_key'])
        actual=c['kind']=='runtime' and r is not None
        if c['kind']=='runtime' and r is None:
            r=measured.get(key)
            if r is None:r=next((measured[(c['asset'],p)] for p in aliases[(c['asset'],c.get('global_id'))] if (c['asset'],p) in measured),None)
            if r is None:r=next((v for (a,p),v in measured.items() if a==c['asset'] and plain(p)==plain(c['hierarchy_key'])),None)
            inherited+=r is not None
        active+=actual
        if r is None:missing.append(c['id']);continue
        evidence='captures.json' if actual else 'saved-verification.json'
        c['font09']={'font':r['font'],'role':r['role'],'final_size':r['size'],'observed_active_in_qa':actual,'observed_resolutions':sorted(viewports.get(c['hierarchy_key'],[])) if actual else [],'evidence':'output/ui-update/fonts/FONT-09/'+evidence}
        family,weight,face=fonts[r['font']]
        c['target'].update(role=roles[r['role']],family=family,weight=weight,face=face,font_asset='Assets/TextMesh Pro/Fonts/Concept/SDF/'+r['font']+'.asset',final_size=r['size'],size_status='FONT-09: saved/reopened or observed in the real Player; exact active resolutions in font09.observed_resolutions')
        c['migration_state']='FONT-09: aplicado serializado' if c['kind']=='serialized' else 'FONT-09: consumidor ativo observado' if actual else 'FONT-09: contraparte serializada verificada; consumidor inativo'
        resolution_rows.append({'id':c['id'],'kind':c['kind'],'path':c['hierarchy_key'],'observed':actual,'resolutions':c['font09']['observed_resolutions']})
    atomic_json(OUT/'coverage-resolution.json',{'active':active,'inherited':inherited,'missing':missing,'rows':resolution_rows});assert not missing,missing
    coverage['status']='FONT-01 preserved; FONT-04 providers and FONT-05..09 consumers integrated and validated; FONT-10 pending'
    coverage['font09']={'status':'concluído','serialized_consumers':saved['count'],'runtime_entries_observed':active,'runtime_entries_verified_via_saved_counterpart':inherited,'edit_tests':tests['count'],'suite_checks':suites,'focused_checks':focused['count'],'captures':len(captures),'qa':'docs/ui-update/qa/FONT-09.json'}
    atomic_json(cp,coverage)
    cp=ROOT/'docs/ui-update/font-contract.json';contract=json.loads(cp.read_text(encoding='utf-8'))
    contract['font09']={'qa':'docs/ui-update/qa/FONT-09.json','sizes':'Final size and explicit role per consumer in font-coverage.json. No global reduction; saved 46/32/24/22/20 sizes retained.',
        'events':'20 real catalog events and all 60 choices; saved/reopened template and actual decision/history clones. Option titles use Condensed Bold; descriptions/effects use Roboto Regular.',
        'dialogs':'Real Condensed Bold headings and SemiBold actions; Roboto Regular explanatory body/error. Modal ownership, keyboard, raycast and persistence assertions retained.',
        'feedback':'Roboto Regular body, neutral shared material; actual help, tooltip, toast, loading, empty and error controls. Native wrapping and box height, no autosize, synthetic bold, truncation or text scale.',
        'scope':'Proposed typography contract only; global promotion remains FONT-10.'}
    atomic_json(cp,contract)
    cp=ROOT/'docs/ui-update/font-assets.json';assets=json.loads(cp.read_text(encoding='utf-8'));assets['font09']={'qa':'docs/ui-update/qa/FONT-09.json','faces':'Six existing production faces and three finite fallbacks reused; per-message materials/atlases prohibited.'};atomic_json(cp,assets)
    page=['<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>FONT-09 · QA real</title><style>body{font:16px system-ui;background:#171223;color:#eee;margin:24px}h1,h2{color:#d8baff}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0 0 24px}img{width:100%;display:block}figcaption{padding:8px}.gallery{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}a{color:#d4bcff}</style><h1>FONT-09 · Eventos, diálogos, feedback e ajuda</h1><p>Capturas desta execução no Unity 6000.3.20f1 com SQLite/PlayerPrefs isolados. Os cinco concepts não apresentam uma tela específica de evento, diálogo ou toast; 03-gestao serve de referência para famílias, hierarquia, cabeçalho e ações. Sem inventar um concept de evento. Build independente fica para FONT-10.</p>']
    for size in ['1920x1080','1280x720','1600x1000']:
        chosen=next(c for c in manifest if c['resolution']==size and c['name'].startswith('catalog-freezer_breakdown-'))
        file=pathlib.Path(chosen['path']).relative_to(OUT.relative_to(ROOT)).as_posix()
        page.append('<h2>'+size+'</h2><div class="pair"><figure><img src="../../../../art-source/references/concepts/03-gestao.png"><figcaption>Referência comum de tipografia</figcaption></figure><figure><img src="'+file+'"><figcaption>Unity · '+size+'</figcaption></figure></div>')
    page.append('<h2>Catálogo, histórico, estados e conteúdo longo</h2><div class="gallery">')
    for c in manifest:
        file=pathlib.Path(c['path']).relative_to(OUT.relative_to(ROOT)).as_posix()
        page.append('<figure><a href="'+file+'"><img loading="lazy" src="'+file+'"></a><figcaption>'+html.escape(c['name']+' · '+c['resolution'])+'</figcaption></figure>')
    page.append('</div></html>');(OUT/'index.html').write_text('\n'.join(page),encoding='utf-8')
    print({'serialized':saved['count'],'tests':tests['count'],'suites':suites,'focused':focused['count'],'captures':len(captures),'active':active,'inherited':inherited})

def stage_list():
    paths=[
        'Assets/Editor/UI/Font09Migration.cs','Assets/Editor/UI/Font09Migration.cs.meta',
        'Assets/Editor/Tests/Font09Validation.cs','Assets/Editor/Tests/Font09Validation.cs.meta',
        'Assets/Editor/Tests/Font09ExitValidation.cs','Assets/Editor/Tests/Font09ExitValidation.cs.meta',
        'Assets/Editor/Tests/UI12EventValidation.cs','Assets/Editor/Tests/UI12EventTests.cs',
        'Assets/Editor/Tests/UI17FeedbackValidation.cs','Assets/Editor/Tests/UI18DialogValidation.cs','Assets/Editor/Tests/UI18DialogTests.cs',
        'Assets/Editor/UI/EventUiMigration.cs','Assets/Editor/UI/DialogUiMigration.cs','Assets/Editor/UI/FeedbackUiMigration.cs',
        'Assets/Scripts/Game/Adapter/In/UI/EventOptionCardView.cs','Assets/Scripts/Game/Adapter/In/UI/SharedTooltip.cs',
        'Assets/Prefabs/EventOptionCard.prefab','Assets/Prefabs/Panel_Equipment.prefab',
        'docs/ui-update/font-coverage.json','docs/ui-update/font-contract.json','docs/ui-update/font-assets.json','docs/ui-update/qa/FONT-09.json',
        'docs/ui-update/font-tasks/README.md','docs/ui-update/font-tasks/FONT-09-eventos-dialogos-e-ajuda.md','tools/ui/font09_support.py']
    # Include ContextualHelp only if the actual long-content test required it.
    extra='Assets/Scripts/Game/Adapter/In/UI/ContextualHelp.cs'
    if (ROOT/extra).read_bytes()!=(OUT/'baseline'/extra).read_bytes():paths.append(extra)
    evidence=['execution.json','edit-tests.json','saved-verification.json','focused-checks.json','captures.json','capture-manifest.json','coverage-resolution.json','preservation.json','font-cache-preservation.json','commit-scene-verification.json','scene-staging.json','staging-plan.json','index.html','ConceptUiRevision.font09.patch','source-audit.json','baseline-preservation.json','session-geometry.json','post-migration-verification.json','post-runtime-template-audit.json','visual-review.json','prefab-formatting.json','retained-suites.json']
    paths+=['output/ui-update/fonts/FONT-09/'+p for p in evidence]
    for folder,report in [('events','playmode-events.json'),('feedback','playmode.json'),('dialogs','playmode.json')]:paths+=['output/ui-update/fonts/FONT-09/'+folder+'/'+n for n in [report,'runtime.json']]
    paths+=['output/ui-update/fonts/FONT-09/exits/'+name for name in ['execution.json']+['exit-'+mode+'.json' for mode in ['draft','saved','save','discard','failure-retry','loading-discard','incompatible','completed','bankrupt']]]
    paths += [c['path'] for c in json.loads((OUT/'capture-manifest.json').read_text(encoding='utf-8'))]
    paths=sorted(set(paths))
    for path in paths:
        assert path.endswith(('staging-plan.json','baseline-preservation.json')) or (ROOT/path).is_file(),path
        assert '/orchestration/' not in path and not path.endswith('.db') and '/baseline/' not in path
    report={'task':'FONT-09','paths':paths,'scene_staged_separately':'Assets/Scenes/GameScene.unity','scene_snapshot_sha256':hashlib.sha256((OUT/'scene-for-index.unity').read_bytes()).hexdigest(),
        'minimum_dependencies':{'scene':'Four scoped roots, frozen typography consumer IDs and required prefab overrides; 10,263 HEAD blocks outside selection retained. No unsaved Editor hierarchy is included.',
        'ConceptUiRevision':'Preexisting untracked file retained; only FONT-09 title role change supplied in a patch.',
        'UI12EventTests':'Read the already loaded scene without closing it; enumerate all 20 events and 60 options.',
        'SharedTooltip':'Only native text/surface sizing before display. Focus, input, actions and modal guards preserved.'},
        'excluded':'All prior deltas, font caches, backups, SQLite, local dependencies, orchestration and raw staging patches.'}
    atomic_json(OUT/'staging-plan.json',report)
    (OUT/'commit-paths.nul').write_bytes(b'\0'.join(p.encode('utf-8') for p in paths)+b'\0')
    baseline=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    changed=[p for p,h in baseline.items() if (ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    missing=[p for p in baseline if not (ROOT/p).is_file()]
    authorized=set(paths)|{'Assets/Scenes/GameScene.unity','Assets/Editor/UI/ConceptUiRevision.cs'}
    unexpected=[p for p in changed if p not in authorized]
    result={'passed':not unexpected and not missing,'files_compared':len(baseline),'unchanged':len(baseline)-len(changed)-len(missing),'authorized_changed':changed,'unexpected':unexpected,'missing':missing,'excluded_mutable':'output/ui-update/fonts/orchestration'}
    atomic_json(OUT/'baseline-preservation.json',result)
    assert not unexpected and not missing,(unexpected,missing)
    print({'paths':len(paths),'baseline_files_preserved':result['unchanged']})

def qa():
    read=lambda n:json.loads((OUT/n).read_text(encoding='utf-8'))
    for name in ['execution.json','edit-tests.json','preservation.json','source-audit.json','font-cache-preservation.json','commit-scene-verification.json','post-runtime-template-audit.json','visual-review.json','exits/execution.json']:
        assert read(name)['passed'],name
    saved=read('saved-verification.json');tests=read('edit-tests.json');captures=read('capture-manifest.json');focused=read('focused-checks.json')
    suites={}
    for folder,report in [('events','playmode-events.json'),('feedback','playmode.json'),('dialogs','playmode.json')]:
        r=read(folder+'/'+report);assert r['completed'] and r['failures']==0;suites[folder]=len(r['checks'])
    assert focused['completed'] and not focused['failures']
    exits=read('exits/execution.json')
    for mode in exits['modes']:assert read('exits/exit-'+mode+'.json')['passed'],mode
    result={'task':'FONT-09','status':'concluído','date':'2026-10-18','unity':'6000.3.20f1','scope':'UI-12, UI-17, UI-18: events, history, dialogs, feedback and help',
        'validation':{'edit_tests':tests['count'],'serialized_consumers':saved['count'],'suite_checks':suites,'focused_checks':focused['count'],'actual_exit_modes':exits['modes'],'runtime_checks_total':sum(suites.values())+focused['count']+len(exits['modes']),'captures':len(captures),'resolutions':['1920x1080','1280x720','1600x1000'],'isolated_storage':True,'scene_preserved':read('execution.json')['scenePreserved'],'console_errors':read('execution.json')['consoleErrors'],'console_warnings':read('execution.json')['consoleWarnings'],'retained_expected_reset_warnings':read('retained-suites.json')['expected_warning_count']},
        'accepted':['20 real events, 60 real options, idempotent confirmations and actual monthly settlement. Native decision/history clones, selection, keyboard and scroll masks verified.',
        'Saved inactive templates and runtime role bindings use real production faces and neutral shared materials; no synthetic bold, autosize, truncation or text scaling.',
        'Actual dialog focus, tab order, raycast/scrim, cancellation, reset, save failure/retry, archived reports and purchase retain the original assertions with isolated SQLite/PlayerPrefs.',
        'Long tooltip/help/toast/event/dialog content and repeated display/reentry measured in all three viewports. Typography and read-only queries preserve SELECT total_changes().'],
        'evidence':{'gallery':'output/ui-update/fonts/FONT-09/index.html','captures':'output/ui-update/fonts/FONT-09/capture-manifest.json','inventory':'output/ui-update/fonts/FONT-09/coverage-resolution.json','preservation':'output/ui-update/fonts/FONT-09/baseline-preservation.json','scoped_scene':'output/ui-update/fonts/FONT-09/commit-scene-verification.json','visual_review':'output/ui-update/fonts/FONT-09/visual-review.json'},
        'limits':['Validation runs in the existing Editor with a disposable Player and isolated storage. Standalone Windows/Android builds and global contract promotion remain FONT-10.',
        'The real closing-month flow waits for the four event decisions; no additional month-close dialog was invented. Quit probes invoke the real buttons and stop Play Mode.',
        'The five approved concepts have no dedicated event/dialog/help screen. The gallery compares shared typography with 03-gestao and labels this limit explicitly.',
        'Six faces and three finite fallbacks reused; the previously verified 536-codepoint corpus is preserved, not extended.',
        'Inactive inventory entries are verified through saved counterparts and identified separately from active runtime observations.',
        'The real Edit-mode session at FONT-09 entry had seven roots and no extra Canvas/Text (TMP) named Restaurante. That observed session is retained; historical handoff metadata is not substituted for current evidence.'],
        'next_task':'FONT-10 only after this acceptance and the local English commit, per sequential-handoff.txt.'}
    atomic_json(ROOT/'docs/ui-update/qa/FONT-09.json',result)
    p=ROOT/'docs/ui-update/font-tasks/FONT-09-eventos-dialogos-e-ajuda.md';s=p.read_text(encoding='utf-8').replace('Status: **pendente**.','Status: **concluído**.').replace('- [ ]','- [x]');p.write_text(s,encoding='utf-8')
    p=ROOT/'docs/ui-update/font-tasks/README.md';s=p.read_text(encoding='utf-8').replace('FONT-01 a FONT-08 concluídos; FONT-09 a FONT-10 pendentes','FONT-01 a FONT-09 concluídos; FONT-10 pendente').replace('[FONT-09 — Eventos, diálogos, feedback e ajuda](FONT-09-eventos-dialogos-e-ajuda.md) | FONT-08 | Pendente','[FONT-09 — Eventos, diálogos, feedback e ajuda](FONT-09-eventos-dialogos-e-ajuda.md) | FONT-08 | Concluído — [QA](../qa/FONT-09.json)');p.write_text(s,encoding='utf-8')
    print({'accepted':True,'tests':tests['count'],'runtime':result['validation']['runtime_checks_total'],'captures':len(captures)})

if __name__=='__main__': globals()[sys.argv[1]]()
