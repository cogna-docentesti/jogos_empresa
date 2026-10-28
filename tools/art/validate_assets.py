"""Read-only validation of Cogna production plan or selected PNG assets."""
from pathlib import Path
import argparse, json, hashlib, sys, collections, math
from PIL import Image

def load(project):
    return json.loads((project/'docs/art/manifest.json').read_text(encoding='utf-8'))

def validate_plan(project, data):
    errors=[]
    root=(project/data['asset_root']).resolve()
    editable_root=(project/data['editable_root']).resolve()
    if not root.is_relative_to(project): errors.append('Asset root outside project')
    assets=data['assets'];tasks=data['tasks'];ids=[a['id'] for a in assets]
    paths=[a['path'].lower() for a in assets];task_ids=[t['id'] for t in tasks]
    for label, values in [('asset IDs',ids),('file paths',paths),('task IDs',task_ids)]:
        if len(values)!=len(set(values)): errors.append('Duplicate '+label)
    assigned=[aid for t in tasks for aid in t['asset_ids']]
    if collections.Counter(assigned)!=collections.Counter(ids): errors.append('Tasks do not partition all assets exactly once')
    for a in assets:
        target=(root/a['path']).resolve()
        if not target.is_relative_to(root.resolve()): errors.append(a['id']+': path outside asset root')
        if a['task_id'] not in task_ids: errors.append(a['id']+': unknown task')
        if len(a['export_size_px'])!=2 or min(a['export_size_px'])<=0: errors.append(a['id']+': invalid export size')
        l,b,r,t=a['nine_slice_lbrt_px'];w,h=a['export_size_px']
        if min(l,b,r,t)<0 or l+r>=w or b+t>=h: errors.append(a['id']+': invalid slice borders')
        dw,dh=a['display_size_px']
        if a.get('display_fit')=='sliced' and (l+r>=dw or b+t>=dh):errors.append(a['id']+': slice borders consume display rectangle')
        for ref in a['references']:
            target_ref=(project/ref).resolve()
            if not target_ref.is_relative_to(project):errors.append(a['id']+': reference outside project')
            elif not target_ref.is_file():errors.append(a['id']+': missing reference '+ref)
        if a.get('source_editable') and not (project/a['source_editable']).resolve().is_relative_to(editable_root):errors.append(a['id']+': editable source outside art-source/editable')
    seen=set();visiting=set();lookup={t['id']:t for t in tasks}
    def visit(tid):
        if tid in visiting: errors.append('Dependency cycle at '+tid);return
        if tid in seen:return
        visiting.add(tid)
        for dep in lookup[tid]['dependencies']:
            if dep not in lookup:errors.append('Unknown dependency '+dep)
            else:visit(dep)
        visiting.remove(tid);seen.add(tid)
    for tid in lookup:visit(tid)
    for task in tasks:
        for aux in task.get('auxiliary_deliverables',[]):
            if not (project/aux['schema']).is_file():errors.append(task['id']+': missing auxiliary schema')
            if not (project/aux['path']).resolve().is_relative_to(project/'docs/art/layouts'):errors.append(task['id']+': composition contract outside docs/art/layouts')
    cat=json.loads((project/'docs/art/catalogos.json').read_text(encoding='utf-8'))
    for family,kind in [('Equipments','equipamento'),('Products','produto'),('CreditLines','credito'),('Events','evento')]:
        expected={x['id'] for x in cat[family]};actual=[a['catalog_id'] for a in assets if a['kind']==kind]
        if set(actual)!=expected or len(actual)!=len(expected):errors.append('Incomplete/duplicate catalog coverage '+family)
    if data['counts']['assets_total']!=len(assets):errors.append('Asset count differs from manifest')
    return {'mode':'plan','assets':len(assets),'tasks':len(tasks),'errors':errors,'passed':not errors}

