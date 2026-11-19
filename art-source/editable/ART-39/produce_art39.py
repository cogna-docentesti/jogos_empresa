"""Technical exports and inspection boards for two independent ART-39 generations.
Artwork and alpha originate in integrated ImageGen; only uniform resampling,
transparent padding, sRGB profile, metadata and inspection composites are added.
No segmentation, repainting or background removal is performed here.
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
import uuid
from PIL import Image, ImageCms, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
TASK = 'ART-39'
BASE = '1a9d59652ac379d52ac853066937a0c063cb3bec'
PROMPTS = 'docs/art/prompts/ART-39.json'
PREVIEW = 'art-source/previews/ART-39'
PRODUCER = 'art-source/editable/ART-39/produce_art39.py'
MASTER = 'Assets/Art/new/characters/CH07/ch07_canonical.png'
BACKGROUNDS = ['#F5F0E8', '#24113D']
IDS = ['CH07_CANONICAL', 'PORTRAIT_WAITER']


def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8'))


def write(path, value):
    dest = ROOT/path
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, encoding='utf-8').strip()


def alpha_metrics(im):
    a = im.getchannel('A')
    h = a.histogram()
    w, height = im.size
    b = a.getbbox()
    edges = [(0,0,w,1),(0,height-1,w,height),(0,0,1,height),(w-1,0,w,height)]
    return {
        'alpha_extrema': list(a.getextrema()),
        'alpha_bbox_ltrb_px': list(b),
        'visible_bbox_alpha128_ltrb_px': list(a.point(lambda x: 255 if x>=128 else 0).getbbox()),
        'transparent_fraction': h[0]/(w*height), 'opaque_fraction': h[255]/(w*height),
        'outer_edge_alpha_max': max(a.crop(e).getextrema()[1] for e in edges),
        'transparent_margins_ltrb_px': [b[0],b[1],w-b[2],height-b[3]],
    }


def composite(im, bg):
    out = Image.new('RGBA', im.size, bg)
    out.alpha_composite(im)
    return out.convert('RGB')


def contain(im, box):
    scale = min(box[0]/im.width,box[1]/im.height)
    size = (round(im.width*scale),round(im.height*scale))
    out = Image.new('RGBA',box)
    out.alpha_composite(im.resize(size,Image.Resampling.LANCZOS),
                        ((box[0]-size[0])//2,(box[1]-size[1])//2))
    return out


def preview(name, im):
    path = ROOT/PREVIEW/name
    path.parent.mkdir(parents=True,exist_ok=True)
    im.save(path)
    return PREVIEW+'/'+name


def export(asset_id, scale):
    assert git('rev-parse','HEAD') == BASE
    assert git('branch','--show-current') == 'dev/new-screens'
    for a in read('docs/art/qa/ART-39-prerequisites.json')['baseline_assets']:
        assert sha(a['path']) == a['sha256'], a['id']
    m = read('docs/art/manifest.json')
    a = next(a for a in m['assets'] if a['id']==asset_id)
    assert a['task_id']==TASK and a['status']=='planejado'
    final = m['asset_root']+'/'+a['path']
    p = read(PROMPTS)
    g = next(g for g in p['generations'] if g['asset_id']==asset_id and g['selected'])
    im = Image.open(ROOT/g['source_path'])
    assert im.mode=='RGBA'
    size = tuple(a['export_size_px'])
    resampled = (round(im.width*scale),round(im.height*scale))
    offset = ((size[0]-resampled[0])//2,(size[1]-resampled[1])//2)
    assert min(offset)>=0
    out = Image.new('RGBA',size)
    out.alpha_composite(im.resize(resampled,Image.Resampling.LANCZOS),offset)
    (ROOT/final).parent.mkdir(parents=True,exist_ok=True)
    profile = ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
    if (ROOT/final).exists():
        # Resume only an identical own-task export after an interrupted record.
        existing = Image.open(ROOT/final)
        assert existing.mode==out.mode and existing.size==out.size
        assert existing.tobytes()==out.tobytes() and existing.info.get('icc_profile')
    else:
        out.save(ROOT/final,icc_profile=profile)
    metrics = alpha_metrics(out)
    assert metrics['outer_edge_alpha_max']==0
    l,t,r,b = metrics['transparent_margins_ltrb_px']
    assert min(l,r)>=size[0]*.1 and min(t,b)>=size[1]*.1, metrics
    # Use the existing validated Single/FullRect importer as technical template.
    meta = (ROOT/'Assets/Art/new/characters/CH01/ch01_guiding_question.png.meta').read_text(encoding='utf-8')
    meta = re.sub(r'^guid: [0-9a-f]+$', 'guid: '+uuid.uuid4().hex, meta, flags=re.M)
    meta = re.sub(r'^    spriteID: [0-9a-f]+$', '    spriteID: '+uuid.uuid4().hex, meta, flags=re.M)
    meta = re.sub(r'^  userData:.*$', '  userData: ART-39 CH07 art extension; no scene or role integration', meta, flags=re.M)
    if (ROOT/(final+'.meta')).exists():
        assert 'userData: ART-39 CH07 art extension' in (ROOT/(final+'.meta')).read_text(encoding='utf-8')
    else:
        (ROOT/(final+'.meta')).write_text('\n'.join(line.rstrip() for line in meta.splitlines())+'\n',encoding='utf-8')
    if asset_id=='CH07_CANONICAL':
        folder = ROOT/'Assets/Art/new/characters/CH07.meta'
        if not folder.exists():
            folder.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: ART-39 CH07 canonical identity\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
    refs = ['art-source/references/concepts/03-gestao.png','Assets/Art/new/characters/CH06/ch06_canonical.png'] if asset_id=='CH07_CANONICAL' else [MASTER,'art-source/references/concepts/03-gestao.png']
    g.update(source_sha256=sha(g['source_path']),source_size_px=list(im.size),
             reference_sha256={ref:sha(ref) for ref in refs})
    record = {
        'id':asset_id,'path':final,'status':'gerado','mode':out.mode,
        'actual_size_px':list(size),'sha256':sha(final),
        'source_path':g['source_path'],'source_sha256':sha(g['source_path']),
        'source_size_px':list(im.size),'tool':'image_gen.imagegen','model':None,
        'references':refs,'reference_sha256':{ref:sha(ref) for ref in refs},
        'character_ids':['CH07'],'canonical_asset':MASTER,'canonical_sha256':sha(MASTER),
        'alpha':metrics,'display_size_px':a['display_size_px'],'display_fit':'contain',
        'pivot':a['pivot'],'nine_slice_lbrt_px':a['nine_slice_lbrt_px'],
        'technical_export':{
            'tool':'Pillow','uniform_scale':scale,'source_size_px':list(im.size),
            'resampled_size_px':list(resampled),'transparent_offset_px':list(offset),
            'output_size_px':list(size),'producer_source':PRODUCER,
            'operation':'Uniform full-frame Lanczos resample, transparent padding and sRGB profile only; original generated RGBA preserved.',
        },
        'qa':{'technical':False,'visual':False,'identity':False},
    }
    p['assets'].append(record)
    write(PROMPTS,p)
    a.update(status='gerado',source='ART-39 CH07 new identity; integrated ImageGen',
             production_method='imagegen',generation_tool='image_gen.imagegen',
             generation_model=None,generation_model_note=p['model_note'],
             references=refs,sha256=sha(final),actual_size_px=list(size),
             generation_prompt=g['prompt'],production_record=PROMPTS,
             source_raster=g['source_path'],source_raster_sha256=sha(g['source_path']),
             technical_export=record['technical_export'])
    write('docs/art/manifest.json',m)
    print(asset_id, size, metrics)


def proofs():
    p = read(PROMPTS)
    paths = []
    for a in p['assets']:
        im = Image.open(ROOT/a['path']).convert('RGBA')
        for bg,label in zip(BACKGROUNDS,['light','dark']):
            paths.append(preview(a['id']+'-native-'+label+'.png',composite(im,bg)))
        dw,dh = a['display_size_px']
        display = contain(im,(dw,dh))
        board = Image.new('RGB',(dw*2,dh))
        for i,bg in enumerate(BACKGROUNDS):
            board.paste(composite(display,bg),(dw*i,0))
        paths.append(preview(a['id']+'-display-light-dark.png',board))
    if len(p['assets'])==2:
        master = Image.open(ROOT/MASTER).convert('RGBA')
        portrait = Image.open(ROOT/'Assets/Art/new/team/portraits/waiter.png').convert('RGBA')
        board = Image.new('RGB',(1440,680))
        d = ImageDraw.Draw(board)
        samples = [(master,BACKGROUNDS[0],'CH07 canonical'),(portrait,BACKGROUNDS[0],'CH07 waiter portrait'),
                   (master,BACKGROUNDS[1],'CH07 canonical'),(portrait,BACKGROUNDS[1],'CH07 waiter portrait')]
        for i,(im,bg,label) in enumerate(samples):
            d.rectangle((i*360,0,(i+1)*360-1,679),fill=bg)
            d.text((i*360+16,16),label,fill='#30243C' if i<2 else '#F5F0E8')
            im=im.copy()
            im.thumbnail((340,630),Image.Resampling.LANCZOS)
            board.paste(composite(im,bg),(360*i+(360-im.width)//2,40+(630-im.height)//2))
        paths.append(preview('CH07-identity-comparison.png',board))
        board = Image.new('RGB',(1280,700))
        d = ImageDraw.Draw(board)
        details = [(master,(435,210,605,410),'Canonical face'),(portrait,(205,65,325,205),'Portrait face'),
                   (master,(330,745,425,900),'Canonical hand L'),(master,(645,730,735,900),'Canonical hand R'),
                   (portrait,(220,355,350,445),'Portrait hands')]
        for row,bg in enumerate(BACKGROUNDS):
            d.rectangle((0,row*350,1279,(row+1)*350-1),fill=bg)
            for col,(im,box,label) in enumerate(details):
                d.text((col*256+16,row*350+16),label+' / 1:1',fill='#30243C' if row==0 else '#F5F0E8')
                board.paste(composite(im,bg).crop(box),(col*256+16,row*350+55))
        paths.append(preview('CH07-details-1x.png',board))
    write('docs/art/qa/ART-39-preview-index.json',{
        'task_id':TASK,'previews':paths,'backgrounds':BACKGROUNDS,
        'display_fit':'contain','purpose':'Asset and identity inspection only; not final UI.',
    })
    print('Created',len(paths),'native/display proofs.')


def validate():
    for selector,output in [(['--task',TASK],'ART-39-technical.json'),(['--plan'],'ART-39-plan-validation.json')]:
        r = subprocess.run([sys.executable,'tools/art/validate_assets.py','--project',str(ROOT),*selector],
                           cwd=ROOT,capture_output=True,encoding='utf-8')
        report = json.loads(r.stdout)
        write('docs/art/qa/'+output,report)
        assert r.returncode==0 and report['passed'],report
    print('Task and plan validation passed.')


if __name__=='__main__':
    if sys.argv[1]=='export': export(sys.argv[2],float(sys.argv[3]))
    else: {'proofs':proofs,'validate':validate}[sys.argv[1]]()
