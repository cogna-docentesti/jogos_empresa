"""Read-only image checks and recorded evidence for ART-29; never edits art."""
from pathlib import Path
import hashlib, json, math, re, runpy, subprocess, sys
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
BASE='b8019babaf19d246cac2b79a2d0a816de04a6fc6'
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
def write(p,d):(ROOT/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def old(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT)
def validate(*args):
    r=subprocess.run([sys.executable,str(ROOT/'tools/art/validate_assets.py'),'--project',str(ROOT),*args],capture_output=True,text=True,encoding='utf-8')
    d=json.loads(r.stdout)
    assert r.returncode==0 and d['passed'],d
    return d
def main():
    m=read('docs/art/manifest.json');p=read('docs/art/prompts/ART-29.json');c=read('docs/art/layouts/dre-layout.json');q=c['quarterly']
    assets={a['id']:a for a in m['assets']};selected={k:a for k,a in assets.items() if a['task_id']=='ART-29'}
    assert set(selected)=={'CH01_DISCUSSING','CH04_DISCUSSING'}
    previous_contract=json.loads(old('docs/art/layouts/dre-layout.json'))
    assert {k:v for k,v in c.items() if k not in ['quarterly','updated_by_tasks']}==previous_contract
    schema_validate=runpy.run_path(str(ROOT/'art-source/editable/ART-28/verify-art28.py'))['schema_validate']
    schema=read('docs/art/schemas/dre-layout.schema.json')
    schema_validate(c,schema);schema_validate(q,schema)
    layers={l['asset_id']:l for l in q['layers']}
    assert set(layers)==set(q['shared_assets_unchanged'])|set(selected)
    assert len(layers)==7 and len({l['order'] for l in q['layers']})==7
    transforms={}
    for k,v in q['anchor_definitions'].items():
        a=assets[v['asset_id']];l=layers[v['asset_id']]
        point=[l['position_px'][i]+(v['native_px'][i]-a['export_size_px'][i]*l['pivot'][i])*l['scale'] for i in range(2)]
        assert point==q['anchors'][k]
        transforms[k]=point
    assert q['anchors']['ch01_hand']==q['anchors']['paper_grip']
    assert abs(q['anchors']['ch04_rest_hand'][1]-q['anchors']['desk_back_edge'][1])<3
    assert q['occlusion_masks']==[]
    for l in q['layers']:
        a=assets[l['asset_id']]
        assert math.isfinite(l['scale']) and l['scale']>0
        assert all(a['export_size_px'][i]*l['scale']<=a['display_size_px'][i]+1e-9 for i in range(2))
    shared=[]
    for aid in q['shared_assets_unchanged']:
        a=assets[aid];path=m['asset_root']+'/'+a['path']
        assert sha(path)==a['sha256']==hashlib.sha256(old(path)).hexdigest()
        assert (ROOT/(path+'.meta')).read_bytes().replace(b'\r\n',b'\n')==old(path+'.meta').replace(b'\r\n',b'\n')
        shared.append({'id':aid,'path':path,'sha256':sha(path),'unchanged_from_base':True})
    for r in p['assets']:
        assert sha(r['output_path'])==r['sha256']==selected[r['id']]['sha256']
        assert sha(r['source_path'])==r['source_sha256']
        assert sha(r['canonical_identity']['path'])==r['canonical_identity']['sha256']
        for ref,h in r['reference_sha256'].items():assert sha(ref)==h
        assert re.search(r'^guid: [0-9a-f]{32}$',(ROOT/(r['output_path']+'.meta')).read_text(),re.M)
        x0,y0,x1,y1=r['measurements']['alpha_bbox_ltrb_px'];w,h=r['actual_size_px']
        assert min(x0/w,(w-x1)/w,y0/h,(h-y1)/h)>=0.1-1e-9
    for g in p['generations']:assert sha(g['source_path'])==g['source_sha256']
    a=Image.open(ROOT/'art-source/previews/ART-28/ART-28-monthly-composite.png');b=Image.open(ROOT/'art-source/previews/ART-29/ART-29-monthly-regression.png')
    assert a.size==b.size and a.tobytes()==b.tobytes()
    assert sha('art-source/previews/ART-28/ART-28-monthly-composite.png')==sha('art-source/previews/ART-29/ART-29-monthly-regression.png')
    before=json.loads(old('docs/art/manifest.json'));touched=[]
    for field in m:
        if field not in ['assets','tasks']:assert m[field]==before[field],field
    for field in ['assets','tasks']:
        previous={x['id']:x for x in before[field]}
        assert len(m[field])==len(previous)
        for e in m[field]:
            if e!=previous[e['id']]:
                assert (e.get('task_id')=='ART-29' if field=='assets' else e['id']=='ART-29')
                touched.append(e['id'])
    task_list=read('docs/art/tasks.json');old_tasks={t['id']:t for t in json.loads(old('docs/art/tasks.json'))}
    assert all(t==old_tasks[t['id']] or t['id']=='ART-29' for t in task_list)
    tree=subprocess.check_output(['git','status','--porcelain=v1','--untracked-files=all'],cwd=ROOT,text=True,encoding='utf-8').splitlines();paths=[x[3:] for x in tree]
    allowed_prefixes=['art-source/editable/ART-29/','art-source/previews/ART-29/','docs/art/qa/ART-29']
    exact=['docs/art/manifest.json','docs/art/tasks.json','docs/art/prompts/ART-29.json','docs/art/layouts/dre-layout.json']
    pngs=[m['asset_root']+'/'+a['path'] for a in selected.values()];exact+=pngs+[s+'.meta' for s in pngs]
    assert all(s in exact or any(s.startswith(pre) for pre in allowed_prefixes) for s in paths),paths
    write('docs/art/qa/ART-29-scope-audit.json',{'passed':True,'base_commit':BASE,'manifest_changed_records':touched,'other_manifest_records_unchanged':True,'scope_paths':paths,'monthly_contract_preserved':True,'game_integration_performed':False})
    if '--scope-only' not in sys.argv:
        task=validate('--task','ART-29');plan=validate('--plan');prereqs={tid:validate('--task',tid) for tid in ['ART-01','ART-28']}
        for tid in prereqs:assert next(t for t in m['tasks'] if t['id']==tid)['status']=='validado'
        write('docs/art/qa/ART-29-technical.json',task);write('docs/art/qa/ART-29-plan-validation.json',plan)
        write('docs/art/qa/ART-29-prerequisites.json',{'passed':True,'tasks':prereqs,'shared_assets':shared})
        write('docs/art/qa/ART-29-contract-validation.json',{'passed':True,'schema':'docs/art/schemas/dre-layout.schema.json','schema_assertions':'All assertion keywords used by this schema, same verifier as ART-28. Root monthly contract and nested quarterly contract checked separately.','anchor_transforms':transforms,'grip_distance_px':0,'mentor_hand_to_desk_back_edge_y_distance_px':abs(q['anchors']['ch04_rest_hand'][1]-q['anchors']['desk_back_edge'][1]),'uniform_scales':True,'layer_coverage':list(layers),'clip_rect_px':q['clip_rect_px'],'occlusion_masks_required':False,'monthly_contract_preserved':True,'monthly_composite_png_and_pixels_identical':True,'shared_assets':shared})
    print(json.dumps({'passed':True,'schema':True,'asset_count':2,'monthly_preserved':True,'scope':True,'scope_only':'--scope-only' in sys.argv}))
if __name__=='__main__':main()