def validate_task(project,data,tid):
    if tid not in {t['id'] for t in data['tasks']}:return {'passed':False,'errors':['Unknown task '+tid]}
    root=(project/data['asset_root']).resolve()
    selected=[a for a in data['assets'] if a['task_id']==tid];errors=[];results=[]
    task=next(t for t in data['tasks'] if t['id']==tid)
    def vector(value,length=2):
        return isinstance(value,list) and len(value)==length and all(isinstance(x,(int,float)) and not isinstance(x,bool) and math.isfinite(x) for x in value)
    for aux in task.get('auxiliary_deliverables',[]):
        sidecar=project/aux['path']
        if not sidecar.is_file():errors.append('Missing composition contract '+aux['path']);continue
        try:
            contract=json.loads(sidecar.read_text(encoding='utf-8'))
            for field in aux['required_fields']:
                if field not in contract:errors.append(aux['path']+': missing field '+field)
            if contract.get('coordinate_origin')!='top_left_y_down':errors.append(aux['path']+': unsupported coordinate origin')
            camera=contract.get('camera',{})
            required_camera={'orientation_description','light_direction_description'} | ({'projection'} if sidecar.name=='map-layout.json' else set())
            if not isinstance(camera,dict) or not required_camera.issubset(camera):errors.append(aux['path']+': incomplete camera/light specification')
            if not isinstance(contract.get('layers'),list) or not contract['layers']:errors.append(aux['path']+': no composition layers')
            for layer in contract.get('layers',[]):
                if not {'asset_id','position_px','scale','order','pivot'}.issubset(layer):errors.append(aux['path']+': incomplete layer')
                elif not isinstance(layer['scale'],(int,float)) or layer['scale']<=0:errors.append(aux['path']+': scale must be uniform positive number')
                elif not vector(layer['position_px']) or not vector(layer['pivot']):errors.append(aux['path']+': invalid layer coordinates/pivot')
                elif layer['asset_id'] not in {a['id'] for a in data['assets']}:errors.append(aux['path']+': unknown layer asset')
            if sidecar.name=='map-layout.json':
                if contract.get('master_size_px')!=[1920,1080]:errors.append('Map master size differs')
                transform=contract.get('display_transform',{})
                if not {'uniform_scale','offset_px','target_viewport_px','fit_mode'}.issubset(transform):errors.append('Map lacks complete uniform display transform')
                elif not isinstance(transform['uniform_scale'],(int,float)) or transform['uniform_scale']<=0 or not vector(transform['offset_px']) or not vector(transform['target_viewport_px'],4) or transform['fit_mode'] not in ['contain','cover']:errors.append('Map display transform has invalid values')
                expected={x['id'] for x in json.loads((project/'docs/art/catalogos.json').read_text(encoding='utf-8'))['Locations']}
                if {x.get('catalog_id') for x in contract.get('zones',[])}!=expected:errors.append('Map zone coverage differs')
                for zone in contract.get('zones',[]):
                    if not vector(zone.get('label_anchor_px')) or len(zone.get('polygon_px',[]))<3 or not all(vector(p) for p in zone.get('polygon_px',[])):errors.append('Map zone lacks usable polygon/anchor')
            elif sidecar.name=='dre-layout.json':
                if contract.get('canvas_px')!=[1920,1080]:errors.append('DRE canvas size differs')
                if not {'ch01_hand','paper_grip','desk_front_edge'}.issubset(contract.get('anchors',{})):errors.append('DRE alignment anchors missing')
                elif not all(vector(point) for point in contract['anchors'].values()):errors.append('DRE anchor coordinates invalid')
        except Exception as exc:errors.append(aux['path']+': invalid JSON '+str(exc))
    for a in selected:
        target=(root/a['path']).resolve();item={'id':a['id'],'path':str(target)}
        if not target.is_relative_to(root.resolve()):errors.append(a['id']+': outside root');continue
        if not target.is_file():errors.append(a['id']+': missing PNG');results.append({**item,'exists':False});continue
        if a.get('production_method')=='vector_or_code' and (not a.get('source_editable') or not (project/a['source_editable']).is_file()):errors.append(a['id']+': missing editable source')
        try:
            with Image.open(target) as probe:
                probe.verify()
            with Image.open(target) as im:
                item.update({'exists':True,'size':list(im.size),'mode':im.mode,'format':im.format})
                if im.format!='PNG':errors.append(a['id']+': not PNG')
                if im.mode!='RGBA':errors.append(a['id']+': not RGBA')
                if list(im.size)!=a['export_size_px']:errors.append(a['id']+': export size differs')
                if im.mode=='RGBA':
                    alpha=im.getchannel('A');ext=alpha.getextrema();hist=alpha.histogram();pixels=im.width*im.height
                    item.update({'alpha_extrema':list(ext),'transparent_fraction':hist[0]/pixels,'opaque_fraction':hist[255]/pixels,'alpha_bbox':alpha.getbbox()})
                    if a['alpha_policy'] in ['recorte','contorno'] and hist[0]==0:errors.append(a['id']+': no truly transparent pixels')
                    if ext[1]==0:errors.append(a['id']+': fully invisible image')
                    if a['alpha_policy']=='semitransparente' and not any(hist[1:255]):errors.append(a['id']+': expected semitransparency')
            item['sha256']=hashlib.sha256(target.read_bytes()).hexdigest()
        except Exception as exc:errors.append(a['id']+': unreadable image '+str(exc))
        results.append(item)
    return {'mode':'task','task':tid,'asset_count':len(selected),'results':results,'errors':errors,'passed':not errors,
            'visual_qa_required':True,'note':'This validates files only; style, identity, composition and map alignment require visual review.' if selected else 'This task has no production PNGs; follow its validation/briefing instructions.'}

def main():
    p=argparse.ArgumentParser(description=__doc__)
    location=p.add_mutually_exclusive_group()
    location.add_argument('--project',help='Repository root; defaults to the project containing this script')
    location.add_argument('--root',help='Legacy asset-root argument (Assets/Art/new)')
    group=p.add_mutually_exclusive_group(required=True);group.add_argument('--plan',action='store_true');group.add_argument('--task');args=p.parse_args()
    project=Path(args.project).resolve() if args.project else (Path(args.root).resolve().parents[2] if args.root else Path(__file__).resolve().parents[2])
    data=load(project)
    report=validate_plan(project,data) if args.plan else validate_task(project,data,args.task.upper())
    print(json.dumps(report,ensure_ascii=False,indent=2));return 0 if report['passed'] else 1
if __name__=='__main__':
    if hasattr(sys.stdout,'reconfigure'):sys.stdout.reconfigure(encoding='utf-8')
    raise SystemExit(main())
