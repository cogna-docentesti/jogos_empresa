"""Read-only technical evidence for ART-28; records JSON reports, never edits art."""
from pathlib import Path
import hashlib, json, math, subprocess, sys

ROOT=Path(__file__).resolve().parents[3]
BASE='4df7e36ad6c356fe2f70aadd41e8f1b4cdcea85b'
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def write(p,d): (ROOT/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p): return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def schema_validate(value,schema,location='$'):
    # Implements exactly the assertion keywords present in dre-layout.schema.json.
    supported={'$schema','title','description','type','required','properties','const','items','minItems','maxItems','exclusiveMinimum','additionalProperties'}
    assert set(schema)<=supported,('Unsupported schema keyword',set(schema)-supported)
    if 'const' in schema: assert value==schema['const'],location
    if 'type' in schema:
        checks={'object':isinstance(value,dict),'array':isinstance(value,list),'string':isinstance(value,str),'number':isinstance(value,(int,float)) and not isinstance(value,bool),'integer':isinstance(value,int) and not isinstance(value,bool)}
        assert checks[schema['type']],location
    if 'exclusiveMinimum' in schema: assert value>schema['exclusiveMinimum'],location
    if isinstance(value,dict):
        assert set(schema.get('required',[]))<=set(value),location
        properties=schema.get('properties',{})
        for k,child in value.items():
            if k in properties: schema_validate(child,properties[k],location+'.'+k)
            elif isinstance(schema.get('additionalProperties'),dict): schema_validate(child,schema['additionalProperties'],location+'.'+k)
    if isinstance(value,list):
        assert len(value)>=schema.get('minItems',0),location
        if 'maxItems' in schema: assert len(value)<=schema['maxItems'],location
        if 'items' in schema:
            for i,child in enumerate(value): schema_validate(child,schema['items'],location+'['+str(i)+']')
def validate(*args):
    r=subprocess.run([sys.executable,str(ROOT/'tools/art/validate_assets.py'),'--project',str(ROOT),*args],capture_output=True,text=True,encoding='utf-8')
    d=json.loads(r.stdout)
    if r.returncode or not d['passed']: raise ValueError(d)
    return d
def main():
    m=read('docs/art/manifest.json');p=read('docs/art/prompts/ART-28.json');c=read('docs/art/layouts/dre-layout.json')
    task=validate('--task','ART-28');plan=validate('--plan')
    prereqs={tid:validate('--task',tid) for tid in ['ART-01','ART-09']}
    for tid in prereqs:
        assert next(t for t in m['tasks'] if t['id']==tid)['status']=='validado'
    schema_validate(c,read('docs/art/schemas/dre-layout.schema.json'))
    assets={a['id']:a for a in m['assets'] if a['task_id']=='ART-28'}
    assert len(assets)==6 and {l['asset_id'] for l in c['layers']}==set(assets)
    assert len({l['order'] for l in c['layers']})==6
    layers={l['asset_id']:l for l in c['layers']}
    transformed={}
    for k,v in c['anchor_definitions'].items():
        a=assets[v['asset_id']];l=layers[v['asset_id']]
        q=[l['position_px'][i]+(v['native_px'][i]-a['export_size_px'][i]*l['pivot'][i])*l['scale'] for i in range(2)]
        assert all(abs(q[i]-c['anchors'][k][i])<1e-9 for i in range(2)),(k,q,c['anchors'][k])
        transformed[k]=q
    assert c['anchors']['ch01_hand']==c['anchors']['paper_grip']
    assert c['occlusion_masks']==[] # Preview proves existing z-order is sufficient.
    for l in c['layers']:
        a=assets[l['asset_id']]
        assert math.isfinite(l['scale']) and l['scale']>0
        assert all(a['export_size_px'][i]*l['scale']<=a['display_size_px'][i]+1e-9 for i in range(2))
    for r in p['assets']:
        assert r['sha256']==sha(r['output_path'])==assets[r['id']]['sha256']
        assert r['source_sha256']==sha(r['source_path'])
        for ref,h in r['reference_sha256'].items(): assert sha(ref)==h
    before=json.loads(subprocess.check_output(['git','show',BASE+':docs/art/manifest.json'],cwd=ROOT,text=True,encoding='utf-8'))
    touched=[]
    for field in m:
        if field not in ['assets','tasks']: assert m[field]==before[field],field
    for field,ident in [('assets','id'),('tasks','id')]:
        previous={x[ident]:x for x in before[field]}
        for entry in m[field]:
            old=previous[entry[ident]]
            if entry!=old:
                assert (entry.get('task_id')=='ART-28' if field=='assets' else entry['id']=='ART-28')
                touched.append(entry['id'])
    tree=subprocess.check_output(['git','status','--porcelain=v1','--untracked-files=all'],cwd=ROOT,text=True,encoding='utf-8').splitlines()
    paths=[x[3:] for x in tree]
    approved_prefixes=['art-source/editable/ART-28/','art-source/previews/ART-28/','docs/art/qa/ART-28']
    exact=['docs/art/manifest.json','docs/art/tasks.json','docs/art/prompts/ART-28.json','docs/art/layouts/dre-layout.json']
    pngs=['Assets/Art/new/'+a['path'] for a in assets.values()]
    exact+=pngs+[p+'.meta' for p in pngs]
    assert all(x in exact or any(x.startswith(q) for q in approved_prefixes) for x in paths),paths
    write('docs/art/qa/ART-28-technical.json',task)
    write('docs/art/qa/ART-28-plan-validation.json',plan)
    write('docs/art/qa/ART-28-prerequisites.json',{'passed':True,'tasks':prereqs})
    write('docs/art/qa/ART-28-contract-validation.json',{'passed':True,'schema':'docs/art/schemas/dre-layout.schema.json','anchor_transforms':transformed,'grip_distance_px':0,'uniform_scales':True,'layer_coverage':list(layers),'clip_rect_px':c['clip_rect_px'],'occlusion':'Existing hand alpha above paper; desk above seated lower body. Visual review is recorded separately.'})
    write('docs/art/qa/ART-28-scope-audit.json',{'passed':True,'base_commit':BASE,'manifest_changed_records':touched,'future_manifest_records_unchanged':True,'scope_paths':paths,'game_integration_performed':False})
    print(json.dumps({'passed':True,'asset_count':6,'schema':True,'grip_distance_px':0,'prerequisites':list(prereqs),'scope':True}))
if __name__=='__main__': main()
