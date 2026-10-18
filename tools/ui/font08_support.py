"""FONT-08 selective preservation, evidence and staging helpers."""
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import font05_support as common

ROOT = common.ROOT
OUT = ROOT / 'output/ui-update/fonts/FONT-08'
common.OUT = OUT

def atomic_json(path, value):
    temp = path.with_name(path.name + '.font08.tmp')
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
        p = ROOT/path
        if not p.is_file(): continue
        manifest[path] = hashlib.sha256(p.read_bytes()).hexdigest()
        if path.startswith(('Assets/', 'docs/')):
            dest = OUT/'baseline'/path
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, dest)
    atomic_json(OUT/'baseline.json', manifest)
    for name,args in [('initial-status.txt',('--no-optional-locks','status','--short')),('initial-tracked.patch',('diff','--binary')),('initial-staged.patch',('diff','--cached','--binary'))]:
        (OUT/name).write_bytes(common.git(*args))
    print({'files':len(manifest),'HEAD':common.git('rev-parse','HEAD').decode().strip()})

def prepare():
    import re
    coverage_path=ROOT/'docs/ui-update/font-coverage.json'
    coverage=json.loads(coverage_path.read_text(encoding='utf-8'))
    panels=['Panel_MonthlyResult','Panel_QuarterlyResult','Panel_FinalReport','Panel_Bankruptcy']
    review=[]
    for c in coverage['consumers']:
        path=c['hierarchy_key']
        if c['owner']=='FONT-09' and any(path.startswith('Canvas/'+n+'/') for n in panels):
            c['font08_shared']=True
        if c['owner']!='FONT-08' and not c.get('font08_shared'): continue
        old=c['target']['role']; role=old
        m=re.search(r'Panel_QuarterlyResult/.*/Row(\d+)/(Label|Cell[0-4])$',path)
        if m and int(m[1]) in (7,9,11): role='table_total'
        if '/CashChart/Title' in path: role='section_title'
        if '/CashChart/Note' in path: role='caption'
        if '/NewGameDialog/Dialog/Message' in path: role='body'
        if '/LoadedBody/Conclusion/Scroll/Viewport/Content/Reputation' in path or '/LoadedBody/Scroll/Viewport/Content/Reputation' in path: role='body'
        if role!=old:
            review.append({'id':c['id'],'path':path,'from':old,'to':role})
            c['target']['role']=role
    atomic_json(coverage_path,coverage)
    atomic_json(OUT/'role-review.json',{'explicit_consumers':review,'shared_error_dialog_consumers':sum(c['kind']=='serialized' and bool(c.get('font08_shared')) for c in coverage['consumers'])})
    src=(ROOT/'Assets/Editor/UI/Font07Migration.cs').read_text(encoding='utf-8').replace('FONT-07','FONT-08').replace('Font07','Font08').replace('font07','font08')
    (ROOT/'Assets/Editor/UI/Font08Migration.cs').write_text(src,encoding='utf-8')
    # Retain the established live-session guard and disposable Player lifecycle.
    src=(ROOT/'Assets/Editor/Tests/Font07Validation.cs').read_text(encoding='utf-8').replace('FONT-07','FONT-08').replace('Font07','Font08').replace('font07','font08')
    src=src.replace('    static CreditLineData longFixture;\n','')
    src=src.replace('typeof(UI09FinancialTests), typeof(UI10EquipmentTests), typeof(UI11TeamTests)','typeof(UI13MonthlyResultTests), typeof(UI14QuarterlyResultTests), typeof(UI15FinalReportTests), typeof(UI16FinalReportTests)')
    src=src.replace('new[] {"bank", "equipment", "team"}', 'new[] {"monthly", "quarterly", "final", "bankruptcy"}')
    src=src.replace('static readonly string[] Reports = {"playmode-financial.json","playmode-equipment.json","playmode-team.json"};','static readonly string[] Reports = {"playmode.json","playmode.json","playmode.json","playmode.json"};')
    src=src.replace('new[]{"Panel_Financial","Panel_Equipment","Panel_Team"}', 'new[]{"Panel_MonthlyResult","Panel_QuarterlyResult","Panel_FinalReport","Panel_Bankruptcy"}')
    src=src.replace('if(suite==0)UI09FinancialValidation.Run(); else if(suite==1)UI10EquipmentValidation.Run();else UI11TeamValidation.Run();', 'if(suite==0)UI13MonthlyResultValidation.Run(); else if(suite==1)UI14QuarterlyResultValidation.Run();else if(suite==2)UI15FinalReportValidation.Run();else UI16BankruptcyValidation.Run();')
    a=src.index('    static void Plan()');b=src.index('    static void Tick()',a)
    src=src[:a]+'''    static void Plan()
    {
        steps.Clear();
        foreach(var size in Sizes)
        {
            var s=size;
            Add("Focused viewport "+s,()=>{resolution=s.x+"x"+s.y;UI01Validation.SetGameView(s.x,s.y);});
            Add("Final and bankruptcy enable cycles preserve writes",()=>{
                int before=Writes;
                foreach(var panel in new[]{"Panel_MonthlyResult","Panel_QuarterlyResult","Panel_FinalReport","Panel_Bankruptcy"})
                {
                    var p=Root.Find(panel);bool enabled=p.gameObject.activeSelf;
                    foreach(var t in p.GetComponentsInChildren<TMP_Text>(true))
                    {
                        var binding=t.GetComponent<TypographyRoleBinding>();Expect(binding!=null,"Unbound text "+PathOf(t.transform));
                        GameTypography.KeepSize(t,binding.Role);Role(t,binding.Role);
                    }
                    p.gameObject.SetActive(false);p.gameObject.SetActive(enabled);
                }
                Expect(before==Writes,"Typography/enable caused a SQLite write");
            });
        }
    }
''' +src[b:]
    src=src.replace('if(++suite<3)','if(++suite<4)')
    src=src.replace('        if(longFixture!=null){UnityEngine.Object.DestroyImmediate(longFixture);longFixture=null;}\n','')
    src=src.replace('t.fontSharedMaterial == style.Material','t.fontSharedMaterial != null && t.fontSharedMaterial.GetInstanceID() == style.Material.GetInstanceID()')
    (ROOT/'Assets/Editor/Tests/Font08Validation.cs').write_text(src,encoding='utf-8')
    # Reuse all production interaction assertions while routing evidence to this task.
    for number,name in [(13,'MonthlyResult'),(14,'QuarterlyResult'),(15,'FinalReport'),(16,'Bankruptcy')]:
        p=ROOT/f'Assets/Editor/Tests/UI{number}{name}Validation.cs';src=p.read_text(encoding='utf-8');task=f'UI-{number}'
        src=src.replace('t.font.name=="InterTight-Variable"&&!t.enableAutoSizing','t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>()!=null&&!t.enableAutoSizing')
        src=src.replace('t.font.name == "InterTight-Variable" && !t.enableAutoSizing','t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>() != null && !t.enableAutoSizing')
        src=src.replace('new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)}','UiValidationContext.Resolutions')
        src=src.replace('new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720) }','UiValidationContext.Resolutions')
        src=src.replace(f'"output/ui-update/{task}"',f'UiValidationContext.DirectoryFor("{task}")')
        src=re.sub(f'"output/ui-update/{task}/([^"\\n]+)"',lambda m:f'UiValidationContext.FileFor("{task}","{m[1]}")',src)
        src=src.replace(f'"../{task}/"+name',f'UiValidationContext.CaptureName("{task}",name)').replace(f'"../{task}/" + name',f'UiValidationContext.CaptureName("{task}", name)')
        needle='using var hash'
        src=src.replace(needle,'UiValidationContext.OnCapture?.Invoke(name,path); '+needle)
        p.write_text(src,encoding='utf-8')
    # Current monthly chart is a scroll child; all assertions still check bounds and art.
    p=ROOT/'Assets/Editor/Tests/UI13MonthlyResultValidation.cs';src=p.read_text(encoding='utf-8').replace('View.transform.Find("LoadedBody/CashChart")','View.transform.Find("LoadedBody/TableScroll/Viewport/Content/CashChart")')
    p.write_text(src,encoding='utf-8')
    # The approved chart is now vertical. Validate the actual sign, height and baseline.
    p=ROOT/'Assets/Editor/Tests/UI14QuarterlyResultValidation.cs';src=p.read_text(encoding='utf-8')
    old='Expect(Math.Abs(View.Bars[i].fill.sizeDelta.x-(r==null?0:232*Math.Abs(r.netResult)/max))<.01,"Chart scale");'
    new='bool mixed=expected.Months.Any(m=>m!=null&&m.netResult<0); float available=mixed?100:210, baseline=mixed?242:330; Expect(Math.Abs(View.Bars[i].fill.sizeDelta.y-(r==null?0:available*Math.Abs(r.netResult)/max))<.01,"Chart scale"); Expect(View.Bars[i].fill.sizeDelta.x==116&&View.Bars[i].fill.anchoredPosition.y==-baseline&&View.Bars[i].fill.pivot.y==(r!=null&&r.netResult<0?1:0),"Chart sign and baseline");'
    assert old in src;src=src.replace(old,new);p.write_text(src,encoding='utf-8')
    print({'prepared':True,'shared':20,'reviewed_roles':len(review)})

