"""Verify exact RGBA reconstruction, compact metadata and unchanged map assembly; create QA previews only."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[3]
private=Path('C:/Users/Pichau/.codex/art-runtime/python')
if private.is_dir():sys.path.insert(0,str(private))
from jsonschema import Draft202012Validator
PREVIEW=ROOT/'art-source/previews/ART-11/compact';PREVIEW.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def font(n):return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',n)
def background(im,color):
    out=Image.new('RGBA',im.size,color);out.alpha_composite(im);return out.convert('RGB')
def save(im,name):im.save(PREVIEW/name);return 'art-source/previews/ART-11/compact/'+name

m=read('docs/art/manifest.json');production=read('docs/art/prompts/ART-11-compact.json')
contract=read('docs/art/layouts/map-landmarks-compact.json');layout=read('docs/art/layouts/map-layout.json')
schema=read('docs/art/schemas/map-layout.schema.json');Draft202012Validator.check_schema(schema)
schema_errors=[e.message for e in Draft202012Validator(schema).iter_errors(layout)];assert not schema_errors,schema_errors
records=[];reconstructed={};old_images={};before_hashes={}
for r in production['assets']:
    old_path=ROOT/r['source_path'];path=ROOT/r['output_path'];before_hashes[r['id']]=sha(path)
    old=Image.open(old_path).convert('RGBA');small_probe=Image.open(path);assert small_probe.mode=='RGBA' and small_probe.info.get('icc_profile')
    small=small_probe.convert('RGBA');x,y,w,h=r['crop_rect_px'];assert small.size==(w,h)
    alpha=np.array(small.getchannel('A'));assert int(alpha.min())==0 and int(alpha.max())>0
    assert not np.any(alpha[:2,:]) and not np.any(alpha[-2:,:]) and not np.any(alpha[:,:2]) and not np.any(alpha[:,-2:])
    # Paste does not blend or premultiply; this proves equality of all RGBA bytes.
    rebuilt=Image.new('RGBA',(1920,1080),(0,0,0,0));rebuilt.paste(small,(x,y))
    delta=np.abs(np.array(rebuilt).astype(np.int16)-np.array(old).astype(np.int16))
    assert not np.any(delta),r['id']+' reconstructed RGBA differs'
    assert sha(old_path)==r['source_sha256'] and sha(path)==r['sha256']
    assert r['ground_anchor_local_px']==[r['ground_anchor_master_px'][0]-x,r['ground_anchor_master_px'][1]-y]
    a=next(a for a in m['assets'] if a['id']==r['id']);layer=next(a for a in layout['layers'] if a['asset_id']==r['id'])
    assert a['export_size_px']==[w,h] and a['map_position_px']==[x,y]
    assert layer['position_px']==[x,y] and layer['texture_size_px']==[w,h] and layer['pivot']==[0,1] and layer['scale']==1
    assert r['unity_master_rect']['anchored_position_px']==[x,-y]
    detail=Image.new('RGB',(w*2+48,h+82),'#DDD5E4');d=ImageDraw.Draw(detail)
    d.text((16,12),r['id']+' | '+str(w)+'x'+str(h)+' | '+str([x,y]),font=font(18),fill='#24113D')
    detail.paste(background(small,'#F5F0E8'),(16,58));detail.paste(background(small,'#24113D'),(32+w,58))
    detail_preview=save(detail,r['id']+'-compact-detail.png')
    reconstructed[r['id']]=rebuilt;old_images[r['id']]=old
    records.append({'id':r['id'],'path':r['output_path'],'actual_size_px':[w,h],'position_px':[x,y],'pivot':[0,1],
        'sha256':r['sha256'],'rgba_pixels_reconstructed_equal':True,'maximum_rgba_delta':int(delta.max()),
        'transparent_border_px':2,'mode':'RGBA','srgb_profile_present':True,
        'source_bytes':r['source_bytes'],'output_bytes':r['output_bytes'],'rgba8_bytes_before':r['rgba8_bytes_before'],'rgba8_bytes_after':r['rgba8_bytes_after'],
        'ground_anchor_local_px':r['ground_anchor_local_px'],'detail_preview':detail_preview})

base=Image.open(ROOT/'Assets/Art/new/location/map/map_base.png').convert('RGBA')
order={l['asset_id']:l['order'] for l in layout['layers']};previews={}
for name,color in [('light','#F5F0E8'),('dark','#24113D')]:
    old=Image.new('RGBA',(1920,1080),color);old.alpha_composite(base)
    new=Image.new('RGBA',(1920,1080),color);new.alpha_composite(base)
    for r in sorted(production['assets'],key=lambda r:order[r['id']]):
        old.alpha_composite(old_images[r['id']]);new.alpha_composite(reconstructed[r['id']])
    assert np.array_equal(np.array(old),np.array(new))
    previews['assembly_'+name]=save(new.convert('RGB'),'compact-assembly-'+name+'.png')
    display=Image.new('RGBA',(1280,848),color);display.alpha_composite(new.resize((1280,720),Image.Resampling.LANCZOS),(0,64))
    previews['display_'+name]=save(display.convert('RGB'),'compact-display-'+name+'.png')

gallery=Image.new('RGB',(1440,2180),'#DDD5E4');d=ImageDraw.Draw(gallery)
d.text((28,20),'ART-11 | Construções compactas e posições exatas',font=font(30),fill='#24113D')
d.text((28,68),'Mesmo desenho e resolução | reconstrução RGBA idêntica | 95,3% menos pixels de textura',font=font(21),fill='#30243C')
for i,r in enumerate(records):
    detail=Image.open(ROOT/r['detail_preview']);left=28+(i%2)*704;top=120+(i//2)*410
    # Native pairs are available individually; fit only the comparison gallery.
    if detail.width>676:
        detail=detail.resize((676,round(detail.height*676/detail.width)),Image.Resampling.LANCZOS)
    gallery.paste(detail,(left,top))
gallery.paste(Image.open(PREVIEW/'compact-assembly-light.png').resize((1344,756),Image.Resampling.LANCZOS),(48,1370))
previews['contact_sheet']=save(gallery,'ART-11-compact-contact-sheet.png')
assert all(sha(ROOT/r['output_path'])==before_hashes[r['id']] for r in production['assets'])
result={'task_id':'ART-11','revision':'compact','date':'2026-10-14','passed':True,'asset_count':5,'assets':records,
    'all_rgba_pixels_reconstructed_equal':True,'assembly_equal':True,'maximum_rgba_delta':0,
    'schema_valid':True,'schema_errors':[],'coordinate_contract':'docs/art/layouts/map-layout.json',
    'active_contract_sha256':sha(ROOT/'docs/art/layouts/map-layout.json'),'compact_contract':'docs/art/layouts/map-landmarks-compact.json',
    'summary':production['summary'],'previews':previews,'production_pixels_unchanged_by_qa':True}
(ROOT/'docs/art/qa/ART-11-compact-measurements.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'asset_count':5,'rgba_reconstruction_equal':True,'assembly_equal':True,'schema_valid':True,'summary':production['summary']}))
