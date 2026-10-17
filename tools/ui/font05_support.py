"""FONT-05 baseline, isolated patch staging and evidence packaging."""
import hashlib
import json
import pathlib
import re
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "output/ui-update/fonts/FONT-05"

def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)

def baseline():
    OUT.mkdir(parents=True, exist_ok=True)
    paths = set(git("ls-files").decode().splitlines())
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT / "Assets").rglob("*.cs"))
    manifest = {}
    for path in sorted(paths):
        p = ROOT / path
        if not p.is_file():
            continue
        data = p.read_bytes()
        manifest[path] = hashlib.sha256(data).hexdigest()
        if p.suffix in (".cs", ".unity", ".prefab", ".json", ".md"):
            dst = OUT / "baseline" / path
            dst.parent.mkdir(parents=True, exist_ok=True)
            dst.write_bytes(data)
    (OUT / "baseline.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    for name, args in [("initial-status.txt", ("status", "--short")), ("initial-tracked.patch", ("diff", "--binary")), ("initial-staged.patch", ("diff", "--cached", "--binary"))]:
        (OUT / name).write_bytes(git(*args))
    print(f"Baseline: {len(manifest)} files; HEAD {git('rev-parse', 'HEAD').decode().strip()}")

def blocks(text):
    matches = list(re.finditer(r"^--- !u!(\d+) &(-?\d+).*\n", text, re.M))
    return {int(m[2]): text[m.start(): matches[i+1].start() if i+1 < len(matches) else len(text)] for i,m in enumerate(matches)}

def inspect_scene():
    import collections
    import difflib
    path = "Assets/Scenes/GameScene.unity"
    before = blocks((OUT / "baseline" / path).read_text(encoding="utf-8"))
    after = blocks((ROOT / path).read_text(encoding="utf-8"))
    changed = [i for i in before.keys() & after.keys() if before[i] != after[i]]
    changes = []
    for key in changed:
        for line in difflib.unified_diff(before[key].splitlines(), after[key].splitlines(), n=0):
            if line.startswith(("+", "-")) and not line.startswith(("+++", "---")):
                changes.append(line)
    (OUT / "scene-field-changes.txt").write_text("\n".join(changes), encoding="utf-8")
    counts = collections.Counter(re.sub(r":.*", ":", s.strip()) for s in changes)
    report = {"blocks_before": len(before), "blocks_after": len(after), "added": len(after.keys()-before.keys()), "deleted": len(before.keys()-after.keys()), "changed": len(changed), "fields": dict(counts)}
    (OUT / "scene-delta.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2)[:8000])

def preservation():
    import yaml
    fields = {"m_fontAsset", "m_sharedMaterial", "m_fontWeight", "m_fontStyle", "m_enableAutoSizing", "m_overflowMode", "m_GlobalFontAsset", "m_GlobalPointSize", "m_ActiveFontFeatures"}
    coverage=json.loads((ROOT/"docs/ui-update/font-coverage.json").read_text(encoding="utf-8"))
    authorized={}
    role_guid = re.search(r"guid: (\w+)", (ROOT / "Assets/Scripts/Game/Adapter/In/UI/Theme/TypographyRoleBinding.cs.meta").read_text())[1]
    def normalized(raw, added, key):
        value = yaml.safe_load(raw.split("\n",1)[1])
        data = next(iter(value.values()))
        layout=authorized.get(key,{})
        if "size" in layout:
            data.pop("m_fontSize",None);data.pop("m_fontSizeBase",None)
        if "height" in layout and "m_SizeDelta" in data:data["m_SizeDelta"].pop("y",None)
        if "top" in layout and "m_AnchoredPosition" in data:data["m_AnchoredPosition"].pop("y",None)
        if "m_text" in data and isinstance(data["m_text"],str):
            # TMP_InputField.UpdateLabel appends its non-content caret sentinel.
            data["m_text"]=data["m_text"].rstrip("\u200b") or None
        data.pop("m_EditorClassIdentifier", None)
        for f in fields:
            data.pop(f, None)
        if "m_Component" in data:
            data["m_Component"] = [c for c in data["m_Component"] if c["component"]["fileID"] not in added]
        if "m_Modification" in data:
            m = data["m_Modification"]
            m["m_Modifications"] = [v for v in m.get("m_Modifications",[]) if v["propertyPath"] not in fields]
            m["m_AddedComponents"] = [v for v in m.get("m_AddedComponents",[]) if v["addedObject"]["fileID"] not in added]
        return value
    results=[]
    for path in ["Assets/Scenes/GameScene.unity", "output/ui-update/fonts/FONT-05/live-session-after.unity"]:
        before_path = OUT / ("baseline/"+path if path.startswith("Assets/") else "live-session-before.unity")
        a=blocks(before_path.read_text(encoding="utf-8"));b=blocks((ROOT/path).read_text(encoding="utf-8"))
        def ref(raw,field):
            m=re.search(r"^ +"+field+r": \{fileID: (-?\d+)",raw,re.M)
            return int(m[1]) if m else 0
        rects={ref(raw,"m_GameObject"):i for i,raw in b.items() if raw.startswith("--- !u!224 ")}
        names={i:(re.search(r"^  m_Name: (.*)",raw,re.M)[1]) for i,raw in b.items() if raw.startswith("--- !u!1 ") and re.search(r"^  m_Name: (.*)",raw,re.M)}
        def h(i):
            go=ref(b[i],"m_GameObject");name=names.get(go,"");name=json.loads(name) if name.startswith('"') else name;p=ref(b[i],"m_Father")
            return (h(p)+"/" if p in b else "")+name
        by_path={h(i):i for i in rects.values()}
        authorized={}
        for c in coverage["consumers"]:
            if c["kind"]!="serialized" or c["asset"]!="Assets/Scenes/GameScene.unity" or "font05_layout" not in c:continue
            cid=int(c["global_id"].split("-")[-2]);layout=c["font05_layout"]
            if cid not in b:continue
            authorized[cid]=layout
            rid=rects.get(ref(b[cid],"m_GameObject"))
            if rid:authorized[rid]=layout
        for g in coverage.get("font05_geometry",[]):
            if g["hierarchy"] in by_path:authorized[by_path[g["hierarchy"]]]=g
        added=set(b)-set(a)
        role_added={i for i in added if role_guid in b[i]}
        incidental=[]; unexpected=[]
        for i in a.keys()&b.keys():
            if a[i]==b[i]:continue
            if normalized(a[i],role_added,i)!=normalized(b[i],role_added,i): unexpected.append(i)
            else: incidental.append(i)
        added_other=[i for i in added if i not in role_added]
        # A stripped TMP serializes the existing prefab text reference for the added role binding.
        extra=[i for i in added_other if " stripped\n" not in b[i]]
        result={"path":path,"passed":not unexpected and not extra and not(set(a)-set(b)),"role_bindings_added":len(role_added),"existing_blocks_preserved":len(a),"authorized_typographic_layout_ids":sorted(authorized),"unexpected_changed_ids":unexpected,"unexpected_added_ids":extra,"deleted":list(set(a)-set(b)),"stripped_prefab_references":added_other}
        results.append(result)
    (OUT/"preservation.json").write_text(json.dumps({"passed":all(r["passed"] for r in results),"comparisons":results},indent=2),encoding="utf-8")
    print(json.dumps(results,indent=2))

def scene_index():
    """Narrow baseline composition prerequisites: only FONT-05 panels and shared header trees."""
    path="Assets/Scenes/GameScene.unity"
    head_text=git("show","HEAD:"+path).decode("utf-8")
    head=blocks(head_text); current_text=(ROOT/path).read_text(encoding="utf-8");current=blocks(current_text)
    gos={};transforms={};by_go={};component_go={}
    def ref(raw,field):
        m=re.search(r"^ +"+field+r": \{fileID: (-?\d+)",raw,re.M)
        return int(m[1]) if m else 0
    for i,raw in current.items():
        kind=int(re.match(r"--- !u!(\d+)",raw)[1])
        if kind==1:
            m=re.search(r"^  m_Name: (.*)",raw,re.M)
            if m:
                name=m[1]
                if name.startswith('"'): name=json.loads(name)
                gos[i]=name
        go=ref(raw,"m_GameObject")
        if go:by_go.setdefault(go,[]).append(i);component_go[i]=go
        if kind in (4,224) and go:transforms[i]=(go,ref(raw,"m_Father"))
    cache={}
    def hierarchy(tid,seen=None):
        if tid in cache:return cache[tid]
        if tid not in transforms:return ""
        go,parent=transforms[tid];name=gos.get(go,"#"+str(go));prefix=hierarchy(parent) if parent else ""
        cache[tid]=(prefix+"/" if prefix else "")+name
        return cache[tid]
    scopes={"Canvas/Panel_Identification","Canvas/Panel_Location","Canvas/Panel_Menu","Canvas/SharedComponentsPreview","Canvas/MenuAccess","Canvas/HubHistory"}
    coverage=json.loads((ROOT/"docs/ui-update/font-coverage.json").read_text(encoding="utf-8"))
    for c in coverage["consumers"]:
        if c.get("font05_shared")=="true" and c["kind"]=="serialized" and c["scope"]=="production-scene":
            owners=[b["owner"] for b in c["bindings"] if b["component"].endswith(("ConceptHudView","MenuHudView"))]
            scopes.update(owners or [c["hierarchy"].rsplit("/",1)[0]])
    selected_go={go for tid,(go,_) in transforms.items() if any(hierarchy(tid)==s or hierarchy(tid).startswith(s+"/") for s in scopes)}
    selected=set(selected_go)
    for go in selected_go:selected.update(by_go.get(go,[]))
    # Include prefab instance override records rooted below these exact scopes.
    for i,raw in current.items():
        parent=ref(raw,"m_TransformParent")
        if parent in selected:selected.add(i)
    # A header prefab is parented to an out-of-scope panel. Its frozen targetPrefabId identifies the override record directly.
    for c in coverage["consumers"]:
        if c["kind"]=="serialized" and c["scope"]=="production-scene" and (c["owner"]=="FONT-05" or c.get("font05_shared")=="true"):
            pid=int(c["global_id"].split("-")[-1])
            if pid in current:selected.add(pid)
    for i,raw in current.items():
        if ref(raw,"m_PrefabInstance") in selected:selected.add(i)
    prerequisites=[]
    while True:
        missing=set()
        for i in selected:
            for match in re.finditer(r"\{fileID: (-?\d+)\}",current[i]):
                target=int(match[1])
                if target and target in current and target not in head and target not in selected:missing.add(target)
        if not missing:break
        prerequisites.extend(sorted(missing));selected.update(missing)
    result=dict(head)
    for i in selected:result[i]="\n".join(line.rstrip(" \t") for line in current[i].split("\n"))
    preamble=head_text[:head_text.index("--- !u!")]
    # Preserve all original declaration order. Unity resolves by local ID; new scoped records precede SceneRoots.
    root_ids=[i for i,raw in head.items() if "\nSceneRoots:\n" in raw]
    order=[i for i in head if i not in root_ids]+sorted(set(result)-set(head))+root_ids
    indexed=preamble+"".join(result[i] for i in order)
    (OUT/"scene-for-index.unity").write_text(indexed,encoding="utf-8",newline="")
    (OUT/"scene-head.unity").write_text(head_text,encoding="utf-8",newline="")
    report={"scope_roots":sorted(scopes),"selected_blocks":len(selected),"new_blocks":len(selected-set(head)),"replaced_blocks":sum(i in head and current[i]!=head[i] for i in selected),"unchanged_head_blocks_outside_scope":len(set(head)-selected),"dependency_closure_ids":prerequisites,
        "reason":"Typography consumers introduced by the preexisting composition need their scoped hierarchy and serialized references. Other panels, art metadata, restaurant catalogs and unsaved Editor extras are excluded."}
    (OUT/"scene-staging.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps(report,indent=2)[:6000])

def patches(index=False):
    import difflib
    source="Assets/Editor/UI/ConceptUiRevision.cs"
    a=(OUT/"baseline"/source).read_text(encoding="utf-8").splitlines(True)
    b=(ROOT/source).read_text(encoding="utf-8").splitlines(True)
    (OUT/"ConceptUiRevision.font05.patch").write_text("".join(difflib.unified_diff(a,b,fromfile="a/"+source,tofile="b/"+source)),encoding="utf-8",newline="")
    base=OUT/"scene-head.unity"
    if index:
        base=OUT/"scene-patch-base.unity";base.write_bytes(git("show",":Assets/Scenes/GameScene.unity"))
    command=["git","diff","--no-index","--no-ext-diff","--",str(base),str(OUT/"scene-for-index.unity")]
    diff=subprocess.run(command,cwd=ROOT,capture_output=True).stdout.decode("utf-8")
    lines=diff.splitlines(True)
    for i,line in enumerate(lines):
        if line.startswith("diff --git "):lines[i]="diff --git a/Assets/Scenes/GameScene.unity b/Assets/Scenes/GameScene.unity\n"
        elif line.startswith("--- "):lines[i]="--- a/Assets/Scenes/GameScene.unity\n"
        elif line.startswith("+++ "):lines[i]="+++ b/Assets/Scenes/GameScene.unity\n"
    (OUT/"scene-index.patch").write_text("".join(lines),encoding="utf-8",newline="")
    print("Prepared scoped scene patch and applied composition-builder delta.")

def finalize():
    import html
    execution=json.loads((OUT/"execution.json").read_text(encoding="utf-8"))
    if not execution.get("passed") or "transientEditorExtraSuppression" not in execution:
        raise RuntimeError("Wait for the current final validation to pass.")
    focused=json.loads((OUT/"focused-checks.json").read_text(encoding="utf-8"))
    saved=json.loads((OUT/"saved-verification.json").read_text(encoding="utf-8"))
    captures=json.loads((OUT/"captures.json").read_text(encoding="utf-8"))
    campaign=json.loads((OUT/"campaigns/playmode.json").read_text(encoding="utf-8"))
    campaign_frames=json.loads((OUT/"campaigns/capture-frames.json").read_text(encoding="utf-8"))
    assert focused["completed"] and not focused["failures"] and campaign["completed"] and campaign["failures"]==0
    manifests=[]
    for c in captures+campaign_frames:
        path=pathlib.Path(c["path"])
        data=path.read_bytes()
        manifests.append({"name":c["name"],"path":path.relative_to(ROOT).as_posix(),"sha256":hashlib.sha256(data).hexdigest(),"bytes":len(data),"frame":c["frame"]})
    (OUT/"capture-manifest.json").write_text(json.dumps(manifests,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    coverage_path=ROOT/"docs/ui-update/font-coverage.json"
    coverage=json.loads(coverage_path.read_text(encoding="utf-8"))
    measured={(r["asset"],r["path"]):r for r in saved["rows"]}
    runtime={t["path"]:t for c in captures for t in c["texts"]}
    applied=0
    for c in coverage["consumers"]:
        key=(c["asset"],c["hierarchy_key"])
        if key in measured:
            r=measured[key];c["target"]["final_size"]=r["size"];c["target"]["size_status"]="FONT-05: salvo e reaberto; consumidor e tamanho verificados"
            c["migration_state"]="FONT-05: aplicado serializado" if c["kind"]=="serialized" else "FONT-05: template serializado e binding de reentrada"
            c["font05"]={"saved_font":r["font"],"saved_role":r["role"],"final_size":r["size"],"observed_active_in_focused_qa":c["hierarchy"] in runtime,"evidence":"output/ui-update/fonts/FONT-05/saved-verification.json"}
            applied+=1
        if c["kind"]=="runtime" and c["hierarchy"] in runtime and c["owner"]=="FONT-05":
            r=runtime[c["hierarchy"]];c["font05"]={"runtime_font":r["font"],"runtime_role":r["role"],"final_size":r["size"],"evidence":"output/ui-update/fonts/FONT-05/captures.json"}
    coverage["status"]="FONT-01 preservado; providers FONT-04 e consumidores FONT-05 integrados e validados; FONT-06..10 pendentes"
    coverage["font05"]={"status":"concluído","serialized_consumers":saved["count"],"focused_captures":len(captures),"campaign_captures":len(campaign_frames),"qa":"docs/ui-update/qa/FONT-05.json","scene_session_delta_preserved":True}
    coverage_path.write_text(json.dumps(coverage,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    contract_path=ROOT/"docs/ui-update/font-contract.json";contract=json.loads(contract_path.read_text(encoding="utf-8"))
    contract["font05"]={"qa":"docs/ui-update/qa/FONT-05.json","sizes":"Final sizes per consumer in font-coverage.json; global tokens remain start sizes for pending screens.","identification":{"title":72,"labels":30,"input_and_placeholder":30,"action":36,"storage_note":20},"hub":{"brand":44,"header_values":23,"sidebar_title":34,"company_body":24,"metric_values":26,"navigation":24,"map_markers":32},"layout":"Company 88 design units for three lines; adjacent text, metric rows and progress moved. Map affine transform unchanged."}
    contract_path.write_text(json.dumps(contract,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    page=['<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>FONT-05 · QA real</title><style>body{font:16px system-ui;background:#171223;color:#eee;margin:24px}h1,h2{color:#d8baff}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:32px}figure{margin:0}img{width:100%;display:block}figcaption{padding:8px}.crop{aspect-ratio:16/9;position:relative;overflow:hidden}.crop img{position:absolute;width:200%;height:200%;max-width:none}.location img{left:-100%;top:0}.identification img{left:0;top:0}.gallery{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}a{color:#d4bcff}pre{white-space:pre-wrap}</style><h1>FONT-05 · componentes, HUD, hub e entrada</h1><p>Capturas reais do Editor 6000.3.20f1; 1920×1080, 1280×720 e 1600×1000. Concepts são referência de altura aparente e hierarquia. Textos e dados reais do jogo preservados.</p>']
    for name,concept,cls in [('identification-reentry','02-abertura.png','identification'),('location-selected','02-abertura.png','location'),('hub','01-hub.png','')]:
        page.append('<h2>'+name+'</h2>')
        for size in ['1920x1080','1280x720','1600x1000']:
            left='<div class="crop '+cls+'"><img src="../../../../art-source/references/concepts/'+concept+'"></div>' if cls else '<img src="../../../../art-source/references/concepts/'+concept+'">'
            page.append('<div class="pair"><figure>'+left+'<figcaption>Concept '+concept+'</figcaption></figure><figure><img src="'+name+'-'+size+'.png"><figcaption>Player real · '+size+'</figcaption></figure></div>')
    page.append('<h2>Inputs, caret, seleção, erro, estados e reentrada</h2><div class="gallery">')
    for c in captures:
        filename=pathlib.Path(c['path']).name
        page.append('<figure><a href="'+filename+'"><img loading="lazy" src="'+filename+'"></a><figcaption>'+html.escape(c['name']+' · '+c['resolution'])+'</figcaption></figure>')
    page.append('</div><p>Prefabs sem consumidor ativo e o showcase inativo foram verificados por serialização e testes; nenhum painel foi inventado. Nenhum TMP_Dropdown foi encontrado no escopo de produção. O TMP não salvo Canvas/Text (TMP), “Restaurante”, foi preservado na sessão e teve somente o renderer ocultado no player descartável.</p></html>')
    (OUT/"index.html").write_text("\n".join(page),encoding="utf-8")
    print(json.dumps({"serialized":saved["count"],"inventory_entries_updated":applied,"focused_checks":focused["count"],"campaign_checks":len(campaign["checks"]),"captures":len(manifests)},indent=2))

def stage_list():
    paths=[
        "Assets/Editor/UI/Font05Migration.cs","Assets/Editor/UI/Font05Migration.cs.meta",
        "Assets/Editor/Tests/Font05Validation.cs","Assets/Editor/Tests/Font05Validation.cs.meta",
        "Assets/Editor/Tests/UI04HubTests.cs","Assets/Editor/Tests/UI05LocationTests.cs",
        "Assets/Editor/Tests/UI04HubValidation.cs","Assets/Editor/Tests/UI05LocationValidation.cs",
        "Assets/Editor/UI/HubUiMigration.cs","Assets/Scripts/Game/Adapter/In/UI/MenuHudView.cs",
        "Assets/Scripts/Game/Adapter/In/UI/ConceptHudView.cs","Assets/Scripts/Game/Adapter/In/UI/ConceptHudView.cs.meta",
        "Assets/Scripts/Game/Adapter/In/UI/SharedControlAppearance.cs",
        "docs/ui-update/font-coverage.json","docs/ui-update/font-contract.json",
        "docs/ui-update/font-tasks/README.md","docs/ui-update/font-tasks/FONT-05-componentes-hud-e-entrada.md",
        "docs/ui-update/qa/FONT-05.json","tools/ui/font05_support.py",
    ]
    coverage=json.loads((ROOT/"docs/ui-update/font-coverage.json").read_text(encoding="utf-8"))
    paths+=sorted({c["asset"] for c in coverage["consumers"] if c["kind"]=="serialized" and c["owner"]=="FONT-05" and c["asset"].endswith(".prefab")})
    evidence=["execution.json","edit-tests.json","saved-verification.json","focused-checks.json","captures.json","capture-manifest.json","preservation.json","font-cache-preservation.json","commit-scene-verification.json","scene-staging.json","staging-plan.json","index.html","ConceptUiRevision.font05.patch","source-audit.json"]
    paths+=["output/ui-update/fonts/FONT-05/"+name for name in evidence]
    paths += ["output/ui-update/fonts/FONT-05/campaigns/"+name for name in ["playmode.json","capture-frames.json","state-transitions.json","runtime-bindings.json","runtime.json"]]
    paths+=[c["path"] for c in json.loads((OUT/"capture-manifest.json").read_text(encoding="utf-8"))]
    paths=sorted(set(paths))
    assert all((ROOT/p).is_file() for p in paths if not p.endswith("staging-plan.json"))
    dependencies={
        "scene":"Only FONT-05 scene subtrees/shared headers and their prefab override records; outside-scope HEAD blocks retained exactly. Scene staged by patch, never git add.",
        "ConceptHudView":"Preexisting untracked runtime type is a minimum dependency for serialized header/sidebar references; now contains explicit role reapplication.",
        "SharedControlAppearance":"Includes the six-line preexisting Navigation paint branch required by the scoped navbar, with explicit disabled contrast fixed in FONT-05.",
        "UI04HubTests_and_UI05LocationTests":"Small preexisting map-geometry expectations are necessary for the scoped composition prerequisite; callback and polygon assertions retained.",
        "ConceptUiRevision":"Preexisting untracked editor builder stays untracked; FONT-05 delta applied in workspace and supplied as a patch.",
    }
    report={"task":"FONT-05","paths":paths,"scene_staged_separately":"Assets/Scenes/GameScene.unity","baseline":"output/ui-update/fonts/FONT-05/baseline.json (local only)","minimum_dependencies":dependencies,"excluded":"Other prior source/data/art changes, backups, dependencies, SQLite files and unsaved Editor extras.","scene_snapshot_sha256":hashlib.sha256((OUT/"scene-for-index.unity").read_bytes()).hexdigest()}
    (OUT/"staging-plan.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    (OUT/"commit-paths.nul").write_bytes(b"\0".join(p.encode("utf-8") for p in paths)+b"\0")
    print(f"Explicit stage list: {len(paths)} paths plus the separately audited scene.")

if __name__ == "__main__":
    if sys.argv[1] == "baseline":
        baseline()
    elif sys.argv[1] == "inspect":
        inspect_scene()
    elif sys.argv[1] == "preservation":
        preservation()
    elif sys.argv[1] == "scene-index":
        scene_index()
    elif sys.argv[1] == "patches":
        patches()
    elif sys.argv[1] == "patches-index":
        patches(index=True)
    elif sys.argv[1] == "finalize":
        finalize()
    elif sys.argv[1] == "stage-list":
        stage_list()