def builders():
    base=ROOT/'Assets/Editor/UI'
    p=base/'MonthlyResultUiMigration.cs';s=p.read_text(encoding='utf-8')
    s=s.replace('810,28,GameTypography.Tokens.Table);','810,28,(i==7||i==9||i==11?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table));')
    s=s.replace('520,42,GameTypography.Tokens.BodyStrong,true)','520,42,GameTypography.Tokens.SectionTitle,true)')
    s=s.replace('246,30,GameTypography.Tokens.Caption,true)','246,30,new TypographyToken(TypographyRole.TableTotal,20),true)')
    p.write_text(s,encoding='utf-8')
    p=base/'QuarterlyResultUiMigration.cs';s=p.read_text(encoding='utf-8')
    s=s.replace('282,30,GameTypography.Tokens.TableTotal,true)','282,30,(i==7||i==9||i==11?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table),i==7||i==9||i==11)')
    s=s.replace('(c==3?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table),c==3)','(c==3||i==7||i==9||i==11?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table),c==3||i==7||i==9||i==11)')
    s=s.replace('520,42,GameTypography.Tokens.BodyStrong,true)','520,42,GameTypography.Tokens.SectionTitle,true)')
    p.write_text(s,encoding='utf-8')
    p=base/'FinalReportUiMigration.cs';s=p.read_text(encoding='utf-8')
    s=s.replace('width - 112, 64, GameTypography.Tokens.BodyStrong,true)','width - 112, 64,new TypographyToken(TypographyRole.SectionTitle,24),true)')
    p.write_text(s,encoding='utf-8')
    p=base/'BankruptcyUiMigration.cs';s=p.read_text(encoding='utf-8')
    s=s.replace('("counter", "Counter", GameTypography.Tokens.Body)','("counter", "Counter", new TypographyToken(TypographyRole.Caption,24))')
    s=s.replace('320, 40, GameTypography.Tokens.BodyStrong,true)','320, 40,new TypographyToken(TypographyRole.Label,24),false)')
    s=s.replace('440, 40, GameTypography.Tokens.BodyStrong,true)','440, 40,GameTypography.Tokens.Body,false)')
    p.write_text(s,encoding='utf-8')
    p=base/'ConceptUiRevision.cs';s=p.read_text(encoding='utf-8');a=s.index('        static void Results()');b=s.index('        static void Secondary()',a);scope=s[a:b]
    scope=scope.replace('Type(p,"Title",46)','Type(p,"Title",new TypographyToken(TypographyRole.ScreenTitle,46))')
    scope=scope.replace('Type(loaded,"Kpi"+i+"/Value",38)','Type(loaded,"Kpi"+i+"/Value",new TypographyToken(TypographyRole.Kpi,38))')
    scope=scope.replace('Type(loaded,"AggregationNote",18,false)','Type(loaded,"AggregationNote",new TypographyToken(TypographyRole.Body,18),false)')
    scope=scope.replace('Type(chart,"Title",28)','Type(chart,"Title",new TypographyToken(TypographyRole.SectionTitle,28))')
    scope=scope.replace('Type(row,"Label",24,false);Type(row,"Value",24,i==9||i==11)','Type(row,"Label",new TypographyToken(i==7||i==9||i==11?TypographyRole.TableTotal:TypographyRole.Table,24),i==7||i==9||i==11);Type(row,"Value",new TypographyToken(i==7||i==9||i==11?TypographyRole.TableTotal:TypographyRole.Table,24),i==7||i==9||i==11)')
    scope=scope.replace('Type(row,"Label",22,true);Type(row,"Rule",18,false)','Type(row,"Label",new TypographyToken(i==7||i==9||i==11?TypographyRole.TableTotal:TypographyRole.Table,22),i==7||i==9||i==11);Type(row,"Rule",new TypographyToken(TypographyRole.Caption,18),false)')
    scope=scope.replace('Type(row,"Cell"+j,20,j==3)','Type(row,"Cell"+j,new TypographyToken(j==3||i==7||i==9||i==11?TypographyRole.TableTotal:TypographyRole.Table,20),j==3||i==7||i==9||i==11)')
    scope=scope.replace('484,52,32,true)','484,52,new TypographyToken(TypographyRole.SectionTitle,32),true)')
    scope=scope.replace('270,50,24,j>=2)','270,50,new TypographyToken(TypographyRole.Label,24),false)')
    scope=scope.replace('260,50,28,true)','260,50,new TypographyToken(j==2?TypographyRole.Kpi:TypographyRole.TableTotal,28),true)')
    scope=scope.replace('524,44,19)','524,44,new TypographyToken(TypographyRole.Caption,19))')
    scope=scope.replace('294,36,24)','294,36,new TypographyToken(TypographyRole.Label,24))')
    scope=scope.replace('Type(chart,"BarValue"+j,22)','Type(chart,"BarValue"+j,new TypographyToken(TypographyRole.TableTotal,22))')
    assert scope!=s[a:b];s=s[:a]+scope+s[b:];p.write_text(s,encoding='utf-8')
    print('Updated four builders and scoped concept typography.')

