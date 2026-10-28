"""Compose existing sprites for ART-36 review. No asset is created or edited.

Every page is 1920x1080. Display proofs use the manifest's real layout box,
uniform contain/cover, or preserved 9-slice borders. Mobile is a 50% optical
stress check, not a claim that a responsive UI has been implemented.
"""
from pathlib import Path
import hashlib
import json
import sys
import io
import time
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT/'art-source/previews/ART-36'
OUT.mkdir(parents=True,exist_ok=True)
M = json.loads((ROOT/'docs/art/manifest.json').read_text(encoding='utf-8'))
ASSETS = [a for a in M['assets'] if int(a['task_id'][4:]) <= 35]
BYID = {a['id']:a for a in ASSETS}
LIGHT='#F5F0E8'
DARK='#24113D'
FONT = 'C:/Windows/Fonts/arial.ttf'
INDEX=[]
COVERAGE={a['id']:[] for a in ASSETS}

def text(im,xy,s,size=16,fill=DARK):
    ImageDraw.Draw(im).text(xy,s,font=ImageFont.truetype(FONT,size),fill=fill)

def load(a):
    return Image.open(ROOT/M['asset_root']/a['path']).convert('RGBA')

def sliced(im,size,borders):
    w,h=size;l,b,r,t=borders
    assert l+r<w and t+b<h
    out=Image.new('RGBA',size)
    xs=[0,l,im.width-r,im.width];ys=[0,t,im.height-b,im.height]
    xd=[0,l,w-r,w];yd=[0,t,h-b,h]
    for y in range(3):
        for x in range(3):
            src=(xs[x],ys[y],xs[x+1],ys[y+1]);dst=(xd[x],yd[y],xd[x+1],yd[y+1])
            if src[2]>src[0] and src[3]>src[1] and dst[2]>dst[0] and dst[3]>dst[1]:
                out.alpha_composite(im.crop(src).resize((dst[2]-dst[0],dst[3]-dst[1]),Image.Resampling.LANCZOS),(dst[0],dst[1]))
    return out

