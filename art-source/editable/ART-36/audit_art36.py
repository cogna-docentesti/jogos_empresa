"""Read-only audit of ART-01..35. Writes ART-36 reports, never production assets."""
from pathlib import Path
import collections
import hashlib
import importlib.util
import json
import math
import re
import sys
sys.dont_write_bytecode = True
import yaml
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
QA = ROOT / 'docs/art/qa'
BASE = '5d18996a6ff6cebc5e6dab0c04eaef4d5e93e107'

def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8'))

def sha(path):
    return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()

def save(name, data):
    (QA / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def schema_check(value, schema, at='$'):
    """Validate every assertion keyword used in the two checked-in schemas.

    Deliberately rejects unsupported keywords rather than pretending to be a
    general-purpose JSON Schema implementation. No package install is needed.
    """
    supported = {'$schema','title','description','type','required','properties',
                 'const','enum','exclusiveMinimum','minItems','maxItems','items',
                 'additionalProperties'}
    assert not set(schema) - supported, (at, set(schema) - supported)
    kinds = {'object':dict,'array':list,'number':(int,float),'integer':int,'string':str}
    errors = []
    typ = schema.get('type')
    if typ and (not isinstance(value,kinds[typ]) or typ in ['number','integer'] and isinstance(value,bool)):
        return [at + ': type ' + typ]
    if 'const' in schema and value != schema['const']: errors.append(at + ': const')
    if 'enum' in schema and value not in schema['enum']: errors.append(at + ': enum')
    if 'exclusiveMinimum' in schema and value <= schema['exclusiveMinimum']: errors.append(at + ': minimum')
    if isinstance(value,dict):
        errors += [at + ': missing ' + key for key in schema.get('required',[]) if key not in value]
        for key,item in value.items():
            sub = schema.get('properties',{}).get(key,schema.get('additionalProperties',{}))
            if isinstance(sub,dict): errors += schema_check(item,sub,at+'.'+key)
    if isinstance(value,list):
        for key,op in [('minItems',lambda n,m:n<m),('maxItems',lambda n,m:n>m)]:
            if key in schema and op(len(value),schema[key]): errors.append(at + ': ' + key)
        for i,item in enumerate(value): errors += schema_check(item,schema.get('items',{}),f'{at}[{i}]')
    return errors

def audit():
    manifest = read('docs/art/manifest.json')
    assets = [a for a in manifest['assets'] if int(a['task_id'][4:]) <= 35]
    lookup = {a['id']:a for a in assets}
    spec = importlib.util.spec_from_file_location('validator',ROOT/'tools/art/validate_assets.py')
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    reports = [validator.validate_task(ROOT,manifest,f'ART-{i:02d}') for i in range(1,36)]
    errors = [e for r in reports for e in r['errors']]
    results = {r['id']:r for report in reports for r in report['results']}
    records, imports, identities = [], [], []
    for asset in assets:
        aid = asset['id']
        path = manifest['asset_root']+'/'+asset['path']
        result = results[aid]
        if asset['status'] != 'validado': errors.append(aid+': prior status not validated')
        if result.get('sha256') != asset.get('sha256'): errors.append(aid+': manifest SHA256 mismatch')
        if result.get('size') != asset.get('actual_size_px'): errors.append(aid+': actual_size_px mismatch')
        references = [{'path':x,'sha256':sha(x)} for x in asset['references']]
        source = asset.get('source_editable')
        if source and not (ROOT/source).is_file(): errors.append(aid+': missing source '+source)
        meta_path = path+'.meta'
        meta = yaml.safe_load((ROOT/meta_path).read_text(encoding='utf-8')) if (ROOT/meta_path).is_file() else {}
        importer = meta.get('TextureImporter',{})
        border = importer.get('spriteBorder',{})
        actual_border = [border.get(k) for k in ['x','y','z','w']]
        pivot = importer.get('spritePivot',{})
        actual_pivot = [pivot.get('x'),pivot.get('y')]
        pending = []
        if importer.get('textureType') != 8: pending.append('textureType Sprite')
        if importer.get('spriteMode') != 1: pending.append('Single mode; review existing sub-sprite references before conversion')
        if actual_pivot != asset['pivot']: pending.append('manifest pivot')
        if actual_border != asset['nine_slice_lbrt_px']: pending.append('manifest slice borders')
        if importer.get('enableMipMap',importer.get('mipmaps',{}).get('enableMipMap')) != 0: pending.append('disable mipmaps')
        if importer.get('maxTextureSize',0) < max(asset['export_size_px']): pending.append('max texture size')
        if any(x.get('textureCompression') != 0 for x in importer.get('platformSettings',[]) if x.get('buildTarget')=='DefaultTexturePlatform'):
            pending.append('uncompressed QA import')
        sprites = importer.get('spriteSheet',{}).get('sprites',[]) or []
        imp = {'id':aid,'path':meta_path,'sha256':sha(meta_path) if meta else None,'guid':meta.get('guid'),
               'spriteMode':importer.get('spriteMode'),'pivot':actual_pivot,'border_lbrt_px':actual_border,
               'sub_sprites':[{'name':s.get('name'),'rect':s.get('rect'),'pivot':s.get('pivot'),'border':s.get('border')} for s in sprites],
               'pending_integration':pending}
        imports.append(imp)
        with Image.open(ROOT/path) as im:
            alpha = im.getchannel('A')
            bbox = alpha.getbbox()
            edge = max(alpha.crop(box).getextrema()[1] for box in [(0,0,im.width,1),(0,im.height-1,im.width,im.height),(0,0,1,im.height),(im.width-1,0,im.width,im.height)])
        records.append({**result,'path':path,'status':asset['status'],'task_id':asset['task_id'],
                        'display_size_px':asset['display_size_px'],'display_fit':asset['display_fit'],
                        'pivot':asset['pivot'],'nine_slice_lbrt_px':asset['nine_slice_lbrt_px'],
                        'alpha_policy':asset['alpha_policy'],'outer_edge_alpha_max':edge,
                        'transparent_margins_ltrb_px':[bbox[0],bbox[1],result['size'][0]-bbox[2],result['size'][1]-bbox[3]],
                        'origin':asset['source'],'production_record':asset.get('production_record'),
                        'generation_tool':asset.get('generation_tool'),'generation_model':asset.get('generation_model'),
                        'exact_prompt':asset.get('generation_prompt'),'references':references,
                        'source_editable':source,'source_sha256':sha(source) if source else None,
                        'character_ids':asset['character_ids'],'meta_report':'docs/art/qa/ART-36-import-audit.json'})
    for ch in read('docs/art/characters.json'):
        path = manifest['asset_root']+'/'+ch['canonical_asset']
        ok = sha(path)==ch['identity_hash']==lookup[ch['id']+'_CANONICAL']['sha256']
        identities.append({'id':ch['id'],'path':path,'sha256':sha(path),'hash_matches':ok,
                           'dependent_asset_ids':[a['id'] for a in assets if ch['id'] in a['character_ids']]})
        if not ok: errors.append(ch['id']+': canonical hash mismatch')
    # Stable-state groups share complete alpha, dimensions, manifest pivot and borders.
    groups = {}
    for a in assets:
        if a['task_id'] in ['ART-04','ART-05','ART-09','ART-13'] and any(a['id'].endswith('_'+s) for s in ['NORMAL','HOVER','PRESSED','DISABLED','FOCUS','INVALID','SELECTED']):
            prefix = a['id'].rsplit('_',1)[0]
            groups.setdefault(prefix,[]).append(a)
    state_reports = []
    for prefix in ['UI_RADIO','UI_CHECKBOX','UI_TOGGLE']:
        groups[prefix]=[a for a in assets if a['id'].rsplit('_',1)[0]==prefix]
    for name,group in groups.items():
        if len(group)<2: continue
        alpha_hashes=[]
        for a in group:
            with Image.open(ROOT/manifest['asset_root']/a['path']) as im: alpha_hashes.append(hashlib.sha256(im.getchannel('A').tobytes()).hexdigest())
        stable = all(a['export_size_px']==group[0]['export_size_px'] and a['pivot']==group[0]['pivot'] and a['nine_slice_lbrt_px']==group[0]['nine_slice_lbrt_px'] for a in group)
        same_alpha = len(set(alpha_hashes))==1
        state_reports.append({'family':name,'ids':[a['id'] for a in group],'same_geometry':stable,'identical_alpha':same_alpha,
                              'alpha_bboxes':[results[a['id']]['alpha_bbox'] for a in group]})
        if not stable: errors.append(name+': unstable state geometry')
    contracts=[]
    for name in ['map','dre']:
        path=f'docs/art/layouts/{name}-layout.json'
        data=read(path)
        failures=schema_check(data,read(f'docs/art/schemas/{name}-layout.schema.json'))
        errors.extend(failures)
        contracts.append({'path':path,'sha256':sha(path),'schema_checked':True,'schema_errors':failures})
    mp=read('docs/art/layouts/map-layout.json')
    map_layers=[]
    for layer in mp['layers']:
        a=lookup[layer['asset_id']];x,y=layer['position_px'];w,h=a['export_size_px']
        ok = 0<=x<=1920-w and 0<=y<=1080-h
        if not ok: errors.append(a['id']+': map layer outside master')
        map_layers.append({'asset_id':a['id'],'position_px':[x,y],'texture_size_px':[w,h],'inside_master':ok})
    dre=read('docs/art/layouts/dre-layout.json');dre_anchors=[]
    for name,c in [('monthly',dre),('quarterly',dre['quarterly'])]:
        layers={l['asset_id']:l for l in c['layers']}
        for anchor,d in c['anchor_definitions'].items():
            layer=layers[d['asset_id']];a=lookup[d['asset_id']];s=layer['scale']
            measured=[layer['position_px'][i]+(d['native_px'][i]-a['export_size_px'][i]*layer['pivot'][i])*s for i in [0,1]]
            delta=math.dist(measured,c['anchors'][anchor])
            dre_anchors.append({'variant':name,'anchor':anchor,'measured_px':measured,'declared_px':c['anchors'][anchor],'error_px':delta})
            if delta>1e-6: errors.append(name+': incorrect DRE anchor '+anchor)
    dependencies=[]
    for task in manifest['tasks'][:35]:
        q=read(f'docs/art/qa/{task["id"]}.json')
        dependencies.append({'task_id':task['id'],'manifest_status':task['status'],'qa_status':q.get('status'),
                             'asset_count':len(task['asset_ids']),'files_passed':reports[int(task['id'][4:])-1]['passed']})
        if task['status']!='validado' or q.get('status')!='validado': errors.append(task['id']+': prerequisite status')
    save('ART-36-prerequisites.json',{'task_id':'ART-36','passed':not errors,'base_commit':BASE,'tasks':dependencies,'reports':reports})
    save('ART-36-inventory.json',{'task_id':'ART-36','base_commit':BASE,'asset_count':len(records),'passed':not errors,'errors':errors,'assets':records,'canonical_identities':identities})
    save('ART-36-import-audit.json',{'task_id':'ART-36','read_only':True,'integration_performed':False,
         'issue_policy':'Importer proposals are integration work, not new sprite production. Existing GUIDs/sub-sprite references are preserved.',
         'assets_with_pending_import':sum(bool(x['pending_integration']) for x in imports),
         'issue_counts':dict(collections.Counter(i for a in imports for i in a['pending_integration'])), 'assets':imports})
    save('ART-36-contracts.json',{'task_id':'ART-36','passed':not errors,'errors':errors,'schema_method':'Strict local evaluator of every keyword actually used by the checked-in schemas; unsupported keywords fail.',
         'contracts':contracts,'state_families':state_reports,'map_layers':map_layers,'dre_anchor_measurements':dre_anchors,
         'map_compact_sidecars':'Historical active_contract_sha256 values describe their production revision; current authoritative contract is map-layout.json. No sidecar rewritten.',
         'integration_performed':False})
    # Scan serialized references without loading Unity or changing any importer.
    guid_to_id={x['guid']:x['id'] for x in imports if x['guid']}
    consumers={a['id']:[] for a in assets}
    scene_scalers=[]
    scanned=[]
    for file in sorted((ROOT/'Assets').rglob('*')):
        if file.suffix not in ['.unity','.prefab','.asset','.mat','.controller','.overrideController','.playable']:continue
        rel=file.relative_to(ROOT).as_posix()
        lines=file.read_text(encoding='utf-8',errors='replace').splitlines()
        scanned.append(rel)
        for line_no,line in enumerate(lines,1):
            for guid in re.findall(r'guid: ([a-f0-9]{32})',line):
                if guid in guid_to_id:consumers[guid_to_id[guid]].append({'path':rel,'line':line_no,'serialized_reference':line.strip()})
            if file.suffix=='.unity' and 'm_ReferenceResolution:' in line:
                scene_scalers.append({'path':rel,'line':line_no,'value':line.strip()})
    cat=read('docs/art/catalogos.json');catalog_live=[]
    for family,items in cat.items():
        if not isinstance(items,list):continue
        for item in items:
            if not isinstance(item,dict) or not item.get('source'):continue
            source=ROOT/item['source'];content=source.read_text(encoding='utf-8')
            match=re.search(r'^  id: (.+)$',content,re.M)
            live_id=match.group(1).strip() if match else None
            catalog_live.append({'family':family,'id':item['id'],'source':item['source'],'live_id':live_id,
                                 'snapshot_id_matches':live_id==item['id']})
            if live_id!=item['id']:errors.append(item['source']+': live catalog ID differs')
    routes={}
    for a in assets:
        tid=int(a['task_id'][4:])
        if tid<=2:usage='Canonical reference only; reuse identity in poses, do not use concept sheets as sprites'
        elif tid<=9:usage='Shared UI/HUD; compose text/icons and bases as separate objects'
        elif tid<=13:usage='LocationSelectionScene map; apply authoritative map-layout.json'
        elif tid==14:usage='0_Identification; apply identification-layout.json proposal'
        elif tid in [15,27]:usage='Restaurant choice / GameScene establishment review'
        elif tid==16:usage='Location detail panel; independent vignette'
        elif 17<=tid<=19:usage='ProductData icon / menu and cart cards'
        elif 20<=tid<=24:usage='EquipmentData icon / equipment cards'
        elif tid==25:usage='CreditLineData icon / finance; CH01 and environment remain independent'
        elif tid==26:usage='RoleData icon / team cards; portrait and environment remain independent'
        elif tid in [28,29]:usage='GameScene DRE; apply dre-layout.json anchors, uniform scale and clipping'
        elif 30<=tid<=33:usage='EventData illustration; title, effects and all 3 options remain native UI'
        elif tid==34:usage='Results / difficulty / empty state illustration; native labels and controls'
        else:usage='Loading/tutorial overlay; rotation and visibility controlled by future UI'
        candidates=[x for x in catalog_live if x['id']==a.get('catalog_id')]
        routes[a['id']]={'path':manifest['asset_root']+'/'+a['path'],'catalog_id':a.get('catalog_id'),
                        'suggested_usage':usage,'catalog_targets':candidates,
                        'serialized_consumers':consumers[a['id']],
                        'integration_status':'serialized_reference_present_requires_runtime_review' if consumers[a['id']] else 'not_referenced_in_scanned_serialized_files',
                        'display_size_px':a['display_size_px'],'fit':a['display_fit'],'pivot':a['pivot'],
                        'nine_slice_lbrt_px':a['nine_slice_lbrt_px'],
                        'import_pending':next(x['pending_integration'] for x in imports if x['id']==a['id'])}
    save('ART-36-integration-map.json',{'task_id':'ART-36','integration_performed':False,
         'static_scan_only':True,'runtime_tested':False,'serialized_files_scanned':len(scanned),
         'scan_scope':scanned,'assets_with_serialized_consumers':sum(bool(x) for x in consumers.values()),
         'scene_scalers':scene_scalers,'catalog_snapshot_verified_against_live_ids':catalog_live,
         'assets':routes,'limitations':['Static GUID references do not prove a visible or reachable runtime UI. Dynamic asset loading, scaling, clipping and input are untested.'],
         'errors':errors})
    print(json.dumps({'assets':len(records),'passed':not errors,'errors':errors,'import_pending':sum(bool(x['pending_integration']) for x in imports),
                      'state_families':state_reports},ensure_ascii=False,indent=2))

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    audit()