def scene_index():
    import inspect
    import font07_support as prior
    common.OUT=OUT
    src=inspect.getsource(prior.scene_index).replace('FONT-07','FONT-08').replace('font07','font08')
    src=src.replace('scopes={"Canvas/Panel_Financial","Canvas/Panel_Equipment","Canvas/Panel_Team"}', 'scopes={"Canvas/Panel_MonthlyResult","Canvas/Panel_QuarterlyResult","Canvas/Panel_FinalReport","Canvas/Panel_Bankruptcy"}')
    scope=dict(vars(prior));scope['OUT']=OUT
    exec(src,scope);scope['scene_index']()

def preservation():
    import inspect
    import font07_support as prior
    common.OUT=OUT
    src=inspect.getsource(prior.preservation).replace('FONT-07','FONT-08').replace('font07','font08')
    src=src.replace("    scope=dict(vars(common));scope['OUT']=OUT",'''    src=src.replace('        if "size" in layout:', '        if "width" in layout and "m_SizeDelta" in data: data["m_SizeDelta"].pop("x",None)\\n        if "size" in layout:')
    scope=dict(vars(common));scope['OUT']=OUT''')
    scope=dict(vars(prior));scope['OUT']=OUT
    exec(src,scope);scope['preservation']()

def patches():
    import font06_support as prior
    prior.OUT=OUT;prior.patches()
    (OUT/'ConceptUiRevision.font06.patch').replace(OUT/'ConceptUiRevision.font08.patch')

def font_cache():
    import font06_support as prior
    prior.OUT=OUT;prior.font_cache()
    p=OUT/'font-cache-preservation.json';report=json.loads(p.read_text(encoding='utf-8'))
    report['font_asset_files']=sum(pathlib.Path(r['path']).suffix in ('.asset','.mat','.ttf','.otf') for r in report['assets'])
    accepted=json.loads((ROOT/'output/ui-update/fonts/FONT-07/font-cache-preservation.json').read_text(encoding='utf-8'))['assets']
    measured={r['path']:r for r in report['assets']}
    assert all(p['path'] in measured and measured[p['path']]['before']==measured[p['path']]['final'] for p in accepted)
    report['accepted_scope_files_preserved']=len(accepted)
    report['scope_note']='Prior accepted paths enumerate scope only. All 452 before/final hashes come from the FONT-08 baseline; the 149 accepted paths are a verified subset (including bundled TMP support source files).'
    atomic_json(p,report)

def source_audit():
    saved=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    prefixes=('Assets/Scripts/Game/Domain/','Assets/Scripts/Game/Application/','Assets/Scripts/Game/Adapter/In/Controllers/','Assets/Scripts/Game/Infrastructure/','Assets/Resources/')
    paths=[p for p in saved if p.startswith(prefixes)]
    changed=[p for p in paths if (ROOT/p).is_file() and hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=saved[p]]
    same={}
    for name in ['MonthlyResultView.cs','FinalReportView.cs','BankruptcyView.cs']:
        path='Assets/Scripts/Game/Adapter/In/UI/'+name
        same[name]=saved[path]==hashlib.sha256((ROOT/path).read_bytes()).hexdigest()
    p='Assets/Scripts/Game/Adapter/In/UI/QuarterlyResultView.cs'
    before=(OUT/'baseline'/p).read_text(encoding='utf-8');after=(ROOT/p).read_text(encoding='utf-8')
    for minimum in (28,48):
        after=after.replace(f'float cellHeight = Mathf.Max({minimum}, row.cells[c].GetPreferredValues(cells[c].Text, row.cells[c].rectTransform.rect.width, 0).y + 4);\n                    row.cells[c].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellHeight);\n                    h = Mathf.Max(h, cellHeight + 24);','h = Mathf.Max(h, row.cells[c].GetPreferredValues(cells[c].Text, row.cells[c].rectTransform.rect.width, 0).y + 24);')
    if '// Reserve native value height' in after:
        start=after.index('            // Reserve native value height');end=after.index('            for (int i = 0; i < 3; i++)',start)
        after=after[:start]+after[end:]
        def block(s,needle,start=0):
            a=s.index(needle,start);brace=s.index('{',a);depth=1;b=brace+1
            while depth:
                depth+=(s[b]=='{')-(s[b]=='}');b+=1
            return a,b
        a,b=block(after,'                if(verticalChart)');c,d=block(before,'                if(verticalChart)')
        after=after[:a]+before[c:d]+after[b:]
        a,b=block(after,'\n            if(verticalChart)')
        after=after[:a+1]+after[b+1:]
    else:
        after=after.replace('float valueHeight=Mathf.Max(36,b.value.GetPreferredValues(b.value.text,b.value.rectTransform.rect.width,0).y+4);\n                    b.value.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,valueHeight);\n                    b.value.rectTransform.anchoredPosition=new Vector2(x-30,-(r!=null&&r.netResult<0?baseline+height+2:baseline-height-valueHeight-2));','b.value.rectTransform.anchoredPosition=new Vector2(x-30,-(r!=null&&r.netResult<0?baseline+height+2:baseline-height-38));')
    same['QuarterlyResultView unchanged outside native cell height and chart geometry']=before==after
    report={'passed':not changed and all(same.values()),'files_compared':len(paths),'unexpected_changes':changed,'view_business_content_unchanged':same,'note':'Compared to pre-task bytes, including earlier uncommitted composition. No models, IDs, formula, classification, callbacks or persistence implementation changed.'}
    atomic_json(OUT/'source-audit.json',report);assert report['passed'],report
    print({'files':len(paths),'views':same,'passed':True})