def render(a,size=None):
    size=tuple(round(x) for x in (size or a['display_size_px']))
    im=load(a)
    if a['display_fit']=='sliced': return sliced(im,size,a['nine_slice_lbrt_px'])
    if a['display_fit'] in ['exact','stretch']: return im.resize(size,Image.Resampling.LANCZOS)
    if a['display_fit']=='cover': return ImageOps.fit(im,size,method=Image.Resampling.LANCZOS)
    out=Image.new('RGBA',size)
    fit=ImageOps.contain(im,size,method=Image.Resampling.LANCZOS)
    out.alpha_composite(fit,((size[0]-fit.width)//2,(size[1]-fit.height)//2))
    return out

def tint(im,color):
    out=Image.new('RGBA',im.size,color);out.putalpha(im.getchannel('A'));return out

def white_master(a):
    # Only declared white/tintable geometry, never illustrations or logo.
    return a['id'].startswith('ICON_') or a['id'] in ['UI_SPINNER','TUTORIAL_TAP','TUTORIAL_DRAG','TUTORIAL_FOCUS_RING','UI_MAP_LABEL_TIP']

def save(name,im,ids,kind,notes=''):
    assert im.size==(1920,1080)
    path=OUT/(name+'.png')
    buffer=io.BytesIO();im.convert('RGB').save(buffer,format='PNG')
    data=buffer.getvalue()
    if not path.exists() or path.read_bytes()!=data:
        temporary=path.with_suffix('.tmp')
        temporary.write_bytes(data)
        for attempt in range(10):
            try:
                temporary.replace(path);break
            except PermissionError:
                if attempt==9:raise
                time.sleep(0.3)
    rel=path.relative_to(ROOT).as_posix()
    INDEX.append({'path':rel,'kind':kind,'asset_ids':ids,'size_px':[1920,1080],
                  'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'notes':notes})
    for aid in ids:COVERAGE[aid].append(rel)

def cards():
    page=None;x=y=rowh=0;ids=[];n=0
    def flush():
        nonlocal page,x,y,rowh,ids,n
        if page is not None:
            n+=1;save(f'ART-36-display-{n:02d}',page,ids,'display_light_dark_mobile',
                       '100% manifest display on ivory/plum; right-hand mini is 50% optical stress check. White masters tinted plum only on ivory. Labels are QA overlays.')
        page=Image.new('RGBA',(1920,1080),'#D8D1DF')
        text(page,(16,8),'ART-36 | Display 100% claro / escuro | mini 50% | QA, sem integração',20)
        x=12;y=40;rowh=0;ids=[]
    flush()
    for a in ASSETS:
        w,h=[round(z) for z in a['display_size_px']]
        # Full canvas assets need independent pages.
        if w>=1900 or h>=1000:
            for theme,bg in [('light',LIGHT),('dark',DARK)]:
                im=Image.new('RGBA',(1920,1080),bg)
                im.alpha_composite(render(a),(0,(1080-h)//2))
                save('ART-36-full-'+a['id'].lower()+'-'+theme,im,[a['id']],'full_display',
                     'Full-size display; no title overlay to keep all pixels visible.')
            continue
        wide=2.5*w+40>1896
        if wide and h*2+50>1034:
            for theme,bg in [('light',LIGHT),('dark',DARK)]:
                im=Image.new('RGBA',(1920,1080),bg)
                im.alpha_composite(render(a),(0,(1080-h)//2))
                im.alpha_composite(render(a).resize((w//2,h//2),Image.Resampling.LANCZOS),(1920-w//2,40))
                save('ART-36-full-'+a['id'].lower()+'-'+theme,im,[a['id']],'full_display',
                     'Left: full manifest display. Right: 50% optical stress check.')
            continue
        cw=1896 if wide else round(2.5*w)+40
        ch=(h*2+50) if wide else h+28
        if x+cw>1908:x=12;y+=rowh+6;rowh=0
        if y+ch>1074:flush()
        ImageDraw.Draw(page).rectangle((x,y,x+cw-1,y+ch-1),fill='#BFB5CC')
        label=f'{a["id"]} | {w}x{h}'
        limit=max(8,int((cw-12)/8))
        text(page,(x+6,y+3),label if len(label)<=limit else label[:limit-3]+'...',14)
        src=render(a)
        if wide:
            placements=[(x+6,y+24,LIGHT),(x+6,y+26+h,DARK)]
            mobile_xy=(x+w+24,y+26)
        else:
            placements=[(x+6,y+24,LIGHT),(x+w+16,y+24,DARK)]
            mobile_xy=(x+2*w+26,y+24)
        for xx,yy,bg in placements:
            patch=Image.new('RGBA',src.size,bg)
            patch.alpha_composite(tint(src,DARK) if bg==LIGHT and white_master(a) else src)
            page.alpha_composite(patch,(xx,yy))
        mini=src.resize((max(1,round(w/2)),max(1,round(h/2))),Image.Resampling.LANCZOS)
        patch=Image.new('RGBA',mini.size,DARK);patch.alpha_composite(mini)
        page.alpha_composite(patch,mobile_xy)
        ids.append(a['id']);x+=cw+6;rowh=max(rowh,ch)
        assert x<=1914 and y+rowh<=1074, a['id']
    if ids:
        n+=1;save(f'ART-36-display-{n:02d}',page,ids,'display_light_dark_mobile')

def maps():
    contract=json.loads((ROOT/'docs/art/layouts/map-layout.json').read_text(encoding='utf-8'))
    for restaurant in ['MAP_REST_FRANCES','MAP_REST_JAPONES','MAP_REST_LANCHES']:
        # Different selection in each variant, followed by remaining three below.
        overlay={'MAP_REST_FRANCES':'OVERLAY_BANK','MAP_REST_JAPONES':'OVERLAY_UNIVERSITY','MAP_REST_LANCHES':'OVERLAY_STORE'}[restaurant]
        for theme,bg in [('light',LIGHT),('dark',DARK)]:
            master=Image.new('RGBA',(1920,1080));ids=[]
            for layer in sorted(contract['layers'],key=lambda l:l['order']):
                aid=layer['asset_id']
                if layer.get('selection_group')=='selected_zone' and aid!=overlay:continue
                if layer.get('selection_group')=='restaurant_variant' and aid!=restaurant:continue
                master.alpha_composite(load(BYID[aid]),tuple(layer['position_px']));ids.append(aid)
            im=Image.new('RGBA',(1920,1080),bg)
            im.alpha_composite(master.resize((1280,720),Image.Resampling.LANCZOS),(24,176))
            mini=master.resize((640,360),Image.Resampling.LANCZOS)
            # At right, viewport at 50%, with its left edge shown; entire mini has separate page.
            im.alpha_composite(mini,(1280,620))
            text(im,(24,30),f'ART-36 | {restaurant} | {overlay} | escala comum 2/3',22, DARK if bg==LIGHT else LIGHT)
            text(im,(1330,120),'Master 1920x1080',20,DARK if bg==LIGHT else LIGHT)
            text(im,(1330,155),'Viewport 1280x848',20,DARK if bg==LIGHT else LIGHT)
            text(im,(1330,190),'Desenho 1280x720',20,DARK if bg==LIGHT else LIGHT)
            save('ART-36-map-'+restaurant.lower()+'-'+theme,im,ids,'map_assembly',
                 'Shared uniform 2/3 transform, offset [24,176], one restaurant and one zone highlight. Mini at 1/3 master scale.')
    for overlay in ['OVERLAY_CONDOMINIUM','OVERLAY_MARKETING','OVERLAY_CENTER']:
        im=Image.new('RGBA',(1920,1080),DARK);ids=[]
        for layer in sorted(contract['layers'],key=lambda l:l['order']):
            aid=layer['asset_id']
            if aid.startswith('OVERLAY_') and aid!=overlay:continue
            if layer.get('selection_group')=='restaurant_variant' and aid!='MAP_REST_FRANCES':continue
            im.alpha_composite(load(BYID[aid]),tuple(layer['position_px']));ids.append(aid)
        save('ART-36-map-master-'+overlay.lower(),im,ids,'native_map_assembly','Native master 1:1; no annotations cover geography.')

def dre():
    contract=json.loads((ROOT/'docs/art/layouts/dre-layout.json').read_text(encoding='utf-8'))
    for variant,c in [('monthly',contract),('quarterly',contract['quarterly'])]:
        layer=Image.new('RGBA',(1920,1080));ids=[]
        for l in sorted(c['layers'],key=lambda l:l['order']):
            a=BYID[l['asset_id']];im=load(a);s=l['scale'];size=tuple(round(z*s) for z in im.size)
            xy=tuple(round(l['position_px'][i]-im.size[i]*s*l['pivot'][i]) for i in [0,1])
            layer.alpha_composite(im.resize(size,Image.Resampling.LANCZOS),xy);ids.append(a['id'])
        viewport=contract['clip_rect_px'];x,y,w,h=viewport
        crop=layer.crop((x,y,x+w,y+h))
        page=Image.new('RGBA',(1920,1080),LIGHT)
        text(page,(28,20),'ART-36 | DRE '+variant+' | contrato original, ordem e recorte preservados',24)
        page.alpha_composite(crop,(30,90))
        text(page,(30,62),'Display 560x420 (100%)',18)
        page.alpha_composite(crop.resize((280,210),Image.Resampling.LANCZOS),(610,90))
        text(page,(610,62),'50% (280x210)',18)
        detail=layer.crop((1360,200,1870,500))
        page.alpha_composite(detail,(950,90));text(page,(950,62),'Contato mão / folha / mesa, 1:1 display',18)
        # Isolated transparent layers on dark, preserving viewport clip.
        dark=Image.new('RGBA',(560,420),DARK);dark.alpha_composite(crop)
        page.alpha_composite(dark,(30,600))
        save('ART-36-dre-'+variant,page,ids,'dre_assembly','Original uniform scales, pivot semantics and shared viewport. No masks or pixel edits.')

def native_details():
    # Face and hands at original export pixel density, crop only, no resampling.
    for a in [a for a in ASSETS if a['kind']=='personagem' or a['id'].startswith('PORTRAIT_')]:
        im=load(a);bbox=im.getchannel('A').getbbox()
        x0=max(0,(bbox[0]+bbox[2])//2-210);y0=max(0,bbox[1]-12)
        face=im.crop((x0,y0,min(im.width,x0+420),min(im.height,y0+440)))
        page=Image.new('RGBA',(1920,1080),'#D8D1DF')
        text(page,(20,15),'ART-36 | '+a['id']+' | cortes nativos 1:1, sem reamostragem',22)
        for x,bg in [(20,LIGHT),(470,DARK)]:
            patch=Image.new('RGBA',face.size,bg);patch.alpha_composite(face);page.alpha_composite(patch,(x,60))
        # Entire source width middle section catches hands, torso and clothing.
        yy=min(im.height-500,max(y0+300,im.height//3))
        hands=im.crop((0,yy,min(940,im.width),min(im.height,yy+500)))
        patch=Image.new('RGBA',hands.size,DARK);patch.alpha_composite(hands);page.alpha_composite(patch,(950,60))
        ids=[a['id']]
        for i,cid in enumerate(a['character_ids'][:2]):
            can=load(BYID[cid+'_CANONICAL']);cb=can.getchannel('A').getbbox()
            xx=max(0,(cb[0]+cb[2])//2-160);yy=max(0,cb[1]-8)
            cut=can.crop((xx,yy,xx+320,yy+360))
            patch=Image.new('RGBA',cut.size,LIGHT);patch.alpha_composite(cut);page.alpha_composite(patch,(30+350*i,600))
            text(page,(30+350*i,568),cid+' canônico 1:1',18);ids.append(cid+'_CANONICAL')
        save('ART-36-native-'+a['id'].lower(),page,ids,'native_identity_detail','Crops only; comparison against unchanged canonical source, hashes in inventory.')

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    cards();maps();dre();native_details()
    missing=[aid for aid,paths in COVERAGE.items() if not paths]
    assert not missing,missing
    (ROOT/'docs/art/qa/ART-36-preview-index.json').write_text(json.dumps({'task_id':'ART-36','pages':INDEX,'coverage':COVERAGE,
        'review_status':'pending_direct_inspection','mobile_policy':'50% optical stress only; no responsive layout or tap-target validation.'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'pages':len(INDEX),'assets_covered':len(COVERAGE),'missing':missing}))