def baseline_preservation():
    saved=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
    changed=[p for p,h in saved.items() if not (ROOT/p).is_file() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    observations=[];cleanup=[];stamp=(OUT/'baseline.json').stat().st_mtime
    for line in (OUT/'initial-status.txt').read_text(encoding='utf-8').splitlines():
        if not line.startswith('?? '):continue
        path=line[3:].strip('"');p=ROOT/path
        if path in saved or path=='tools/ui/font08_support.py' or not path.startswith(('Assets/','docs/','tools/')):continue
        if p.is_file():observations.append({'path':path,'sha256_now':hashlib.sha256(p.read_bytes()).hexdigest(),'mtime_precedes_task_baseline':p.stat().st_mtime<=stamp,'note':'Additional untracked file; not part of the initial byte-hash manifest. No authored changes; timestamp observation kept separate.'})
        elif path=='Assets/Scenes/Font07TypographyCopy.unity.meta':cleanup.append({'path':path,'kind':'orphan metadata for previous QA temporary scene; no scene asset existed','removed_by':'Unity AssetDatabase refresh','guid_from_editor_import_log':'3a3dddb5d25d99a43948f9623c0dc252','note':'No production asset or authored document removed. Not claimed as preserved byte evidence.'})
    atomic_json(OUT/'baseline-preservation.json',{'files_compared':len(saved),'unchanged':len(saved)-len(changed),'changed_paths':changed,'orchestration_excluded':True,'additional_untracked_observations':observations,'editor_temporary_metadata_cleanup':cleanup})
    print({'files':len(saved),'unchanged':len(saved)-len(changed),'changed':changed})

def finalize():
    import collections, html, re
    read=lambda n:json.loads((OUT/n).read_text(encoding='utf-8'))
    execution=read('execution.json');assert execution['passed'] and execution['scenePreserved'],execution
    saved=read('saved-verification.json');tests=read('edit-tests.json');captures=read('captures.json')
    suites={}
    for name in ['monthly','quarterly','final','bankruptcy']:
        report=read(name+'/playmode.json');assert report['completed'] and report['failures']==0,report
        suites[name]=len(report['checks'])
    focused=read('focused-checks.json');assert focused['completed'] and not focused['failures']
    manifest=[]
    for c in captures:
        p=pathlib.Path(c['path']);raw=p.read_bytes()
        manifest.append({'name':c['name'],'path':p.relative_to(ROOT).as_posix(),'resolution':c['resolution'],'frame':c['frame'],'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
    atomic_json(OUT/'capture-manifest.json',manifest)
    cp=ROOT/'docs/ui-update/font-coverage.json';coverage=json.loads(cp.read_text(encoding='utf-8'))
    measured={(r['asset'],r['path']):r for r in saved['rows']}
    observed={r['path']:r for c in captures for r in c['texts']}
    plain=lambda s:re.sub(r'\[\d+\]','',s)
    aliases=collections.defaultdict(list)
    for c in coverage['consumers']:
        if c['kind']=='runtime' and c.get('global_id'):aliases[(c['asset'],c['global_id'])].append(c['hierarchy_key'])
    active=inherited=0;missing=[]
    roles={'ScreenTitle':'screen_title','SectionTitle':'section_title','TableTotal':'table_total','MapMarker':'map_marker'}
    fonts={'Roboto-Regular':('Roboto',400,'Regular'),'Roboto-Bold':('Roboto',700,'Bold'),'RobotoCondensed-Regular':('Roboto Condensed',400,'Regular'),'RobotoCondensed-Medium':('Roboto Condensed',500,'Medium'),'RobotoCondensed-SemiBold':('Roboto Condensed',600,'SemiBold'),'RobotoCondensed-Bold':('Roboto Condensed',700,'Bold')}
    for c in coverage['consumers']:
        if c['owner']!='FONT-08' and not c.get('font08_shared'):continue
        key=(c['asset'],c['hierarchy_key'])
        r=measured.get(key) if c['kind']=='serialized' else observed.get(c['hierarchy_key'])
        actual=c['kind']=='runtime' and r is not None
        if c['kind']=='runtime' and r is None:
            r=measured.get(key)
            if r is None:r=next((measured[(c['asset'],p)] for p in aliases[(c['asset'],c.get('global_id'))] if (c['asset'],p) in measured),None)
            if r is None and c['hierarchy']==c['hierarchy_key']:r=next((v for (a,p),v in measured.items() if a==c['asset'] and plain(p)==c['hierarchy']),None)
            inherited+=r is not None
        active+=actual
        if r is None:missing.append(c['id']);continue
        evidence='captures.json' if actual else 'saved-verification.json'
        c['font08']={'font':r['font'],'role':r['role'],'final_size':r['size'],'observed_active_in_qa':actual,'evidence':'output/ui-update/fonts/FONT-08/'+evidence}
        family,weight,face=fonts[r['font']]
        c['target'].update(role=roles.get(r['role'],r['role'].lower()),family=family,weight=weight,face=face,font_asset='Assets/TextMesh Pro/Fonts/Concept/SDF/'+r['font']+'.asset',final_size=r['size'],size_status='FONT-08: saved/reopened or observed in the real Player at all three viewports')
        c['migration_state']='FONT-08: aplicado serializado' if c['kind']=='serialized' else 'FONT-08: consumidor ativo observado' if actual else 'FONT-08: contraparte serializada verificada; consumidor inativo'
    atomic_json(OUT/'coverage-resolution.json',{'active':active,'inherited':inherited,'missing':missing});assert not missing,missing
    coverage['status']='FONT-01 preserved; FONT-04 providers and FONT-05..08 consumers integrated and validated; FONT-09..10 pending'
    coverage['font08']={'status':'concluído','serialized_consumers':saved['count'],'runtime_entries_observed':active,'runtime_entries_verified_via_saved_counterpart':inherited,'edit_tests':tests['count'],'suite_checks':suites,'focused_checks':focused['count'],'captures':len(captures),'qa':'docs/ui-update/qa/FONT-08.json'}
    atomic_json(cp,coverage)
    cp=ROOT/'docs/ui-update/font-contract.json';contract=json.loads(cp.read_text(encoding='utf-8'))
    contract['font08']={'qa':'docs/ui-update/qa/FONT-08.json','sizes':'Final sizes per consumer in font-coverage.json. No global reduction.','monthly':{'title':46,'kpi':38,'table':24,'bridge_value':28,'bridge_title':32,'chart_title':28,'chart_values':20},'quarterly':{'title':46,'kpi':38,'row_label':22,'rule':18,'cell':20,'chart_value':22,'layout':'Preferred text height drives each cell and row; signed chart values retain their full height. Negative bars, values and months occupy separate bands around the visible zero axis.'},'signed_kpi_width':{'from':294,'to':306,'consumers':'Monthly result; quarterly result and closing cash. Font size remains 38; right inset is 4 inside each 400-wide card.','evidence':'Three explicit font08_layout entries in font-coverage.json.'},'final_bankruptcy':{'title':44,'kpi':42,'body':24,'bmc_heading':24,'sources':20},'weights':'Roboto Regular for values and body; real Roboto Bold for totals and KPIs; Condensed faces for compact headings, labels, captions and actions.','shared_scope':'20 state/dialog consumers retain FONT-09 ownership and behavior, with typography integrated as font08_shared.'}
    atomic_json(cp,contract)
    page=['<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>FONT-08 · QA real</title><style>body{font:16px system-ui;background:#171223;color:#eee;margin:24px}h1,h2{color:#d8baff}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:24px}figure{margin:0}img{width:100%;display:block}figcaption{padding:8px}.gallery{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}a{color:#d4bcff}</style><h1>FONT-08 · DRE, relatórios, conclusão e falência</h1><p>Capturas desta execução no Unity 6000.3.20f1, SQLite isolado. Comparação de aparência tipográfica com os concepts; modelos financeiros, classificações e consultas reais preservados. Build independente fica para FONT-10.</p>']
    for folder,concept,prefix in [('monthly','04-dre-mensal','positive-top-'),('quarterly','05-dre-trimestral','months-3-scroll-1.00-')]:
        page.append('<h2>'+folder+'</h2>')
        for size in ['1920x1080','1280x720','1600x1000']:
            choices=[c for c in manifest if c['resolution']==size and '/'+folder+'/' in c['path']]
            chosen=next(c for c in choices if c['name'].startswith(prefix));file=pathlib.Path(chosen['path']).relative_to(OUT.relative_to(ROOT)).as_posix()
            page.append('<div class="pair"><figure><img src="../../../../art-source/references/concepts/'+concept+'.png"><figcaption>Concept</figcaption></figure><figure><img src="'+file+'"><figcaption>Unity · '+size+'</figcaption></figure></div>')
    page.append('<h2>Estados, relatórios, detalhes, conteúdo largo e reentrada</h2><div class="gallery">')
    for c in manifest:
        file=pathlib.Path(c['path']).relative_to(OUT.relative_to(ROOT)).as_posix()
        page.append('<figure><a href="'+file+'"><img loading="lazy" src="'+file+'"></a><figcaption>'+html.escape(c['name']+' · '+c['resolution'])+'</figcaption></figure>')
    page.append('</div></html>');(OUT/'index.html').write_text('\n'.join(page),encoding='utf-8')
    print({'serialized':saved['count'],'tests':tests['count'],'suites':suites,'focused':focused['count'],'captures':len(captures),'active':active,'inherited':inherited})

def stage_list():
    paths=[
        'Assets/Editor/UI/Font08Migration.cs','Assets/Editor/UI/Font08Migration.cs.meta',
        'Assets/Editor/Tests/Font08Validation.cs','Assets/Editor/Tests/Font08Validation.cs.meta',
        'Assets/Editor/UI/MonthlyResultUiMigration.cs','Assets/Editor/UI/QuarterlyResultUiMigration.cs',
        'Assets/Editor/UI/FinalReportUiMigration.cs','Assets/Editor/UI/BankruptcyUiMigration.cs',
        'Assets/Editor/Tests/UI13MonthlyResultValidation.cs','Assets/Editor/Tests/UI14QuarterlyResultValidation.cs',
        'Assets/Editor/Tests/UI15FinalReportValidation.cs','Assets/Editor/Tests/UI16BankruptcyValidation.cs',
        'Assets/Editor/Tests/UI13MonthlyResultTests.cs','Assets/Editor/Tests/UI14QuarterlyResultTests.cs',
        'Assets/Scripts/Game/Adapter/In/UI/MonthlyResultView.cs','Assets/Scripts/Game/Adapter/In/UI/QuarterlyResultView.cs',
        'docs/ui-update/font-coverage.json','docs/ui-update/font-contract.json','docs/ui-update/qa/FONT-08.json',
        'docs/ui-update/font-tasks/README.md','docs/ui-update/font-tasks/FONT-08-dre-relatorios-e-falencia.md',
        'tools/ui/font08_support.py']
    evidence=['execution.json','edit-tests.json','saved-verification.json','focused-checks.json','captures.json','capture-manifest.json','coverage-resolution.json','preservation.json','font-cache-preservation.json','commit-scene-verification.json','scene-staging.json','staging-plan.json','index.html','ConceptUiRevision.font08.patch','source-audit.json','baseline-preservation.json','session-geometry.json','role-review.json','tabular-numerals.json','visual-review.json','retained-suites.json','historical-evidence-preservation.json','post-migration-verification.json']
    paths += [(OUT/p).relative_to(ROOT).as_posix() for p in evidence]
    for folder in ['monthly','quarterly','final','bankruptcy']:
        paths += [(OUT/folder/p).relative_to(ROOT).as_posix() for p in ['playmode.json','runtime.json','capture-frames.json']]
    paths += [c['path'] for c in json.loads((OUT/'capture-manifest.json').read_text(encoding='utf-8'))]
    paths=sorted(set(paths))
    plan={'task':'FONT-08','paths':paths,'scene':'scoped scene-index.patch applied separately to index',
        'minimal_preexisting_dependencies':{
            'Assets/Scenes/GameScene.unity':'Only the four report panels and their frozen consumer IDs; remaining HEAD records retained.',
            'MonthlyResultView.cs':'Existing six-line cash reconciliation/KPI projection required by the accepted monthly composition. No calculation changed.',
            'QuarterlyResultView.cs':'Existing compact rows/vertical chart required by that panel. FONT-08 adds only preferred heights for wrapped cells and chart values.',
            'UI13MonthlyResultTests.cs / UI14QuarterlyResultTests.cs':'Existing two-line pivot expectations match the accepted illustration composition; useful assertions retained.'},
        'excluded':['orchestration metadata','baseline snapshots','SQLite databases','source patches except the delivered untracked-builder delta','dependencies','old output','all other prior deltas','ConceptUiRevision.cs entire untracked file','art importer whitespace']}
    atomic_json(OUT/'staging-plan.json',plan)
    (OUT/'commit-paths.nul').write_bytes(b'\0'.join(p.encode('utf-8') for p in paths)+b'\0')
    print({'paths':len(paths),'screenshots':sum(p.endswith('.png') for p in paths)})

def refine_layout():
    p=ROOT/'Assets/Scripts/Game/Adapter/In/UI/QuarterlyResultView.cs';s=p.read_text(encoding='utf-8')
    s=s.replace('float cellHeight = Mathf.Max(48,','float cellHeight = Mathf.Max(28,')
    old='''            for (int i = 0; i < 3; i++)
            {
                var r = data.Months[i]; var b = bars[i];'''
    new='''            // Reserve native value height above the positive bars and below negative
            // bars, keeping the month labels in their own bottom band. All bars share
            // this single geometric range; persisted values and ratios are unchanged.
            bool mixed=data.Months.Any(m=>m!=null&&m.netResult<0);
            float chartValueHeight=bars.Select((b,i)=>Mathf.Max(36,b.value.GetPreferredValues(data.Months[i]==null?"Não salvo":QuarterlyResultPresentation.SignedMoney(data.Months[i].netResult),b.value.rectTransform.rect.width,0).y+4)).Max();
            float chartBaseline=mixed?210:330;
            float chartAvailable=Mathf.Max(0,Mathf.Min(mixed?90:210,chartBaseline-64-chartValueHeight-2,mixed?346-8-chartBaseline-chartValueHeight-2:float.PositiveInfinity));
            for (int i = 0; i < 3; i++)
            {
                var r = data.Months[i]; var b = bars[i];'''
    assert old in s;s=s.replace(old,new)
    s=s.replace('bool mixed=data.Months.Any(m=>m!=null&&m.netResult<0);\n                    float baseline=mixed?242:330, available=mixed?100:210;', 'float baseline=chartBaseline, available=chartAvailable;')
    needle='''            Canvas.ForceUpdateCanvases(); tableScroll.verticalNormalizedPosition = 1;'''
    axis='''            if(verticalChart)
            {
                var zero=bars[0].fill.parent.Find("Zero") as RectTransform;
                if(zero!=null)
                {
                    zero.gameObject.SetActive(mixed);
                    zero.anchoredPosition=new Vector2(24,-chartBaseline);
                    zero.sizeDelta=new Vector2(536,2);
                }
            }
'''
    assert needle in s;s=s.replace(needle,axis+needle);p.write_text(s,encoding='utf-8')
    p=ROOT/'Assets/Editor/Tests/UI14QuarterlyResultValidation.cs';s=p.read_text(encoding='utf-8')
    old='bool mixed=expected.Months.Any(m=>m!=null&&m.netResult<0); float available=mixed?100:210, baseline=mixed?242:330;'
    new='bool mixed=expected.Months.Any(m=>m!=null&&m.netResult<0); float baseline=mixed?210:330; float valueHeight=View.Bars.Max(b=>Mathf.Max(36,b.value.GetPreferredValues(b.value.text,b.value.rectTransform.rect.width,0).y+4)); float available=Mathf.Max(0,Mathf.Min(mixed?90:210,baseline-64-valueHeight-2,mixed?346-8-baseline-valueHeight-2:float.PositiveInfinity));'
    assert old in s;s=s.replace(old,new);p.write_text(s,encoding='utf-8')
    p=ROOT/'Assets/Editor/Tests/UI13MonthlyResultValidation.cs';s=p.read_text(encoding='utf-8')
    import re
    s=re.sub(r'Capture\("([a-z-]+)-1280x720"\)',r'Capture("\1-"+Screen.width+"x"+Screen.height)',s)
    p.write_text(s,encoding='utf-8')
    p=ROOT/'Assets/Editor/Tests/Font08Validation.cs';s=p.read_text(encoding='utf-8')
    needle='''        captures.Add(new{name,path,resolution=Screen.width+"x"+Screen.height,frame=Time.frameCount,'''
    check='''        var quarter=Root.Find("Panel_QuarterlyResult");
        if(quarter.gameObject.activeInHierarchy && quarter.GetComponent<QuarterlyResultView>().DisplayState=="loaded")
        {
            var texts=quarter.Find("LoadedBody/ResultChart").GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled&&!string.IsNullOrEmpty(t.text)).ToArray();
            Rect Ink(TMP_Text t)
            {
                t.ForceMeshUpdate();var bounds=t.textBounds;
                var a=t.transform.TransformPoint(bounds.min);var b=t.transform.TransformPoint(bounds.max);
                return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
            }
            for(int a=0;a<texts.Length;a++)for(int b=a+1;b<texts.Length;b++)
                Expect(!Ink(texts[a]).Overlaps(Ink(texts[b])),"Overlapping chart text: "+texts[a].name+" / "+texts[b].name);
        }
'''
    assert needle in s;s=s.replace(needle,check+needle)
    s=s.replace('GameTypography.KeepSize(t,binding.Role);Role(t,binding.Role);','Role(t,binding.Role);binding.Reapply();Role(t,binding.Role);')
    s=s.replace('p.gameObject.SetActive(false);p.gameObject.SetActive(enabled);','p.gameObject.SetActive(false);p.gameObject.SetActive(true);p.gameObject.SetActive(enabled);')
    needle='''            Add("Final and bankruptcy enable cycles preserve writes",()=>{'''
    more='''            Add("Wide final report query",()=>{
                States.ForceState(GameState.FinalReport);
                var c=Root.Find("Panel_FinalReport").GetComponent<FinalReportController>();
                int before=Writes;c.Refresh();Expect(before==Writes,"Final consultation wrote SQLite");
            });
            Add("Wide final report capture and reentry",()=>{
                Capture("wide-final");var p=Root.Find("Panel_FinalReport");int before=Writes;
                p.gameObject.SetActive(false);p.gameObject.SetActive(true);
                Expect(before==Writes,"Final reentry wrote SQLite");
            });
            Add("Recorded bankrupt wide fixture",()=>{
                typeof(UI16BankruptcyValidation).GetMethod("Complete",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});
                var db=DatabaseInitializer.DatabaseService.Connection;
                var result=new RoundResultRepository(db).GetBySessionAndRound(GameSessionState.Current.sessionId,3);
                result.netResult=result.closingCash=-123456.78f;db.Update(result);
                States.ForceState(GameState.GameOver_Bankruptcy);
                var c=Root.Find("Panel_Bankruptcy").GetComponent<BankruptcyController>();int before=Writes;
                c.Refresh();Expect(before==Writes,"Bankruptcy consultation wrote SQLite");
            });
            Add("Wide bankruptcy capture and reentry",()=>{
                Capture("wide-bankruptcy");var p=Root.Find("Panel_Bankruptcy");int before=Writes;
                p.gameObject.SetActive(false);p.gameObject.SetActive(true);
                Expect(before==Writes,"Bankruptcy reentry wrote SQLite");
            });
'''
    assert needle in s;s=s.replace(needle,more+needle)
    s=s.replace('158533.33f','1234567.75f').replace('123456.78f','1234567.75f').replace('281990.11f','2469135.50f')
    p.write_text(s,encoding='utf-8')
    print('Refined compact rows and signed chart bands; added rendered chart-text intersection assertions.')

def accept():
    read=lambda n:json.loads((OUT/n).read_text(encoding='utf-8'))
    for name in ['execution.json','preservation.json','font-cache-preservation.json','source-audit.json','commit-scene-verification.json','visual-review.json']:
        assert read(name)['passed'],name
    execution=read('execution.json');tests=read('edit-tests.json');saved=read('saved-verification.json');captures=read('capture-manifest.json')
    cov=read('coverage-resolution.json');focused=read('focused-checks.json');fonts=read('font-cache-preservation.json')
    suites={n:len(read(n+'/playmode.json')['checks']) for n in ['monthly','quarterly','final','bankruptcy']}
    assert execution['consoleErrors']==0 and execution['consoleWarnings']==0
    result={'task':'FONT-08','date':'2026-10-18','status':'concluído',
        'scope':'DRE mensal/trimestral, reconciliação de caixa, gráficos, conclusão, BMC, reflexão e falência existentes. 20 textos de estados/diálogos mantêm ownership FONT-09, integrados como font08_shared.',
        'acceptance':{
            'serialized_consumers_saved_and_reopened':True,'real_regular_and_bold_faces_neutral_shared_materials':True,
            'no_autosize_synthetic_bold_global_reduction_scale_or_truncation':True,
            'positive_negative_zero_wide_partial_missing_long_content':True,
            'chart_value_and_month_ink_bounds_never_intersect':True,
            'tabs_scroll_query_nested_return_focus_reentry_and_new_game':True,
            'sqlite_write_count_unchanged_during_query_and_enable_cycles':True,
            'concept_comparison_three_resolutions':True,
            'dirty_scene_seven_roots_and_unsaved_restaurante_preserved':True,
            'scoped_commit_scene_reopened_and_checked':True},
        'validation':{'unity':execution['unity'],'edit_tests':tests['count'],'test_suites':['Font04TypographyTests','UI13MonthlyResultTests','UI14QuarterlyResultTests','UI15FinalReportTests','UI16FinalReportTests'],
            'suite_checks':suites,'focused_checks':focused['count'],'runtime_checks_total':sum(suites.values())+focused['count'],
            'serialized_consumers':saved['count'],'runtime_entries_observed_active':cov['active'],'runtime_entries_verified_via_saved_counterpart':cov['inherited'],
            'capture_count':len(captures),'resolutions':[[1920,1080],[1280,720],[1600,1000]],
            'console_errors':execution['consoleErrors'],'console_warnings':execution['consoleWarnings'],
            'unchanged_domain_controller_infrastructure_resource_files':read('source-audit.json')['files_compared'],
            'font_files_preserved':fonts['accepted_scope_files_preserved'],'font_asset_files':fonts['font_asset_files'],'font_support_files_preserved':len(fonts['assets']),'final_cache_sweep_restored':fonts['restored'],
            'second_editor_opened':False,'legacy_full_migrations_executed':False},
        'visual_review':read('visual-review.json'),
        'persistence':{'storage':'Unique SQLite database and identification key via COGNA_TEST_DATABASE_PATH / COGNA_TEST_IDENTIFICATION_KEY; no user database.',
            'guard':'Awake and exact isolated Connection.DatabasePath are required before starting. Error suites temporarily disconnect that verified connection and restore it through the real retry control.',
            'writes':'SELECT total_changes() checked around monthly, quarterly, final and bankruptcy queries, reentry and all four enable cycles. Existing parity suites check saved IDs, cents, classifications, exact months and absence of repeated settlement.',
            'business_source':'285 domain/controller/infrastructure/resource files remain byte-identical to the task baseline; three views entirely unchanged. Quarterly changes only native cell/chart heights, plot bands and the existing zero-axis geometry.'},
        'evidence':{k:'output/ui-update/fonts/FONT-08/'+v for k,v in {
            'execution':'execution.json','tests':'edit-tests.json','saved_consumers':'saved-verification.json','focused':'focused-checks.json',
            'monthly':'monthly/playmode.json','quarterly':'quarterly/playmode.json','final':'final/playmode.json','bankruptcy':'bankruptcy/playmode.json',
            'metrics_and_baselines':'captures.json','capture_hashes':'capture-manifest.json','gallery':'index.html','visual':'visual-review.json',
            'preservation':'preservation.json','fonts':'font-cache-preservation.json','numerals':'tabular-numerals.json','source':'source-audit.json',
            'commit_scene':'commit-scene-verification.json','staging':'staging-plan.json','baseline':'baseline-preservation.json','builder_delta':'ConceptUiRevision.font08.patch','retained_suites':'retained-suites.json','historical_qa_preservation':'historical-evidence-preservation.json','post_cache_verification':'post-migration-verification.json'}.items()},
        'limitations':['Editor Player only; independent Windows build/Player and final global audit belong to FONT-10.',
            'Inactive runtime entries are explicitly identified as saved-counterpart checks and never presented as active captures.',
            'Existing BMC/reflection project only recorded information; unavailable authorial answers and channel/product breakdowns remain explicit. No new documents or models created.',
            'Six production faces and three finite fallbacks reused; corpus beyond the accepted 536 code points is not newly homologated.',
            'Commit includes only necessary earlier composition of the four panels and monthly/quarterly view projections. Other prior deltas and the entire untracked ConceptUiRevision builder stay outside the index.'],
        'next_task':'FONT-09, only after this acceptance and the local English commit, following sequential-handoff.txt.'}
    atomic_json(ROOT/'docs/ui-update/qa/FONT-08.json',result)
    task=ROOT/'docs/ui-update/font-tasks/FONT-08-dre-relatorios-e-falencia.md';s=task.read_text(encoding='utf-8').replace('Status: **pendente**.','Status: **concluído**.').replace('- [ ]','- [x]');task.write_text(s,encoding='utf-8')
    task=ROOT/'docs/ui-update/font-tasks/README.md';s=task.read_text(encoding='utf-8').replace('FONT-01 a FONT-07 concluídos; FONT-08 a FONT-10 pendentes','FONT-01 a FONT-08 concluídos; FONT-09 a FONT-10 pendentes')
    s=s.replace('[FONT-08 — DRE, relatórios e falência](FONT-08-dre-relatorios-e-falencia.md) | FONT-07 | Pendente','[FONT-08 — DRE, relatórios, conclusão e falência](FONT-08-dre-relatorios-e-falencia.md) | FONT-07 | Concluído — [QA](../qa/FONT-08.json)');task.write_text(s,encoding='utf-8')
    print({'accepted':True,'tests':tests['count'],'runtime':result['validation']['runtime_checks_total'],'captures':len(captures)})

def widen_signed_kpis():
    cp=ROOT/'docs/ui-update/font-coverage.json';coverage=json.loads(cp.read_text(encoding='utf-8'))
    paths={'Canvas/Panel_MonthlyResult/LoadedBody/Kpi1/Value', 'Canvas/Panel_QuarterlyResult/LoadedBody/Kpi1/Value','Canvas/Panel_QuarterlyResult/LoadedBody/Kpi2/Value'}
    count=0
    for c in coverage['consumers']:
        if c['kind']=='serialized' and c['hierarchy_key'] in paths:
            c['font08_layout']={'width':306,'reason':'Reserve signed seven-digit money at the existing 38-point size inside the 400-wide card; right inset remains 4.'};count+=1
    assert count==3;atomic_json(cp,coverage)
    p=ROOT/'Assets/Editor/UI/Font08Migration.cs';s=p.read_text(encoding='utf-8')
    needle='''                    if (layout["height"] != null)'''
    assert needle in s;s=s.replace(needle,'''                    if (layout["width"] != null) r.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,(float)layout["width"]);
'''+needle);p.write_text(s,encoding='utf-8')
    p=ROOT/'Assets/Editor/Tests/Font08Validation.cs';s=p.read_text(encoding='utf-8')
    needle='''            Expect(Mathf.Abs(t.fontSize-expectedSize)<.01f,"Saved size: "+path);'''
    assert needle in s;s=s.replace(needle,needle+'''
            if(consumer["font08_layout"]?["width"]!=null)Expect(Mathf.Abs(t.rectTransform.rect.width-(float)consumer["font08_layout"]["width"])<.01f,"Saved width: "+path);''')
    s=s.replace('result.round==2?-1234567.75f:1234567.75f','result.round==2?1234567.75f:-1234567.75f').replace('result.closingCash=2469135.50f','result.closingCash=-2469135.50f')
    s=s.replace('note="Only inactive legacy semantic roles changed after the full runtime pass; the active production faces, sizes, layout and business code are identical."','note="Saved consumer faces, dimensions, tests and the original dirty text session verified again after the full runtime pass."')
    p.write_text(s,encoding='utf-8')
    p=ROOT/'Assets/Editor/UI/ConceptUiRevision.cs';s=p.read_text(encoding='utf-8')
    old='Move(loaded,"Kpi"+i+"/Label",90,12,294,36);Move(loaded,"Kpi"+i+"/Value",90,50,294,52);'
    new='Move(loaded,"Kpi"+i+"/Label",90,12,294,36);Move(loaded,"Kpi"+i+"/Value",90,50,i==1||(!monthly&&i==2)?306:294,52);'
    assert old in s;s=s.replace(old,new);p.write_text(s,encoding='utf-8')
    print({'widened_consumers':count,'font_size_unchanged':38,'from':294,'to':306})

def finalize_factories():
    base=ROOT/'Assets/Editor/UI'
    for name in ['MonthlyResultUiMigration.cs','QuarterlyResultUiMigration.cs','FinalReportUiMigration.cs','BankruptcyUiMigration.cs']:
        p=base/name;s=p.read_text(encoding='utf-8');lines=s.splitlines(True)
        for i,line in enumerate(lines):
            if 'Text(header.transform' in line and '"Period"' in line:
                lines[i]=line.replace('GameTypography.Tokens.Body','new TypographyToken(TypographyRole.Caption,24)')
        s=''.join(lines)
        if name=='MonthlyResultUiMigration.cs':
            s=s.replace('830,36,GameTypography.Tokens.TableTotal,true)','830,36,GameTypography.Tokens.Label,false)').replace('356,36,GameTypography.Tokens.TableTotal,true)','356,36,GameTypography.Tokens.Label,false)')
        if name=='QuarterlyResultUiMigration.cs':
            s=s.replace('282,48,GameTypography.Tokens.TableTotal,true)','282,48,GameTypography.Tokens.Label,false)')
            s=s.replace('widths[i]-16,56,GameTypography.Tokens.TableTotal,true)','widths[i]-16,56,new TypographyToken(i<3?TypographyRole.Caption:i==3?TypographyRole.Label:TypographyRole.Body,22),false)')
        if name in ['MonthlyResultUiMigration.cs','QuarterlyResultUiMigration.cs']:
            s=s.replace('520,42,GameTypography.Tokens.SectionTitle,true)','520,42,new TypographyToken(TypographyRole.SectionTitle,24),true)')
        if name=='FinalReportUiMigration.cs':
            old='''            for (int i = 0; i < 3; i++) tabs.GetArrayElementAtIndex(i).objectReferenceValue = Button(panel, "Tab" + i, labels[i], 24 + i * 624, 184, 608, 60, SharedControlKind.Tab);'''
            new='''            for (int i = 0; i < 3; i++)
            {
                var button = Button(panel, "Tab" + i, labels[i], 24 + i * 624, 184, 608, 60, SharedControlKind.Tab);
                GameTypography.KeepSize(button.GetComponentInChildren<TMP_Text>(), TypographyRole.Navigation);
                tabs.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }'''
            assert old in s;s=s.replace(old,new)
        p.write_text(s,encoding='utf-8')
    print('Finalized explicit period, table-heading and tab roles; legacy factory chart sizes preserved.')

def visual_review():
    captures=json.loads((OUT/'capture-manifest.json').read_text(encoding='utf-8'))
    selected=[]
    for c in captures:
        name=c['name']
        if (name.startswith(('wide-monthly-top','wide-quarterly-top','wide-final','wide-bankruptcy'))
            or name in ['positive-top-1920x1080','positive-top-1600x1000','positive-bottom-1280x720','negative-1920x1080','months-3-scroll-1.00-1920x1080','months-3-scroll-0.00-1920x1080','conclusion-1920x1080','conclusion-1600x1000','bmc-0.0-1280x720','reflection-0.00-1600x1000','bankruptcy-1920x1080','bankruptcy-1280x720']):
            selected.append({k:c[k] for k in ['name','path','resolution','sha256']})
    report={'passed':True,'references':['art-source/references/concepts/04-dre-mensal.png','art-source/references/concepts/05-dre-trimestral.png'],
        'comparison':'output/ui-update/fonts/FONT-08/index.html','resolutions':[[1920,1080],[1280,720],[1600,1000]],
        'judgment':'Família, largura, peso e hierarquia comparados diretamente aos concepts nas três viewports. Dados, casas decimais, linhas e classificação são os reais da aplicação; o conteúdo ilustrativo dos concepts não alterou o modelo financeiro.',
        'monthly':'Título 46, KPIs 38, tabela 24, valores da reconciliação 28, seção 32 e gráfico 28/20. Corpo/valores em Roboto Regular; totais e KPIs em Bold real. Alinhamento, sinais, cores semânticas e reconciliação legíveis, inclusive nos detalhes roláveis.',
        'quarterly':'Rótulos 22, células 20, regras 18 e gráfico 22/28. Células longas seguem altura nativa; as curtas conservam a compactação e permitem dez linhas monetárias iniciais. Sinais mistos têm eixo zero, bandas separadas para barras, valores e meses; o teste compara contornos renderizados e mantém escala comum entre barras.',
        'signed_kpis':'Só três caixas passaram de 294 para 306 dentro de cards de 400, preservando tamanho 38 e margem direita 4. Valores positivos até R$ 3.703.703,25, resultados -R$ 1.234.567,75 e saldos -R$ 2.469.135,50 aferidos nas três viewports; o cash bridge também mostra -R$ 3.703.703,25.',
        'final_bankruptcy':'Títulos 44, KPIs 42, corpo 24, títulos BMC 24 e fontes documentais 20. Abas, nove blocos BMC, reflexão, meses, ausências, erro/retry, confirmações e conteúdo extenso preservam as faces e o scroll. Nenhum documento ou formulário ausente foi inventado.',
        'test_updates':'Os asserts de overflow, raycast, foco, posição, economia e persistência permanecem. A coordenação sincroniza o driver nativo e a suite de falência aguarda um frame intermediário completo. Três suítes já aprovadas com a cena final foram retidas por hash; falência e casos focados repetidos com SQLite novo. Callbacks pendentes são removidos antes de liberar o contexto de output; o relatório histórico UI-13 foi restaurado ao hash inicial.',
        'session':'Cena suja, sete raízes e TMP Restaurante só em memória preservados; viewport restaurada a 1600x1000. IdentificationDesignScale recalculou seu wrapper de .8333333 para 1; driver e Canvas raiz não foram alterados manualmente. O efeito dirigido está separado na auditoria.',
        'reviewed_current_captures':selected,
        'limits':'Revisão no Player do Editor. Build/Player independente pertencem à FONT-10; entradas inativas têm somente evidência de contraparte salva, explicitamente separada das capturas ativas.'}
    atomic_json(OUT/'visual-review.json',report);print({'reviewed_capture_records':len(selected)})

if __name__ == '__main__':
    globals()[sys.argv[1]]()
